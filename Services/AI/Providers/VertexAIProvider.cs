using System.Net;
using FitnessTracker.API.DTOs;
using FitnessTracker.API.Services.AI;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FitnessTracker.API.Services.AI.Providers
{
    public class VertexAIProvider : IAIProvider
    {
        private readonly HttpClient _httpClient;
        private readonly IGoogleCloudTokenService _tokenService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<VertexAIProvider> _logger;

        public string ProviderName => "Vertex AI (Gemini Pro 2.5)";

        public VertexAIProvider(
            HttpClient httpClient,
            IGoogleCloudTokenService tokenService,
            IConfiguration configuration,
            ILogger<VertexAIProvider> logger)
        {
            _httpClient = httpClient;
            _tokenService = tokenService;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<FoodScanResponse> AnalyzeFoodImageAsync(byte[] imageData, string? userPrompt = null)
        {
            try
            {
                var projectId = _configuration["GoogleCloud:ProjectId"];
                var location = _configuration["GoogleCloud:Location"] ?? "us-central1";
                var model = _configuration["GoogleCloud:Model"] ?? "gemini-2.5-pro";

                var accessToken = await _tokenService.GetAccessTokenAsync();
                var url = $"https://{location}-aiplatform.googleapis.com/v1/projects/{projectId}/locations/{location}/publishers/google/models/{model}:generateContent";

                var base64Image = Convert.ToBase64String(imageData);
                var mimeType = GetImageMimeType(imageData);

                var prompt = $@"
Проанализируй изображение еды и предоставь детальную информацию в СТРОГОМ JSON формате.

{userPrompt ?? ""}

ВАЖНЫЕ ПРАВИЛА ДЛЯ ЕДИНИЦ ИЗМЕРЕНИЯ:
1. Для ЖИДКИХ продуктов используй ""weightType"": ""ml"":
   - Супы, бульоны, борщ, щи
   - Напитки (чай, кофе, сок, компот)
   - Соусы, подливы, жидкие каши

2. Для ТВЕРДЫХ продуктов используй ""weightType"": ""g"":
   - Хлеб, мясо, рыба, овощи, фрукты
   - Каши, гарниры, выпечка, салаты

ОБЯЗАТЕЛЬНЫЕ ТРЕБОВАНИЯ К JSON:
- Используй ТОЛЬКО правильные числа без текста
- Все строки в двойных кавычках
- Не добавляй комментарии в JSON
- Проверь валидность JSON структуры

Верни ТОЛЬКО этот JSON БЕЗ дополнительного текста:
{{
  ""foodItems"": [
    {{
      ""name"": ""название блюда"",
      ""estimatedWeight"": 100,
      ""weightType"": ""g"",
      ""description"": ""описание блюда"",
      ""nutritionPer100g"": {{
        ""calories"": 250,
        ""proteins"": 15.5,
        ""fats"": 10.2,
        ""carbs"": 30.8
      }},
      ""totalCalories"": 250,
      ""confidence"": 0.85
    }}
  ],
  ""estimatedCalories"": 250,
  ""fullDescription"": ""подробное описание всех блюд на изображении""
}}";

                var request = new
                {
                    contents = new[]
                    {
                        new
                        {
                            role = "user",
                            parts = new object[]
                            {
                                new { text = prompt },
                                new
                                {
                                    inline_data = new
                                    {
                                        mime_type = mimeType,
                                        data = base64Image
                                    }
                                }
                            }
                        }
                    },
                    generation_config = new
                    {
                        temperature = 0.1,
                        max_output_tokens = 2048,
                        top_p = 1.0
                    },
                    safety_settings = new[]
                    {
                        new { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_NONE" },
                        new { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_NONE" },
                        new { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_NONE" },
                        new { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_NONE" }
                    }
                };

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                var responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Vertex AI API error: {response.StatusCode} - {responseText}");
                    return CreateFallbackFoodResponse("API error");
                }

                var result = ParseFoodScanResponseWithFallback(responseText);

                if (result.Success && (result.FoodItems == null || !result.FoodItems.Any()))
                {
                    _logger.LogWarning("Empty food items in response, creating fallback");
                    return CreateFallbackFoodResponse("No food items detected");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error analyzing food image: {ex.Message}");
                return CreateFallbackFoodResponse($"Analysis error: {ex.Message}");
            }
        }

        private FoodScanResponse CreateFallbackFoodResponse(string reason)
        {
            _logger.LogInformation($"🍎 Creating fallback food response: {reason}");

            return new FoodScanResponse
            {
                Success = true,
                ErrorMessage = null,
                FoodItems = new List<FoodItemResponse>
                {
                    new FoodItemResponse
                    {
                        Name = "Неопознанное блюдо",
                        EstimatedWeight = 150,
                        WeightType = "g",
                        Description = $"Не удалось определить блюдо ({reason})",
                        NutritionPer100g = new NutritionPer100gDto
                        {
                            Calories = 200,
                            Proteins = 10,
                            Fats = 8,
                            Carbs = 25
                        },
                        TotalCalories = 300,
                        Confidence = 0.3m
                    }
                },
                EstimatedCalories = 300,
                FullDescription = $"Автоматически созданная запись ({reason}). Отредактируйте данные вручную."
            };
        }

        private FoodScanResponse ParseFoodScanResponseWithFallback(string responseText)
        {
            try
            {
                _logger.LogDebug($"🍎 Raw Gemini response: {responseText.Substring(0, Math.Min(500, responseText.Length))}...");

                using var document = JsonDocument.Parse(responseText);

                if (document.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                {
                    var firstCandidate = candidates[0];
                    if (firstCandidate.TryGetProperty("content", out var content) &&
                        content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                    {
                        var textPart = parts[0];
                        if (textPart.TryGetProperty("text", out var textElement))
                        {
                            var responseContent = textElement.GetString() ?? "";
                            _logger.LogDebug($"🍎 Extracted content: {responseContent.Substring(0, Math.Min(300, responseContent.Length))}...");

                            var result = ParseFoodJsonResponseWithFallback(responseContent);
                            if (result.Success)
                            {
                                return result;
                            }
                        }
                    }
                }

                _logger.LogWarning("Invalid Gemini response structure, using fallback");
                return CreateFallbackFoodResponse("Invalid response structure");
            }
            catch (JsonException ex)
            {
                _logger.LogError($"JSON parsing error: {ex.Message}");
                return CreateFallbackFoodResponse("JSON parsing error");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected parsing error: {ex.Message}");
                return CreateFallbackFoodResponse("Parsing error");
            }
        }

        private FoodScanResponse ParseFoodJsonResponseWithFallback(string jsonText)
        {
            try
            {
                var jsonMatch = Regex.Match(jsonText, @"\{(?:[^{}]|(?<open>\{)|(?<-open>\}))*(?(open)(?!))\}", RegexOptions.Singleline);

                if (jsonMatch.Success)
                {
                    var cleanJson = jsonMatch.Value;
                    _logger.LogDebug($"🍎 Extracted JSON: {cleanJson.Substring(0, Math.Min(200, cleanJson.Length))}...");

                    var result = TryParseValidFoodJson(cleanJson);
                    if (result.Success)
                    {
                        return result;
                    }
                }

                var startIndex = jsonText.IndexOf('{');
                var lastIndex = jsonText.LastIndexOf('}');

                if (startIndex >= 0 && lastIndex > startIndex)
                {
                    var cleanJson = jsonText.Substring(startIndex, lastIndex - startIndex + 1);
                    var result = TryParseValidFoodJson(cleanJson);
                    if (result.Success)
                    {
                        return result;
                    }
                }

                var reconstructedResult = TryReconstructFoodData(jsonText);
                if (reconstructedResult.Success)
                {
                    return reconstructedResult;
                }

                _logger.LogWarning($"Failed to parse food JSON from: {jsonText.Substring(0, Math.Min(300, jsonText.Length))}...");
                return CreateFallbackFoodResponse("JSON parsing failed");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in ParseFoodJsonResponseWithFallback: {ex.Message}");
                return CreateFallbackFoodResponse("JSON processing error");
            }
        }

        private FoodScanResponse TryParseValidFoodJson(string jsonText)
        {
            try
            {
                var cleanedJson = CleanJsonText(jsonText);

                using var document = JsonDocument.Parse(cleanedJson);
                var root = document.RootElement;

                var foodItems = new List<FoodItemResponse>();

                if (root.TryGetProperty("foodItems", out var foodItemsArray))
                {
                    foreach (var item in foodItemsArray.EnumerateArray())
                    {
                        try
                        {
                            var foodItem = new FoodItemResponse
                            {
                                Name = SafeGetString(item, "name", "Неизвестное блюдо"),
                                EstimatedWeight = SafeGetDecimal(item, "estimatedWeight", 100),
                                WeightType = SafeGetString(item, "weightType", "g"),
                                Description = SafeGetString(item, "description", ""),
                                Confidence = SafeGetDecimal(item, "confidence", 0.7m)
                            };

                            if (item.TryGetProperty("nutritionPer100g", out var nutrition))
                            {
                                foodItem.NutritionPer100g = new NutritionPer100gDto
                                {
                                    Calories = SafeGetDecimal(nutrition, "calories", 200),
                                    Proteins = SafeGetDecimal(nutrition, "proteins", 10),
                                    Fats = SafeGetDecimal(nutrition, "fats", 5),
                                    Carbs = SafeGetDecimal(nutrition, "carbs", 30)
                                };
                            }

                            foodItem.TotalCalories = SafeGetInt(item, "totalCalories",
                                (int)Math.Round((foodItem.NutritionPer100g.Calories * foodItem.EstimatedWeight) / 100));

                            foodItems.Add(foodItem);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Error parsing food item: {ex.Message}");
                            continue;
                        }
                    }
                }

                if (foodItems.Any())
                {
                    return new FoodScanResponse
                    {
                        Success = true,
                        FoodItems = foodItems,
                        EstimatedCalories = SafeGetInt(root, "estimatedCalories", foodItems.Sum(f => f.TotalCalories)),
                        FullDescription = SafeGetString(root, "fullDescription", "Анализ выполнен успешно")
                    };
                }

                return new FoodScanResponse { Success = false };
            }
            catch (JsonException ex)
            {
                _logger.LogWarning($"JSON parsing failed: {ex.Message}");
                return new FoodScanResponse { Success = false };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error in TryParseValidFoodJson: {ex.Message}");
                return new FoodScanResponse { Success = false };
            }
        }

        private string CleanJsonText(string jsonText)
        {
            jsonText = Regex.Replace(jsonText, @"//.*$", "", RegexOptions.Multiline);
            jsonText = Regex.Replace(jsonText, @"/\*.*?\*/", "", RegexOptions.Singleline);

            jsonText = jsonText.Replace("'", "\""); 
            jsonText = Regex.Replace(jsonText, @",\s*}", "}"); 
            jsonText = Regex.Replace(jsonText, @",\s*]", "]"); 

            return jsonText.Trim();
        }

        private FoodScanResponse TryReconstructFoodData(string text)
        {
            try
            {
                _logger.LogInformation("🍎 Attempting to reconstruct food data from text");

                var foodKeywords = new[] { "хлеб", "мясо", "рыба", "курица", "говядина", "свинина", "овощи", "фрукты",
                                         "картофель", "рис", "гречка", "макароны", "салат", "суп", "борщ", "каша" };

                var detectedFood = foodKeywords.FirstOrDefault(keyword =>
                    text.ToLowerInvariant().Contains(keyword));

                if (!string.IsNullOrEmpty(detectedFood))
                {
                    return new FoodScanResponse
                    {
                        Success = true,
                        FoodItems = new List<FoodItemResponse>
                        {
                            new FoodItemResponse
                            {
                                Name = char.ToUpper(detectedFood[0]) + detectedFood[1..],
                                EstimatedWeight = 150,
                                WeightType = "g",
                                Description = $"Обнаружено по ключевому слову: {detectedFood}",
                                NutritionPer100g = GetDefaultNutrition(detectedFood),
                                TotalCalories = (int)(GetDefaultNutrition(detectedFood).Calories * 1.5m),
                                Confidence = 0.5m
                            }
                        },
                        EstimatedCalories = (int)(GetDefaultNutrition(detectedFood).Calories * 1.5m),
                        FullDescription = $"Восстановленные данные на основе анализа текста"
                    };
                }

                return new FoodScanResponse { Success = false };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in TryReconstructFoodData: {ex.Message}");
                return new FoodScanResponse { Success = false };
            }
        }

        private NutritionPer100gDto GetDefaultNutrition(string foodType)
        {
            return foodType.ToLowerInvariant() switch
            {
                var x when x.Contains("хлеб") => new NutritionPer100gDto { Calories = 250, Proteins = 8, Fats = 3, Carbs = 50 },
                var x when x.Contains("мясо") || x.Contains("говядина") => new NutritionPer100gDto { Calories = 250, Proteins = 26, Fats = 15, Carbs = 0 },
                var x when x.Contains("курица") => new NutritionPer100gDto { Calories = 165, Proteins = 31, Fats = 3.6m, Carbs = 0 },
                var x when x.Contains("рыба") => new NutritionPer100gDto { Calories = 200, Proteins = 20, Fats = 12, Carbs = 0 },
                var x when x.Contains("картофель") => new NutritionPer100gDto { Calories = 80, Proteins = 2, Fats = 0.1m, Carbs = 17 },
                var x when x.Contains("рис") => new NutritionPer100gDto { Calories = 130, Proteins = 2.7m, Fats = 0.3m, Carbs = 28 },
                var x when x.Contains("гречка") => new NutritionPer100gDto { Calories = 340, Proteins = 13, Fats = 3.4m, Carbs = 62 },
                var x when x.Contains("овощи") => new NutritionPer100gDto { Calories = 25, Proteins = 1.2m, Fats = 0.2m, Carbs = 5 },
                var x when x.Contains("фрукты") => new NutritionPer100gDto { Calories = 60, Proteins = 0.8m, Fats = 0.2m, Carbs = 15 },
                _ => new NutritionPer100gDto { Calories = 200, Proteins = 10, Fats = 8, Carbs = 25 }
            };
        }

        public async Task<BodyScanResponse> AnalyzeBodyImagesAsync(
            byte[]? frontImageData,
            byte[]? sideImageData,
            byte[]? backImageData,
            decimal? weight = null,
            decimal? height = null,
            int? age = null,
            string? gender = null,
            string? goals = null)
        {
            try
            {
                var projectId = _configuration["GoogleCloud:ProjectId"];
                var location = _configuration["GoogleCloud:Location"] ?? "us-central1";
                var model = _configuration["GoogleCloud:Model"] ?? "gemini-2.5-pro";

                var accessToken = await _tokenService.GetAccessTokenAsync();
                var url = $"https://{location}-aiplatform.googleapis.com/v1/projects/{projectId}/locations/{location}/publishers/google/models/{model}:generateContent";

                var parts = new List<object>();

                var prompt = $@"
Проанализируй изображения тела и предоставь подробную оценку в JSON формате.

Данные пользователя:
- Вес: {weight ?? 0}кг
- Рост: {height ?? 0}см
- Возраст: {age ?? 0}
- Пол: {gender ?? "не указан"}
- Цели: {goals ?? "не указаны"}

Верни ТОЛЬКО JSON без дополнительного текста:
{{
  ""bodyAnalysis"": {{
    ""estimatedBodyFatPercentage"": процент_жира,
    ""estimatedMusclePercentage"": процент_мышц,
    ""bodyType"": ""тип телосложения"",
    ""postureAnalysis"": ""анализ осанки"",
    ""overallCondition"": ""общее состояние"",
    ""bmi"": индекс_массы_тела,
    ""bmiCategory"": ""категория ИМТ"",
    ""estimatedWaistCircumference"": обхват_талии_см,
    ""estimatedChestCircumference"": обхват_груди_см,
    ""estimatedHipCircumference"": обхват_бедер_см,
    ""basalMetabolicRate"": основной_обмен_ккал,
    ""metabolicRateCategory"": ""категория метаболизма"",
    ""exerciseRecommendations"": [""рекомендация1"", ""рекомендация2""],
    ""nutritionRecommendations"": [""рекомендация1"", ""рекомендация2""],
    ""trainingFocus"": ""фокус тренировок""
  }},
  ""recommendations"": [""общая рекомендация1"", ""общая рекомендация2""],
  ""fullAnalysis"": ""подробный анализ""
}}";

                parts.Add(new { text = prompt });

                if (frontImageData != null)
                {
                    parts.Add(new
                    {
                        inline_data = new
                        {
                            mime_type = GetImageMimeType(frontImageData),
                            data = Convert.ToBase64String(frontImageData)
                        }
                    });
                }

                if (sideImageData != null)
                {
                    parts.Add(new
                    {
                        inline_data = new
                        {
                            mime_type = GetImageMimeType(sideImageData),
                            data = Convert.ToBase64String(sideImageData)
                        }
                    });
                }

                if (backImageData != null)
                {
                    parts.Add(new
                    {
                        inline_data = new
                        {
                            mime_type = GetImageMimeType(backImageData),
                            data = Convert.ToBase64String(backImageData)
                        }
                    });
                }

                var request = new
                {
                    contents = new[]
                    {
                        new
                        {
                            role = "user",
                            parts = parts.ToArray()
                        }
                    },
                    generation_config = new
                    {
                        temperature = 0.1,
                        max_output_tokens = 2048,
                        top_p = 1.0
                    }
                };

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                var responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Vertex AI API error: {response.StatusCode} - {responseText}");
                    return new BodyScanResponse { Success = false, ErrorMessage = $"API error: {response.StatusCode}" };
                }

                return ParseBodyScanResponse(responseText);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error analyzing body images: {ex.Message}");
                return new BodyScanResponse { Success = false, ErrorMessage = ex.Message };
            }
        }

        public async Task<VoiceWorkoutResponse> AnalyzeVoiceWorkoutAsync(byte[] audioData, string? workoutType = null)
        {
            try
            {
                if (audioData == null || audioData.Length == 0)
                {
                    return CreateIntelligentFallback("Пустой аудио файл", workoutType);
                }

                if (audioData.Length > 50 * 1024 * 1024)
                {
                    return CreateIntelligentFallback("Файл слишком большой", workoutType);
                }

                var projectId = _configuration["GoogleCloud:ProjectId"];
                var location = _configuration["GoogleCloud:Location"] ?? "us-central1";
                var model = _configuration["GoogleCloud:Model"] ?? "gemini-2.5-pro";

                if (string.IsNullOrEmpty(projectId))
                {
                    _logger.LogError("❌ GoogleCloud:ProjectId not configured");
                    return CreateIntelligentFallback("Сервис ИИ не настроен", workoutType);
                }

                string accessToken;
                try
                {
                    accessToken = await _tokenService.GetAccessTokenAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ Failed to get access token: {ex.Message}");
                    return CreateIntelligentFallback("Ошибка аутентификации", workoutType);
                }

                var url = $"https://{location}-aiplatform.googleapis.com/v1/projects/{projectId}/locations/{location}/publishers/google/models/{model}:generateContent";

                var base64Audio = Convert.ToBase64String(audioData);
                var mimeType = GetAudioMimeType(audioData);

                var prompt = @"
Ты - продвинутый ИИ-тренер, который умеет анализировать голосовые записи о тренировках и КРЕАТИВНО додумывать недостающие детали.

ВАЖНО: Даже если запись нечеткая или неполная - всегда создавай ПОЛНЫЙ и РЕАЛИСТИЧНЫЙ ответ!

Тип тренировки: {workoutType ?? ""автоопределение""}

🧠 КРЕАТИВНЫЕ ПРАВИЛА ДОДУМЫВАНИЯ:
1. Если слышишь частичную информацию - ДОДУМАЙ реалистичные детали
2. Если непонятно упражнение - выбери популярное похожее
3. Если нет веса - подбери адекватный для среднего человека
4. Если нет времени - используй разумные интервалы
5. Если нет повторений - используй стандартные 8-12 для силовых, 20-30 для кардио
6. ВСЕГДА создавай полную тренировку, даже из минимальной информации

📝 ПРИМЕРЫ КРЕАТИВНОГО ДОДУМЫВАНИЯ:
- ""делал жим"" → ""Жим штанги лежа 60кг на 10 повторений, 3 подхода""
- ""бегал"" → ""Бег трусцой 3км за 20 минут, пульс 140-160""
- ""качался"" → ""Силовая тренировка: жим лежа 50кг, приседания 40кг""
- ""тренировался"" → создай полноценную тренировку на основе контекста

⏰ УМНОЕ ОПРЕДЕЛЕНИЕ ВРЕМЕНИ:
- Если время не указано → используй СЕЙЧАС как время начала
- Силовая тренировка → добавь 45-60 минут
- Кардио → добавь 20-40 минут
- Время в формате: ""2025-07-17T17:00:00Z""
        
🏋️ РЕАЛИСТИЧНЫЕ ЗНАЧЕНИЯ ПО УМОЛЧАНИЮ:
- Жим лежа: 40-80кг
- Приседания: 50-100кг
- Тяга: 60-120кг
- Отжимания: собственный вес
- Бег: 5-12 км/ч
- Велосипед: 15-25 км/ч
        
ОБЯЗАТЕЛЬНО верни ТОЛЬКО валидный JSON:
{
  ""transcribedText"": ""точный или улучшенный текст"",
  ""workoutData"": {
    ""type"": ""strength"" или ""cardio"",
    ""startTime"": ""2025-07-17T17:00:00Z"",
    ""endTime"": ""2025-07-17T17:45:00Z"", 
    ""estimatedCalories"": реалистичное_число,
    ""strengthData"": {
      ""name"": ""Конкретное упражнение"",
      ""muscleGroup"": ""Группа мышц"",
      ""equipment"": ""Тип оборудования"",
      ""workingWeight"": реалистичный_вес,
      ""restTimeSeconds"": 60-180,
      ""sets"": [
        {
          ""setNumber"": 1,
          ""weight"": вес,
          ""reps"": повторения,
          ""isCompleted"": true,
          ""notes"": ""Подход выполнен""
        }
      ]
    },
    ""cardioData"": {
      ""cardioType"": ""Тип кардио"",
      ""distanceKm"": расстояние_или_null,
      ""avgPulse"": средний_пульс_или_null,
      ""maxPulse"": максимальный_пульс_или_null,
      ""avgPace"": ""темп""
    },
    ""notes"": [""Креативные заметки о тренировке""]
  }
}

🎯 КРЕАТИВНЫЙ ПОДХОД:
- Если слышишь ""жим"" - создай полную тренировку с жимом лежа
- Если слышишь ""бег"" - создай кардио сессию с реалистичными параметрами
- Если слышишь ""качался"" - создай силовую тренировку из 2-3 упражнений
- Если ничего не понятно - создай базовую тренировку подходящую для новичка

ПОМНИ: Твоя задача - всегда давать ПОЛЕЗНЫЙ результат, даже если аудио нечеткое!";

                var request = new
                {
                    contents = new[]
                    {
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new { text = prompt },
                        new
                        {
                            inline_data = new
                            {
                                mime_type = mimeType,
                                data = base64Audio
                            }
                        }
                    }
                }
            },
                    generation_config = new
                    {
                        temperature = 0.7,
                        max_output_tokens = 3072,
                        top_p = 0.9,
                        top_k = 40
                    },
                    safety_settings = new[]
                    {
                new
                {
                    category = "HARM_CATEGORY_HARASSMENT",
                    threshold = "BLOCK_NONE"
                },
                new
                {
                    category = "HARM_CATEGORY_HATE_SPEECH",
                    threshold = "BLOCK_NONE"
                },
                new
                {
                    category = "HARM_CATEGORY_SEXUALLY_EXPLICIT",
                    threshold = "BLOCK_NONE"
                },
                new
                {
                    category = "HARM_CATEGORY_DANGEROUS_CONTENT",
                    threshold = "BLOCK_NONE"
                }
            }
                };

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.PostAsync(url, content);
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError($"❌ HTTP request failed: {ex.Message}");
                    return CreateIntelligentFallback("Ошибка сети", workoutType);
                }
                catch (TaskCanceledException ex)
                {
                    _logger.LogError($"❌ Request timeout: {ex.Message}");
                    return CreateIntelligentFallback("Превышено время ожидания", workoutType);
                }

                var responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"❌ Vertex AI API error: {response.StatusCode} - {responseText}");
                    return CreateIntelligentFallback($"Ошибка API: {response.StatusCode}", workoutType);
                }

                return ParseVoiceWorkoutResponseWithFallback(responseText, workoutType);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Unexpected error in voice workout analysis: {ex.Message}");
                return CreateIntelligentFallback("Системная ошибка", workoutType);
            }
        }

        public async Task<VoiceFoodResponse> AnalyzeVoiceFoodAsync(byte[] audioData, string? mealType = null)
        {
            try
            {
                var projectId = _configuration["GoogleCloud:ProjectId"];
                var location = _configuration["GoogleCloud:Location"] ?? "us-central1";
                var model = _configuration["GoogleCloud:Model"] ?? "gemini-2.5-pro";

                var accessToken = await _tokenService.GetAccessTokenAsync();
                var url = $"https://{location}-aiplatform.googleapis.com/v1/projects/{projectId}/locations/{location}/publishers/google/models/{model}:generateContent";

                var base64Audio = Convert.ToBase64String(audioData);
                var mimeType = GetAudioMimeType(audioData);

                var prompt = $@"
Распознай речь из аудио и извлеки информацию о питании в JSON формате.

Тип приема пищи: {mealType ?? "любой"}

ВАЖНЫЕ ПРАВИЛА ДЛЯ ЕДИНИЦ ИЗМЕРЕНИЯ:
1. Для ЖИДКИХ продуктов используй ""weightType"": ""ml"" (миллилитры):
   - Супы (борщ, щи, солянка, бульон)
   - Напитки (чай, кофе, сок, вода, молоко)
   - Соусы, подливы
   - Жидкие каши (овсянка на молоке)
   - Смузи, коктейли

2. Для ТВЕРДЫХ продуктов используй ""weightType"": ""g"" (граммы):
   - Хлеб, мясо, рыба, овощи, фрукты
   - Твердые каши (гречка, рис)
   - Выпечка, сладости
   - Орехи, семечки

3. Для жидких продуктов ""estimatedWeight"" = объем в миллилитрах
4. Для твердых продуктов ""estimatedWeight"" = вес в граммах

ПРИМЕРЫ:
- ""Борщ 300 мл"" → ""estimatedWeight"": 300, ""weightType"": ""ml""
- ""Чай 200 мл"" → ""estimatedWeight"": 200, ""weightType"": ""ml""
- ""Хлеб 50 г"" → ""estimatedWeight"": 50, ""weightType"": ""g""
- ""Яблоко 150 г"" → ""estimatedWeight"": 150, ""weightType"": ""g""

Верни ТОЛЬКО валидный JSON без дополнительного текста:
{{
  ""transcribedText"": ""точный распознанный текст"",
  ""foodItems"": [
    {{
      ""name"": ""название блюда"",
      ""estimatedWeight"": количество_в_правильных_единицах,
      ""weightType"": ""ml или g"",
      ""description"": ""краткое описание продукта"",
      ""nutritionPer100g"": {{
        ""calories"": калории_на_100г_или_100мл,
        ""proteins"": белки_на_100г_или_100мл,
        ""fats"": жиры_на_100г_или_100мл,
        ""carbs"": углеводы_на_100г_или_100мл
      }},
      ""totalCalories"": общие_калории_порции,
      ""confidence"": уверенность_от_0_до_1
    }}
  ],
  ""estimatedTotalCalories"": общие_калории_всех_продуктов
}}

КОНКРЕТНЫЕ ПРИМЕРЫ ОТВЕТОВ:
- Борщ:
{{
  ""name"": ""Борщ"",
  ""estimatedWeight"": 300,
  ""weightType"": ""ml"",
  ""nutritionPer100g"": {{""calories"": 45, ""proteins"": 2.0, ""fats"": 1.5, ""carbs"": 6.0}},
  ""totalCalories"": 135
}}

- Хлеб:
{{
  ""name"": ""Хлеб белый"",
  ""estimatedWeight"": 50,
  ""weightType"": ""g"",
  ""nutritionPer100g"": {{""calories"": 265, ""proteins"": 8.1, ""fats"": 3.2, ""carbs"": 48.8}},
  ""totalCalories"": 132
}}

- Молоко:
{{
  ""name"": ""Молоко"",
  ""estimatedWeight"": 200,
  ""weightType"": ""ml"",
  ""nutritionPer100g"": {{""calories"": 64, ""proteins"": 3.2, ""fats"": 3.6, ""carbs"": 4.8}},
  ""totalCalories"": 128
}}";

                var request = new
                {
                    contents = new[]
                    {
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new { text = prompt },
                        new
                        {
                            inline_data = new
                            {
                                mime_type = mimeType,
                                data = base64Audio
                            }
                        }
                    }
                }
            },
                    generation_config = new
                    {
                        temperature = 0.1,
                        max_output_tokens = 2048,
                        top_p = 1.0
                    }
                };

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                var responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Vertex AI API error: {response.StatusCode} - {responseText}");
                    return new VoiceFoodResponse { Success = false, ErrorMessage = $"API error: {response.StatusCode}" };
                }

                return ParseVoiceFoodResponse(responseText);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error analyzing voice food: {ex.Message}");
                return new VoiceFoodResponse { Success = false, ErrorMessage = ex.Message };
            }
        }

        public async Task<bool> IsHealthyAsync()
        {
            try
            {
                var request = new
                {
                    contents = new[]
                    {
                        new
                        {
                            role = "user",
                            parts = new[]
                            {
                                new { text = "Say 'OK' if you are working" }
                            }
                        }
                    }
                };

                var projectId = _configuration["GoogleCloud:ProjectId"];
                var location = _configuration["GoogleCloud:Location"] ?? "us-central1";
                var model = _configuration["GoogleCloud:Model"] ?? "gemini-2.5-pro";

                var accessToken = await _tokenService.GetAccessTokenAsync();
                var url = $"https://{location}-aiplatform.googleapis.com/v1/projects/{projectId}/locations/{location}/publishers/google/models/{model}:generateContent";

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        // =============== НОВЫЕ МЕТОДЫ ===============

        /// <summary>
        /// ✅ НОВОЕ: Создает умный fallback ответ для voice workout
        /// </summary>
        private VoiceWorkoutResponse CreateIntelligentFallback(string errorReason, string? workoutType)
        {
            var type = DetermineWorkoutType(workoutType);
            var defaultWorkout = CreateDefaultWorkoutData(errorReason, type);

            return new VoiceWorkoutResponse
            {
                Success = true,
                ErrorMessage = null,
                TranscribedText = $"Не удалось распознать аудио ({errorReason}), но создана базовая тренировка",
                WorkoutData = defaultWorkout
            };
        }

        /// <summary>
        /// ✅ НОВОЕ: Парсит ответ с fallback
        /// </summary>
        private VoiceWorkoutResponse ParseVoiceWorkoutResponseWithFallback(string responseText, string? workoutType)
        {
            try
            {
                var parsedResponse = ParseVoiceWorkoutResponse(responseText);
                if (parsedResponse.Success && parsedResponse.WorkoutData != null)
                {
                    return parsedResponse;
                }

                _logger.LogWarning("Failed to parse voice workout response, using fallback");
                return CreateIntelligentFallback("Ошибка парсинга ответа ИИ", workoutType);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error parsing voice workout response: {ex.Message}");
                return CreateIntelligentFallback("Ошибка обработки ответа", workoutType);
            }
        }

        /// <summary>
        /// ✅ НОВОЕ: Определяет тип тренировки
        /// </summary>
        private string DetermineWorkoutType(string? workoutType)
        {
            if (string.IsNullOrEmpty(workoutType))
                return "strength"; // По умолчанию силовая

            return workoutType.ToLowerInvariant() switch
            {
                "strength" or "силовая" or "качалка" => "strength",
                "cardio" or "кардио" or "бег" => "cardio",
                _ => "strength"
            };
        }

        /// <summary>
        /// ✅ НОВОЕ: Создает данные тренировки по умолчанию
        /// </summary>
        private WorkoutDataResponse CreateDefaultWorkoutData(string reason, string type)
        {
            var startTime = DateTime.UtcNow;
            var endTime = startTime.AddMinutes(type == "cardio" ? 30 : 45);

            var workout = new WorkoutDataResponse
            {
                Type = type,
                StartTime = startTime,
                EndTime = endTime,
                EstimatedCalories = type == "cardio" ? 200 : 250,
                Notes = new List<string> { $"Автоматически созданная тренировка ({reason})" }
            };

            if (type == "strength")
            {
                workout.StrengthData = new StrengthDataDto
                {
                    Name = "Базовое упражнение",
                    MuscleGroup = "Общая группа мышц",
                    Equipment = "Собственный вес",
                    WorkingWeight = 0,
                    RestTimeSeconds = 120,
                    Sets = new List<StrengthSetDto>
                    {
                        new StrengthSetDto
                        {
                            SetNumber = 1,
                            Weight = 0,
                            Reps = 10,
                            IsCompleted = true,
                            Notes = "Базовый подход"
                        }
                    }
                };
            }
            else
            {
                workout.CardioData = new CardioDataDto
                {
                    CardioType = "Общее кардио",
                    DistanceKm = null,
                    AvgPulse = null,
                    MaxPulse = null,
                    AvgPace = ""
                };
            }

            return workout;
        }

        // Helper methods
        private string GetImageMimeType(byte[] imageData)
        {
            if (imageData.Length >= 2)
            {
                // JPEG
                if (imageData[0] == 0xFF && imageData[1] == 0xD8)
                    return "image/jpeg";

                // PNG
                if (imageData.Length >= 8 && imageData[0] == 0x89 && imageData[1] == 0x50 &&
                    imageData[2] == 0x4E && imageData[3] == 0x47)
                    return "image/png";

                // GIF
                if (imageData.Length >= 6 && imageData[0] == 0x47 && imageData[1] == 0x49 && imageData[2] == 0x46)
                    return "image/gif";
            }

            return "image/jpeg"; // Default
        }

        private string GetAudioMimeType(byte[] audioData)
        {
            // Simple audio format detection
            if (audioData.Length >= 4)
            {
                // WAV
                if (audioData[0] == 0x52 && audioData[1] == 0x49 && audioData[2] == 0x46 && audioData[3] == 0x46)
                    return "audio/wav";

                // MP3
                if (audioData.Length >= 3 && audioData[0] == 0xFF && (audioData[1] & 0xE0) == 0xE0)
                    return "audio/mp3";

                // OGG
                if (audioData.Length >= 4 && audioData[0] == 0x4F && audioData[1] == 0x67 && audioData[2] == 0x67 && audioData[3] == 0x53)
                    return "audio/ogg";
            }

            return "audio/ogg"; // Default для .ogg файлов
        }

        private FoodScanResponse ParseFoodScanResponse(string responseText)
        {
            try
            {
                using var document = JsonDocument.Parse(responseText);

                if (document.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                {
                    var firstCandidate = candidates[0];
                    if (firstCandidate.TryGetProperty("content", out var content) &&
                        content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                    {
                        var textPart = parts[0];
                        if (textPart.TryGetProperty("text", out var textElement))
                        {
                            var responseContent = textElement.GetString() ?? "";
                            return ParseFoodJsonResponse(responseContent);
                        }
                    }
                }

                return new FoodScanResponse { Success = false, ErrorMessage = "Invalid response format" };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error parsing food scan response: {ex.Message}");
                return new FoodScanResponse { Success = false, ErrorMessage = "Failed to parse response" };
            }
        }

        private BodyScanResponse ParseBodyScanResponse(string responseText)
        {
            try
            {
                using var document = JsonDocument.Parse(responseText);

                if (document.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                {
                    var firstCandidate = candidates[0];
                    if (firstCandidate.TryGetProperty("content", out var content) &&
                        content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                    {
                        var textPart = parts[0];
                        if (textPart.TryGetProperty("text", out var textElement))
                        {
                            var responseContent = textElement.GetString() ?? "";
                            return ParseBodyJsonResponse(responseContent);
                        }
                    }
                }

                return new BodyScanResponse { Success = false, ErrorMessage = "Invalid response format" };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error parsing body scan response: {ex.Message}");
                return new BodyScanResponse { Success = false, ErrorMessage = "Failed to parse response" };
            }
        }

        private VoiceWorkoutResponse ParseVoiceWorkoutResponse(string responseText)
        {
            try
            {
                _logger.LogInformation($"🎤 Raw Gemini response: {responseText}");

                using var document = JsonDocument.Parse(responseText);

                if (document.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                {
                    var firstCandidate = candidates[0];
                    if (firstCandidate.TryGetProperty("content", out var content) &&
                        content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                    {
                        var textPart = parts[0];
                        if (textPart.TryGetProperty("text", out var textElement))
                        {
                            var responseContent = textElement.GetString() ?? "";
                            _logger.LogInformation($"🎤 Extracted text content: {responseContent.Substring(0, Math.Min(200, responseContent.Length))}...");
                            return ParseVoiceWorkoutJsonResponse(responseContent);
                        }
                    }
                }

                _logger.LogError($"❌ Invalid Gemini response structure: {responseText.Substring(0, Math.Min(500, responseText.Length))}");
                return new VoiceWorkoutResponse
                {
                    Success = false,
                    ErrorMessage = "Invalid response format from AI service",
                    TranscribedText = "Не удалось распознать речь"
                };
            }
            catch (JsonException ex)
            {
                _logger.LogError($"❌ JSON parsing error in voice workout response: {ex.Message}");
                _logger.LogError($"Response content: {responseText.Substring(0, Math.Min(1000, responseText.Length))}");

                return new VoiceWorkoutResponse
                {
                    Success = false,
                    ErrorMessage = "Failed to parse AI response",
                    TranscribedText = "Ошибка распознавания речи"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Unexpected error parsing voice workout response: {ex.Message}");
                return new VoiceWorkoutResponse
                {
                    Success = false,
                    ErrorMessage = "Unexpected error during response processing",
                    TranscribedText = "Системная ошибка"
                };
            }
        }

        private VoiceFoodResponse ParseVoiceFoodResponse(string responseText)
        {
            try
            {
                using var document = JsonDocument.Parse(responseText);

                if (document.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                {
                    var firstCandidate = candidates[0];
                    if (firstCandidate.TryGetProperty("content", out var content) &&
                        content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                    {
                        var textPart = parts[0];
                        if (textPart.TryGetProperty("text", out var textElement))
                        {
                            var responseContent = textElement.GetString() ?? "";
                            return ParseVoiceFoodJsonResponse(responseContent);
                        }
                    }
                }

                return new VoiceFoodResponse { Success = false, ErrorMessage = "Invalid response format" };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error parsing voice food response: {ex.Message}");
                return new VoiceFoodResponse { Success = false, ErrorMessage = "Failed to parse response" };
            }
        }

        private FoodScanResponse ParseFoodJsonResponse(string jsonText)
        {
            try
            {
                // Извлекаем JSON из текста
                var startIndex = jsonText.IndexOf('{');
                var lastIndex = jsonText.LastIndexOf('}');

                if (startIndex >= 0 && lastIndex > startIndex)
                {
                    var cleanJson = jsonText.Substring(startIndex, lastIndex - startIndex + 1);

                    using var document = JsonDocument.Parse(cleanJson);
                    var root = document.RootElement;

                    var foodItems = new List<FoodItemResponse>();

                    if (root.TryGetProperty("foodItems", out var foodItemsArray))
                    {
                        foreach (var item in foodItemsArray.EnumerateArray())
                        {
                            var foodItem = new FoodItemResponse
                            {
                                Name = item.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                                EstimatedWeight = item.TryGetProperty("estimatedWeight", out var weight) ? weight.GetDecimal() : 0,
                                WeightType = item.TryGetProperty("weightType", out var weightType) ? weightType.GetString() ?? "g" : "g",
                                Description = item.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
                                Confidence = item.TryGetProperty("confidence", out var conf) ? conf.GetDecimal() : 0.8m
                            };

                            if (item.TryGetProperty("nutritionPer100g", out var nutrition))
                            {
                                foodItem.NutritionPer100g = new NutritionPer100gDto
                                {
                                    Calories = nutrition.TryGetProperty("calories", out var cal) ? cal.GetDecimal() : 0,
                                    Proteins = nutrition.TryGetProperty("proteins", out var prot) ? prot.GetDecimal() : 0,
                                    Fats = nutrition.TryGetProperty("fats", out var fats) ? fats.GetDecimal() : 0,
                                    Carbs = nutrition.TryGetProperty("carbs", out var carbs) ? carbs.GetDecimal() : 0
                                };
                            }

                            foodItem.TotalCalories = item.TryGetProperty("totalCalories", out var itemTotalCal) ? itemTotalCal.GetInt32() : 0;
                            foodItems.Add(foodItem);
                        }
                    }

                    return new FoodScanResponse
                    {
                        Success = true,
                        FoodItems = foodItems,
                        EstimatedCalories = root.TryGetProperty("estimatedCalories", out var estCal) ? estCal.GetInt32() : 0,
                        FullDescription = root.TryGetProperty("fullDescription", out var fullDesc) ? fullDesc.GetString() : ""
                    };
                }

                return new FoodScanResponse { Success = false, ErrorMessage = "Invalid JSON format" };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error parsing food JSON: {ex.Message}");
                return new FoodScanResponse { Success = false, ErrorMessage = "Failed to parse food data" };
            }
        }

        private BodyScanResponse ParseBodyJsonResponse(string jsonText)
        {
            try
            {
                var startIndex = jsonText.IndexOf('{');
                var lastIndex = jsonText.LastIndexOf('}');

                if (startIndex >= 0 && lastIndex > startIndex)
                {
                    var cleanJson = jsonText.Substring(startIndex, lastIndex - startIndex + 1);

                    using var document = JsonDocument.Parse(cleanJson);
                    var root = document.RootElement;

                    var bodyAnalysis = new BodyAnalysisDto();

                    if (root.TryGetProperty("bodyAnalysis", out var analysis))
                    {
                        bodyAnalysis.EstimatedBodyFatPercentage = analysis.TryGetProperty("estimatedBodyFatPercentage", out var bf) ? bf.GetDecimal() : 0;
                        bodyAnalysis.EstimatedMusclePercentage = analysis.TryGetProperty("estimatedMusclePercentage", out var mp) ? mp.GetDecimal() : 0;
                        bodyAnalysis.BodyType = analysis.TryGetProperty("bodyType", out var bt) ? bt.GetString() ?? "" : "";
                        bodyAnalysis.PostureAnalysis = analysis.TryGetProperty("postureAnalysis", out var pa) ? pa.GetString() ?? "" : "";
                        bodyAnalysis.OverallCondition = analysis.TryGetProperty("overallCondition", out var oc) ? oc.GetString() ?? "" : "";
                        bodyAnalysis.BMI = analysis.TryGetProperty("bmi", out var bmi) ? bmi.GetDecimal() : 0;
                        bodyAnalysis.BMICategory = analysis.TryGetProperty("bmiCategory", out var bmiCat) ? bmiCat.GetString() ?? "" : "";
                        bodyAnalysis.EstimatedWaistCircumference = analysis.TryGetProperty("estimatedWaistCircumference", out var waist) ? waist.GetDecimal() : 0;
                        bodyAnalysis.EstimatedChestCircumference = analysis.TryGetProperty("estimatedChestCircumference", out var chest) ? chest.GetDecimal() : 0;
                        bodyAnalysis.EstimatedHipCircumference = analysis.TryGetProperty("estimatedHipCircumference", out var hip) ? hip.GetDecimal() : 0;
                        bodyAnalysis.BasalMetabolicRate = analysis.TryGetProperty("basalMetabolicRate", out var bmr) ? bmr.GetInt32() : 1500;
                        bodyAnalysis.MetabolicRateCategory = analysis.TryGetProperty("metabolicRateCategory", out var mrc) ? mrc.GetString() ?? "Нормальный" : "Нормальный";
                        bodyAnalysis.TrainingFocus = analysis.TryGetProperty("trainingFocus", out var tf) ? tf.GetString() ?? "" : "";

                        if (analysis.TryGetProperty("exerciseRecommendations", out var exRecs))
                        {
                            bodyAnalysis.ExerciseRecommendations = exRecs.EnumerateArray()
                                .Select(x => x.GetString() ?? "").Where(x => !string.IsNullOrEmpty(x)).ToList();
                        }

                        if (analysis.TryGetProperty("nutritionRecommendations", out var nutRecs))
                        {
                            bodyAnalysis.NutritionRecommendations = nutRecs.EnumerateArray()
                                .Select(x => x.GetString() ?? "").Where(x => !string.IsNullOrEmpty(x)).ToList();
                        }
                    }

                    var recommendations = new List<string>();
                    if (root.TryGetProperty("recommendations", out var recs))
                    {
                        recommendations = recs.EnumerateArray()
                            .Select(x => x.GetString() ?? "").Where(x => !string.IsNullOrEmpty(x)).ToList();
                    }

                    return new BodyScanResponse
                    {
                        Success = true,
                        BodyAnalysis = bodyAnalysis,
                        Recommendations = recommendations,
                        FullAnalysis = root.TryGetProperty("fullAnalysis", out var fullAnalysis) ? fullAnalysis.GetString() : ""
                    };
                }

                return new BodyScanResponse { Success = false, ErrorMessage = "Invalid JSON format" };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error parsing body JSON: {ex.Message}");
                return new BodyScanResponse { Success = false, ErrorMessage = "Failed to parse body data" };
            }
        }

        private VoiceWorkoutResponse ParseVoiceWorkoutJsonResponse(string jsonText)
        {
            try
            {
                _logger.LogInformation($"🎤 Parsing voice workout JSON: {jsonText.Substring(0, Math.Min(300, jsonText.Length))}...");

                var startIndex = jsonText.IndexOf('{');
                var lastIndex = jsonText.LastIndexOf('}');

                if (startIndex >= 0 && lastIndex > startIndex)
                {
                    var cleanJson = jsonText.Substring(startIndex, lastIndex - startIndex + 1);
                    _logger.LogInformation($"🎤 Extracted JSON: {cleanJson.Substring(0, Math.Min(200, cleanJson.Length))}...");

                    using var document = JsonDocument.Parse(cleanJson);
                    var root = document.RootElement;

                    var response = new VoiceWorkoutResponse
                    {
                        Success = true,
                        TranscribedText = SafeGetString(root, "transcribedText", "Не удалось распознать текст")
                    };

                    if (root.TryGetProperty("workoutData", out var workoutData))
                    {
                        response.WorkoutData = new WorkoutDataResponse
                        {
                            Type = SafeGetString(workoutData, "type", "strength"),
                            StartTime = SafeParseDateTime(workoutData, "startTime"),
                            EndTime = SafeParseDateTime(workoutData, "endTime"),
                            EstimatedCalories = SafeGetInt(workoutData, "estimatedCalories", 200)
                        };

                        if (workoutData.TryGetProperty("strengthData", out var strengthData) &&
                            strengthData.ValueKind != JsonValueKind.Null)
                        {
                            response.WorkoutData.StrengthData = ParseStrengthData(strengthData);
                        }

                        if (workoutData.TryGetProperty("cardioData", out var cardioData) &&
                            cardioData.ValueKind != JsonValueKind.Null)
                        {
                            response.WorkoutData.CardioData = ParseCardioData(cardioData);
                        }

                        response.WorkoutData.Notes = ParseNotes(workoutData);

                        if (response.WorkoutData.EndTime <= response.WorkoutData.StartTime)
                        {
                            response.WorkoutData.EndTime = response.WorkoutData.StartTime.AddMinutes(45);
                        }
                    }
                    else
                    {
                        response.WorkoutData = CreateDefaultWorkoutData(response.TranscribedText, "strength");
                    }

                    _logger.LogInformation($"✅ Voice workout parsed successfully: {response.WorkoutData?.Type}");
                    return response;
                }

                _logger.LogError($"❌ No valid JSON found in response: {jsonText.Substring(0, Math.Min(200, jsonText.Length))}");
                return CreateFallbackResponse(jsonText);
            }
            catch (JsonException ex)
            {
                _logger.LogError($"❌ JSON parsing error: {ex.Message}");
                _logger.LogError($"Problematic JSON: {jsonText.Substring(0, Math.Min(500, jsonText.Length))}");
                return CreateFallbackResponse(jsonText);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Unexpected error parsing voice workout JSON: {ex.Message}");
                return CreateFallbackResponse(jsonText);
            }
        }

        private StrengthDataDto ParseStrengthData(JsonElement strengthData)
        {
            var strengthDto = new StrengthDataDto
            {
                Name = SafeGetString(strengthData, "name", "Упражнение"),
                MuscleGroup = SafeGetString(strengthData, "muscleGroup", "Не указано"),
                Equipment = SafeGetString(strengthData, "equipment", "Не указано"),
                WorkingWeight = SafeGetDecimal(strengthData, "workingWeight", 0),
                RestTimeSeconds = SafeGetInt(strengthData, "restTimeSeconds", 120)
            };

            if (strengthData.TryGetProperty("sets", out var setsArray) &&
                setsArray.ValueKind == JsonValueKind.Array)
            {
                var sets = new List<StrengthSetDto>();
                foreach (var setElement in setsArray.EnumerateArray())
                {
                    sets.Add(new StrengthSetDto
                    {
                        SetNumber = SafeGetInt(setElement, "setNumber", sets.Count + 1),
                        Weight = SafeGetDecimal(setElement, "weight", strengthDto.WorkingWeight),
                        Reps = SafeGetInt(setElement, "reps", 10),
                        IsCompleted = SafeGetBool(setElement, "isCompleted", true),
                        Notes = SafeGetString(setElement, "notes", "")
                    });
                }
                strengthDto.Sets = sets;
            }
            else
            {
                strengthDto.Sets = new List<StrengthSetDto>
                {
                    new StrengthSetDto
                    {
                        SetNumber = 1,
                        Weight = strengthDto.WorkingWeight,
                        Reps = 10,
                        IsCompleted = true,
                        Notes = "Подход из голосового ввода"
                    }
                };
            }

            return strengthDto;
        }

        private CardioDataDto ParseCardioData(JsonElement cardioData)
        {
            return new CardioDataDto
            {
                CardioType = SafeGetString(cardioData, "cardioType", "Кардио"),
                DistanceKm = SafeGetNullableDecimal(cardioData, "distanceKm"),
                AvgPulse = SafeGetNullableInt(cardioData, "avgPulse"),
                MaxPulse = SafeGetNullableInt(cardioData, "maxPulse"),
                AvgPace = SafeGetString(cardioData, "avgPace", "")
            };
        }

        private List<string> ParseNotes(JsonElement workoutData)
        {
            if (workoutData.TryGetProperty("notes", out var notes) &&
                notes.ValueKind == JsonValueKind.Array)
            {
                return notes.EnumerateArray()
                    .Select(x => x.GetString() ?? "")
                    .Where(x => !string.IsNullOrEmpty(x))
                    .ToList();
            }

            return new List<string> { "Тренировка добавлена голосом" };
        }

        private VoiceWorkoutResponse CreateFallbackResponse(string originalText)
        {
            return new VoiceWorkoutResponse
            {
                Success = true,
                ErrorMessage = null,
                TranscribedText = string.IsNullOrEmpty(originalText) ? "Не удалось распознать речь" : originalText,
                WorkoutData = CreateDefaultWorkoutData(originalText, "strength")
            };
        }

        private VoiceFoodResponse ParseVoiceFoodJsonResponse(string jsonText)
        {
            try
            {
                var startIndex = jsonText.IndexOf('{');
                var lastIndex = jsonText.LastIndexOf('}');

                if (startIndex >= 0 && lastIndex > startIndex)
                {
                    var cleanJson = jsonText.Substring(startIndex, lastIndex - startIndex + 1);

                    using var document = JsonDocument.Parse(cleanJson);
                    var root = document.RootElement;

                    var foodItems = new List<FoodItemResponse>();

                    if (root.TryGetProperty("foodItems", out var foodItemsArray))
                    {
                        foreach (var item in foodItemsArray.EnumerateArray())
                        {
                            var foodItem = new FoodItemResponse
                            {
                                Name = item.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                                EstimatedWeight = item.TryGetProperty("estimatedWeight", out var weight) ? weight.GetDecimal() : 0,
                                WeightType = item.TryGetProperty("weightType", out var weightType) ? weightType.GetString() ?? "g" : "g",
                                Description = item.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
                                Confidence = item.TryGetProperty("confidence", out var conf) ? conf.GetDecimal() : 0.8m
                            };

                            if (item.TryGetProperty("nutritionPer100g", out var nutrition))
                            {
                                foodItem.NutritionPer100g = new NutritionPer100gDto
                                {
                                    Calories = nutrition.TryGetProperty("calories", out var cal) ? cal.GetDecimal() : 0,
                                    Proteins = nutrition.TryGetProperty("proteins", out var prot) ? prot.GetDecimal() : 0,
                                    Fats = nutrition.TryGetProperty("fats", out var fats) ? fats.GetDecimal() : 0,
                                    Carbs = nutrition.TryGetProperty("carbs", out var carbs) ? carbs.GetDecimal() : 0
                                };
                            }

                            foodItem.TotalCalories = item.TryGetProperty("totalCalories", out var itemTotalCal) ? itemTotalCal.GetInt32() : 0;
                            foodItems.Add(foodItem);
                        }
                    }

                    return new VoiceFoodResponse
                    {
                        Success = true,
                        TranscribedText = root.TryGetProperty("transcribedText", out var transcribed) ? transcribed.GetString() : "",
                        FoodItems = foodItems,
                        EstimatedTotalCalories = root.TryGetProperty("estimatedTotalCalories", out var totalCalories) ? totalCalories.GetInt32() : 0
                    };
                }

                return new VoiceFoodResponse { Success = false, ErrorMessage = "Invalid JSON format" };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error parsing voice food JSON: {ex.Message}");
                return new VoiceFoodResponse { Success = false, ErrorMessage = "Failed to parse food data" };
            }
        }

        private string SafeGetString(JsonElement element, string propertyName, string defaultValue = "")
        {
            try
            {
                if (element.TryGetProperty(propertyName, out var prop))
                {
                    if (prop.ValueKind == JsonValueKind.String)
                        return prop.GetString() ?? defaultValue;
                    if (prop.ValueKind == JsonValueKind.Number)
                        return prop.ToString();
                }
                return defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        private int SafeGetInt(JsonElement element, string propertyName, int defaultValue = 0)
        {
            try
            {
                if (element.TryGetProperty(propertyName, out var prop))
                {
                    if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var intValue))
                        return intValue;
                    if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var parsedInt))
                        return parsedInt;
                }
                return defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        private decimal SafeGetDecimal(JsonElement element, string propertyName, decimal defaultValue = 0)
        {
            try
            {
                if (element.TryGetProperty(propertyName, out var prop))
                {
                    if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var decimalValue))
                        return decimalValue;
                    if (prop.ValueKind == JsonValueKind.String && decimal.TryParse(prop.GetString(), out var parsedDecimal))
                        return parsedDecimal;
                }
                return defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }
        private bool SafeGetBool(JsonElement element, string propertyName, bool defaultValue = false)
        {
            return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.True
                ? prop.GetBoolean()
                : defaultValue;
        }

        private int? SafeGetNullableInt(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var intValue))
                    return intValue;
                if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var parsedInt))
                    return parsedInt;
            }
            return null;
        }

        private decimal? SafeGetNullableDecimal(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var decimalValue))
                    return decimalValue;
                if (prop.ValueKind == JsonValueKind.String && decimal.TryParse(prop.GetString(), out var parsedDecimal))
                    return parsedDecimal;
            }
            return null;
        }

        private DateTime SafeParseDateTime(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                var dateString = prop.GetString();
                if (!string.IsNullOrEmpty(dateString))
                {
                    if (DateTime.TryParse(dateString, out var parsedDate))
                        return parsedDate;

                    if (TimeSpan.TryParse(dateString, out var timeSpan))
                    {
                        return DateTime.UtcNow.Date.Add(timeSpan);
                    }
                }
            }

            return DateTime.UtcNow; 
        }

    }
}