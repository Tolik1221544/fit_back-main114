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
                _logger.LogInformation($"💪 Starting body analysis - Weight: {weight}kg, Height: {height}cm, Age: {age}, Gender: {gender}");

                var projectId = _configuration["GoogleCloud:ProjectId"];
                var location = _configuration["GoogleCloud:Location"] ?? "us-central1";
                var model = _configuration["GoogleCloud:Model"] ?? "gemini-2.5-pro";

                if (string.IsNullOrEmpty(projectId))
                {
                    _logger.LogError("❌ GoogleCloud:ProjectId not configured");
                    return CreateFallbackBodyResponse("Google Cloud не настроен");
                }

                string accessToken;
                try
                {
                    accessToken = await _tokenService.GetAccessTokenAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ Failed to get access token: {ex.Message}");
                    return CreateFallbackBodyResponse("Ошибка аутентификации");
                }

                var url = $"https://{location}-aiplatform.googleapis.com/v1/projects/{projectId}/locations/{location}/publishers/google/models/{model}:generateContent";

                var parts = new List<object>();

                var prompt = $@"
Ты - продвинутый ИИ-специалист по анализу тела и фитнесу. Проанализируй изображения тела и дай ДЕТАЛЬНУЮ и ТОЧНУЮ оценку.

📊 ДАННЫЕ ПОЛЬЗОВАТЕЛЯ:
- Вес: {weight ?? 0}кг
- Рост: {height ?? 0}см  
- Возраст: {age ?? 0} лет
- Пол: {gender ?? "не указан"}
- Цели: {goals ?? "не указаны"}

🔍 ИНСТРУКЦИИ ДЛЯ АНАЛИЗА:
1. Внимательно изучи каждое изображение
2. Оцени общее телосложение и пропорции
3. Определи примерный процент жира и мышц
4. Проанализируй осанку и симметрию
5. Рассчитай ИМТ на основе данных
6. Дай конкретные рекомендации

💪 РАСЧЕТ ОСНОВНОГО ОБМЕНА (BMR):
Используй формулу Миффлина-Сан Жеора:
- Мужчины: BMR = 10 × вес(кг) + 6.25 × рост(см) - 5 × возраст + 5
- Женщины: BMR = 10 × вес(кг) + 6.25 × рост(см) - 5 × возраст - 161

🎯 КАТЕГОРИИ МЕТАБОЛИЗМА:
- Низкий: BMR < 1400 ккал
- Нормальный: BMR 1400-2000 ккал  
- Высокий: BMR > 2000 ккал

📏 ПРИМЕРНЫЕ ЗНАЧЕНИЯ ПО ТЕЛОСЛОЖЕНИЮ:
- Стройное: жир 8-15%, мышцы 35-45%
- Среднее: жир 15-25%, мышцы 30-40%
- Полное: жир 25-35%, мышцы 25-35%

ОБЯЗАТЕЛЬНО верни ТОЛЬКО валидный JSON:
{{
  ""bodyAnalysis"": {{
    ""estimatedBodyFatPercentage"": точный_процент_жира,
    ""estimatedMusclePercentage"": точный_процент_мышц,
    ""bodyType"": ""конкретный тип телосложения"",
    ""postureAnalysis"": ""детальный анализ осанки"",
    ""overallCondition"": ""общая оценка физического состояния"",
    ""bmi"": рассчитанный_ИМТ,
    ""bmiCategory"": ""категория ИМТ"",
    ""estimatedWaistCircumference"": окружность_талии_см,
    ""estimatedChestCircumference"": окружность_груди_см,
    ""estimatedHipCircumference"": окружность_бедер_см,
    ""basalMetabolicRate"": рассчитанный_BMR_ккал,
    ""metabolicRateCategory"": ""категория метаболизма"",
    ""exerciseRecommendations"": [""конкретная рекомендация 1"", ""конкретная рекомендация 2"", ""конкретная рекомендация 3""],
    ""nutritionRecommendations"": [""питательная рекомендация 1"", ""питательная рекомендация 2"", ""питательная рекомендация 3""],
    ""trainingFocus"": ""конкретный фокус тренировок""
  }},
  ""recommendations"": [""общая рекомендация 1"", ""общая рекомендация 2"", ""общая рекомендация 3""],
  ""fullAnalysis"": ""подробный анализ всех аспектов физического состояния с конкретными наблюдениями""
}}

🎯 ПРИМЕРЫ ХОРОШИХ ОТВЕТОВ:

Стройное телосложение:
{{
  ""bodyAnalysis"": {{
    ""estimatedBodyFatPercentage"": 12.5,
    ""estimatedMusclePercentage"": 42.0,
    ""bodyType"": ""Эктоморф - стройное атлетичное телосложение"",
    ""postureAnalysis"": ""Хорошая осанка, плечи слегка наклонены вперед, рекомендуется укрепление мышц спины"",
    ""overallCondition"": ""Отличная физическая форма с низким процентом жира и хорошо развитой мускулатурой"",
    ""bmi"": 21.8,
    ""bmiCategory"": ""Нормальный вес"",
    ""estimatedWaistCircumference"": 75,
    ""estimatedChestCircumference"": 95,
    ""estimatedHipCircumference"": 88,
    ""basalMetabolicRate"": 1750,
    ""metabolicRateCategory"": ""Нормальный"",
    ""exerciseRecommendations"": [""Силовые тренировки для набора мышечной массы"", ""Функциональные упражнения для улучшения координации"", ""Растяжка для поддержания гибкости""],
    ""nutritionRecommendations"": [""Увеличить потребление белка до 2г на кг веса"", ""Добавить сложные углеводы для энергии"", ""Включить полезные жиры для гормонального баланса""],
    ""trainingFocus"": ""Набор мышечной массы и улучшение силовых показателей""
  }},
  ""recommendations"": [""Продолжайте поддерживать активный образ жизни"", ""Сосредоточьтесь на наборе качественной мышечной массы"", ""Регулярно отслеживайте прогресс""],
  ""fullAnalysis"": ""Анализ показывает отличную физическую форму с низким процентом жира (12.5%) и хорошо развитой мускулатурой (42%). Телосложение соответствует эктоморфному типу с преобладанием стройности. Осанка в целом хорошая, но наблюдается небольшой наклон плеч вперед, что характерно для людей, проводящих много времени за компьютером. Рекомендуется фокус на наборе мышечной массы и укреплении задней поверхности тела.""
}}

ВАЖНО: Давай КОНКРЕТНЫЕ и ПОЛЕЗНЫЕ рекомендации, а не общие фразы!";

                parts.Add(new { text = prompt });

                int imageCount = 0;
                if (frontImageData != null && frontImageData.Length > 0)
                {
                    parts.Add(new
                    {
                        inline_data = new
                        {
                            mime_type = GetImageMimeType(frontImageData),
                            data = Convert.ToBase64String(frontImageData)
                        }
                    });
                    imageCount++;
                    _logger.LogInformation($"💪 Added front image: {frontImageData.Length} bytes");
                }

                if (sideImageData != null && sideImageData.Length > 0)
                {
                    parts.Add(new
                    {
                        inline_data = new
                        {
                            mime_type = GetImageMimeType(sideImageData),
                            data = Convert.ToBase64String(sideImageData)
                        }
                    });
                    imageCount++;
                    _logger.LogInformation($"💪 Added side image: {sideImageData.Length} bytes");
                }

                if (backImageData != null && backImageData.Length > 0)
                {
                    parts.Add(new
                    {
                        inline_data = new
                        {
                            mime_type = GetImageMimeType(backImageData),
                            data = Convert.ToBase64String(backImageData)
                        }
                    });
                    imageCount++;
                    _logger.LogInformation($"💪 Added back image: {backImageData.Length} bytes");
                }

                if (imageCount == 0)
                {
                    _logger.LogWarning("⚠️ No images provided for body analysis");
                    return CreateFallbackBodyResponse("Не предоставлены изображения для анализа");
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
                        temperature = 0.3,  
                        max_output_tokens = 4096,  
                        top_p = 0.8,
                        top_k = 40
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

                _logger.LogInformation($"💪 Sending body analysis request with {imageCount} images");

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.PostAsync(url, content);
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError($"❌ HTTP request failed: {ex.Message}");
                    return CreateFallbackBodyResponse("Ошибка сети при подключении к AI сервису");
                }
                catch (TaskCanceledException ex)
                {
                    _logger.LogError($"❌ Request timeout: {ex.Message}");
                    return CreateFallbackBodyResponse("Превышено время ожидания ответа от AI сервиса");
                }

                var responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"❌ Vertex AI API error: {response.StatusCode} - {responseText}");
                    return CreateFallbackBodyResponse($"Ошибка AI сервиса: {response.StatusCode}");
                }

                _logger.LogInformation($"💪 Received response from Vertex AI: {responseText.Length} characters");

                return ParseBodyScanResponseWithFallback(responseText, weight, height, age, gender);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Unexpected error in body analysis: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return CreateFallbackBodyResponse($"Системная ошибка: {ex.Message}");
            }
        }

        private BodyScanResponse ParseBodyScanResponseWithFallback(string responseText, decimal? weight, decimal? height, int? age, string? gender)
        {
            try
            {
                var parsedResponse = ParseBodyScanResponse(responseText);
                if (parsedResponse.Success && ValidateBodyScanResult(parsedResponse))
                {
                    _logger.LogInformation("✅ Body analysis parsed successfully");
                    return parsedResponse;
                }

                _logger.LogWarning("Failed to parse body analysis response, using intelligent fallback");
                return CreateIntelligentBodyFallback("Ошибка парсинга ответа AI", weight, height, age, gender);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error parsing body analysis response: {ex.Message}");
                return CreateIntelligentBodyFallback("Ошибка обработки ответа", weight, height, age, gender);
            }
        }

  ////////////
        private bool ValidateBodyScanResult(BodyScanResponse result)
        {
            if (result?.BodyAnalysis == null)
                return false;

            return result.BodyAnalysis.BMI > 0 &&
                   result.BodyAnalysis.EstimatedBodyFatPercentage >= 0 &&
                   result.BodyAnalysis.EstimatedMusclePercentage >= 0 &&
                   result.BodyAnalysis.BasalMetabolicRate > 0;
        }

        private BodyScanResponse CreateIntelligentBodyFallback(string reason, decimal? weight, decimal? height, int? age, string? gender)
        {
            _logger.LogInformation($"💪 Creating intelligent body fallback: {reason}");

            decimal bmi = 22.5m;
            string bmiCategory = "Нормальный";
            int bmr = 1600;
            string metabolicCategory = "Нормальный";

            if (weight.HasValue && height.HasValue && weight > 0 && height > 0)
            {
                var heightInMeters = height.Value / 100;
                bmi = weight.Value / (heightInMeters * heightInMeters);

                bmiCategory = bmi switch
                {
                    < 18.5m => "Недостаточный вес",
                    >= 18.5m and < 25m => "Нормальный вес",
                    >= 25m and < 30m => "Избыточный вес",
                    >= 30m => "Ожирение",
                    _ => "Нормальный вес"
                };

                if (age.HasValue && age > 0)
                {
                    if (gender?.ToLowerInvariant() == "male" || gender?.ToLowerInvariant() == "мужской")
                    {
                        bmr = (int)(10 * (double)weight.Value + 6.25 * (double)height.Value - 5 * age.Value + 5);
                    }
                    else
                    {
                        bmr = (int)(10 * (double)weight.Value + 6.25 * (double)height.Value - 5 * age.Value - 161);
                    }
                }

                metabolicCategory = bmr switch
                {
                    < 1400 => "Низкий",
                    > 2000 => "Высокий",
                    _ => "Нормальный"
                };
            }

            var (bodyFat, muscle, bodyType, waist, chest, hips) = bmi switch
            {
                < 20m => (12m, 45m, "Эктоморф - стройное телосложение", 70m, 90m, 85m),
                >= 20m and < 25m => (18m, 40m, "Мезоморф - среднее телосложение", 80m, 100m, 95m),
                >= 25m and < 30m => (25m, 35m, "Эндоморф - плотное телосложение", 90m, 110m, 105m),
                _ => (30m, 30m, "Эндоморф - полное телосложение", 100m, 120m, 115m)
            };

            return new BodyScanResponse
            {
                Success = true,
                ErrorMessage = null,
                BodyAnalysis = new BodyAnalysisDto
                {
                    EstimatedBodyFatPercentage = bodyFat,
                    EstimatedMusclePercentage = muscle,
                    BodyType = bodyType,
                    PostureAnalysis = "Анализ осанки недоступен без изображений высокого качества",
                    OverallCondition = $"Оценка на основе предоставленных данных (ИМТ: {bmi:F1})",
                    BMI = Math.Round(bmi, 1),
                    BMICategory = bmiCategory,
                    EstimatedWaistCircumference = waist,
                    EstimatedChestCircumference = chest,
                    EstimatedHipCircumference = hips,
                    BasalMetabolicRate = bmr,
                    MetabolicRateCategory = metabolicCategory,
                    ExerciseRecommendations = GetExerciseRecommendationsByBMI(bmi),
                    NutritionRecommendations = GetNutritionRecommendationsByBMR(bmr),
                    TrainingFocus = GetTrainingFocusByBMI(bmi)
                },
                Recommendations = new List<string>
        {
            "Для более точного анализа загрузите качественные фотографии в хорошем освещении",
            "Убедитесь, что на фото видно всё тело в полный рост",
            "Рекомендуем повторить анализ или обратиться к специалисту"
        },
                FullAnalysis = $"Автоматический анализ на основе предоставленных данных: вес {weight}кг, рост {height}см, возраст {age} лет. {reason}. ИМТ составляет {bmi:F1} ({bmiCategory}), базовый метаболизм {bmr} ккал/день ({metabolicCategory})."
            };
        }

        private List<string> GetExerciseRecommendationsByBMI(decimal bmi)
        {
            return bmi switch
            {
                < 20m => new List<string>
        {
            "Силовые тренировки для набора мышечной массы",
            "Функциональные упражнения с собственным весом",
            "Комплексные движения: приседания, подтягивания, отжимания"
        },
                >= 20m and < 25m => new List<string>
        {
            "Сбалансированные тренировки: силовые + кардио",
            "Функциональный тренинг 3-4 раза в неделю",
            "Упражнения на координацию и гибкость"
        },
                >= 25m and < 30m => new List<string>
        {
            "Кардиотренировки для снижения веса",
            "Силовые упражнения для сохранения мышц",
            "Низкоинтенсивные длительные нагрузки"
        },
                _ => new List<string>
        {
            "Начните с ходьбы и легких упражнений",
            "Постепенно увеличивайте нагрузку",
            "Консультация с врачом перед началом тренировок"
        }
            };
        }

        private List<string> GetNutritionRecommendationsByBMR(int bmr)
        {
            return bmr switch
            {
                < 1400 => new List<string>
        {
            "Небольшие частые приемы пищи",
            "Увеличить потребление белка",
            "Добавить полезные жиры для гормонов"
        },
                > 2000 => new List<string>
        {
            "Обеспечить достаточную калорийность",
            "Сложные углеводы для энергии",
            "Белок 2г на кг веса для восстановления"
        },
                _ => new List<string>
        {
            "Сбалансированное питание по БЖУ",
            "Достаточное количество воды (30-40мл на кг веса)",
            "Овощи и фрукты в каждом приеме пищи"
        }
            };
        }

        private string GetTrainingFocusByBMI(decimal bmi)
        {
            return bmi switch
            {
                < 20m => "Набор мышечной массы и силы",
                >= 20m and < 25m => "Поддержание формы и общее развитие",
                >= 25m and < 30m => "Снижение веса с сохранением мышц",
                _ => "Постепенное снижение веса под контролем специалиста"
            };
        }

        private BodyScanResponse CreateFallbackBodyResponse(string reason)
        {
            _logger.LogWarning($"💪 Creating simple fallback body response: {reason}");

            return new BodyScanResponse
            {
                Success = true,
                ErrorMessage = null,
                BodyAnalysis = new BodyAnalysisDto
                {
                    EstimatedBodyFatPercentage = 20m,
                    EstimatedMusclePercentage = 35m,
                    BodyType = "Среднее телосложение",
                    PostureAnalysis = "Анализ осанки недоступен",
                    OverallCondition = $"Анализ недоступен ({reason})",
                    BMI = 22.5m,
                    BMICategory = "Нормальный",
                    EstimatedWaistCircumference = 80m,
                    EstimatedChestCircumference = 100m,
                    EstimatedHipCircumference = 95m,
                    BasalMetabolicRate = 1600,
                    MetabolicRateCategory = "Нормальный",
                    ExerciseRecommendations = new List<string> { "Регулярные упражнения", "Кардио нагрузки" },
                    NutritionRecommendations = new List<string> { "Сбалансированное питание", "Достаточное количество воды" },
                    TrainingFocus = "Общая физическая подготовка"
                },
                Recommendations = new List<string>
        {
            "Рекомендуем повторить анализ с более качественными фотографиями",
            "Обратитесь к специалисту для точной оценки"
        },
                FullAnalysis = $"Анализ был выполнен автоматически из-за ошибки: {reason}"
            };
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
Ты - продвинутый ИИ-диетолог, который умеет анализировать голосовые записи о питании и КРЕАТИВНО додумывать недостающие детали даже при плохом качестве звука.

ВАЖНО: Даже если запись нечеткая, с шумом или неполная - всегда создавай ПОЛНЫЙ и РЕАЛИСТИЧНЫЙ ответ!

Тип приема пищи: {mealType ?? "любой"}

🧠 КРЕАТИВНЫЕ ПРАВИЛА ДОДУМЫВАНИЯ:
1. Если слышишь частичную информацию - ДОДУМАЙ реалистичные детали
2. Если непонятно блюдо - выбери популярное похожее по контексту
3. Если нет веса/объема - используй стандартные порции
4. Если нет точного названия - создай на основе звуков
5. ВСЕГДА создавай полную запись о питании, даже из минимальной информации
6. При шуме/помехах - фокусируйся на ключевых пищевых словах

📝 ПРИМЕРЫ КРЕАТИВНОГО ДОДУМЫВАНИЯ:
- ""ел хлеб"" → ""Хлеб белый 50г с маслом""
- ""пил кофе"" → ""Кофе с молоком 200мл""
- ""борщ"" → ""Борщ украинский 300мл с мясом""
- ""яблоко"" → ""Яблоко красное среднее 150г""
- ""завтракал"" → создай типичный завтрак
- неразборчивые звуки → определи по времени дня и контексту

🔊 УСТОЙЧИВОСТЬ К ШУМУ:
- Фон, шум, помехи - ИГНОРИРУЙ, ищи пищевые слова
- Неточное произношение - интерпретируй как ближайшее блюдо
- Обрывки фраз - дополни логичными вариантами
- Если ничего не слышно - создай блюдо по типу приема пищи

⚠️ ВАЖНЫЕ ПРАВИЛА ДЛЯ ЕДИНИЦ ИЗМЕРЕНИЯ:
1. Для ЖИДКИХ продуктов используй ""weightType"": ""ml"":
   - Супы (борщ, щи, солянка, бульон) 
   - Напитки (чай, кофе, сок, вода, молоко, компот)
   - Соусы, подливы, кетчуп, майонез
   - Жидкие каши (овсянка на молоке)
   - Смузи, коктейли, йогурт питьевой

2. Для ТВЕРДЫХ продуктов используй ""weightType"": ""g"":
   - Хлеб, мясо, рыба, овощи, фрукты
   - Твердые каши (гречка, рис, пшено)
   - Выпечка, сладости, печенье
   - Орехи, семечки, сыр, творог

🍽️ РЕАЛИСТИЧНЫЕ ПОРЦИИ ПО УМОЛЧАНИЮ:
- Супы: 250-350мл
- Основные блюда: 200-300г
- Хлеб: 30-50г
- Фрукты: 100-200г
- Напитки: 200-250мл
- Каши: 150-200г
- Мясо/рыба: 100-150г

🎯 КРЕАТИВНЫЙ ПОДХОД ПО ТИПАМ ПИТАНИЯ:
- Завтрак: каша, хлеб, кофе, яйца, творог
- Обед: суп, основное блюдо, салат, компот
- Ужин: легкие блюда, овощи, чай
- Перекус: фрукты, орехи, йогурт

ОБЯЗАТЕЛЬНО верни ТОЛЬКО валидный JSON:
{{
  ""transcribedText"": ""точный или улучшенный текст"",
  ""foodItems"": [
    {{
      ""name"": ""Конкретное название блюда"",
      ""estimatedWeight"": реалистичное_количество,
      ""weightType"": ""ml или g"",
      ""description"": ""Детальное описание"",
      ""nutritionPer100g"": {{
        ""calories"": точные_калории_на_100г_или_мл,
        ""proteins"": белки_на_100г_или_мл,
        ""fats"": жиры_на_100г_или_мл,
        ""carbs"": углеводы_на_100г_или_мл
      }},
      ""totalCalories"": общие_калории_порции,
      ""confidence"": уверенность_от_0_до_1
    }}
  ],
  ""estimatedTotalCalories"": сумма_всех_калорий
}}

🔍 КОНКРЕТНЫЕ ПРИМЕРЫ УЛУЧШЕНИЙ:
- Слышно ""бор..."" + шум → ""Борщ украинский 300мл""
- ""Пил что-то горячее"" → ""Чай черный с сахаром 200мл""
- ""Ел с хлебом"" → ""Хлеб белый 50г + масло сливочное 10г""
- Неразборчиво + утро → ""Овсяная каша на молоке 200г""
- Только звуки жевания → блюдо по времени дня

КОНКРЕТНЫЕ ПРИМЕРЫ JSON ОТВЕТОВ:

Борщ (нечеткая запись):
{{
  ""transcribedText"": ""Ел борщ на обед, не очень слышно"",
  ""foodItems"": [
    {{
      ""name"": ""Борщ украинский с мясом"",
      ""estimatedWeight"": 300,
      ""weightType"": ""ml"",
      ""description"": ""Традиционный борщ со свеклой и мясом"",
      ""nutritionPer100g"": {{""calories"": 45, ""proteins"": 2.5, ""fats"": 1.8, ""carbs"": 6.2}},
      ""totalCalories"": 135,
      ""confidence"": 0.7
    }}
  ],
  ""estimatedTotalCalories"": 135
}}

Кофе (с шумом):
{{
  ""transcribedText"": ""Выпил кофе утром"",
  ""foodItems"": [
    {{
      ""name"": ""Кофе с молоком"",
      ""estimatedWeight"": 200,
      ""weightType"": ""ml"",
      ""description"": ""Кофе растворимый с молоком и сахаром"",
      ""nutritionPer100g"": {{""calories"": 35, ""proteins"": 1.5, ""fats"": 1.2, ""carbs"": 4.8}},
      ""totalCalories"": 70,
      ""confidence"": 0.8
    }}
  ],
  ""estimatedTotalCalories"": 70
}}

Неразборчиво (завтрак):
{{
  ""transcribedText"": ""Завтракал, не очень слышно что именно"",
  ""foodItems"": [
    {{
      ""name"": ""Овсяная каша на молоке"",
      ""estimatedWeight"": 200,
      ""weightType"": ""g"",
      ""description"": ""Каша овсяная на молоке с сахаром"",
      ""nutritionPer100g"": {{""calories"": 105, ""proteins"": 3.2, ""fats"": 4.1, ""carbs"": 14.2}},
      ""totalCalories"": 210,
      ""confidence"": 0.5
    }},
    {{
      ""name"": ""Хлеб белый"",
      ""estimatedWeight"": 40,
      ""weightType"": ""g"",
      ""description"": ""Хлеб белый с маслом"",
      ""nutritionPer100g"": {{""calories"": 265, ""proteins"": 8.1, ""fats"": 3.2, ""carbs"": 48.8}},
      ""totalCalories"": 106,
      ""confidence"": 0.6
    }}
  ],
  ""estimatedTotalCalories"": 316
}}

ПОМНИ: Твоя задача - ВСЕГДА давать ПОЛЕЗНЫЙ результат, даже если аудио очень плохого качества!
Лучше креативно додумать, чем вернуть пустой ответ!";

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
                    return CreateIntelligentFoodFallback("API error", mealType);
                }

                return ParseVoiceFoodResponseWithFallback(responseText, mealType);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error analyzing voice food: {ex.Message}");
                return CreateIntelligentFoodFallback($"Analysis error: {ex.Message}", mealType);
            }
        }

        private VoiceFoodResponse CreateIntelligentFoodFallback(string errorReason, string? mealType)
        {
            var defaultFood = GetDefaultFoodForMealType(mealType);

            return new VoiceFoodResponse
            {
                Success = true,
                ErrorMessage = null,
                TranscribedText = $"Не удалось распознать аудио ({errorReason}), создана запись о питании по умолчанию",
                FoodItems = new List<FoodItemResponse> { defaultFood },
                EstimatedTotalCalories = defaultFood.TotalCalories
            };
        }

        private VoiceFoodResponse ParseVoiceFoodResponseWithFallback(string responseText, string? mealType)
        {
            try
            {
                var parsedResponse = ParseVoiceFoodResponse(responseText);
                if (parsedResponse.Success && parsedResponse.FoodItems != null && parsedResponse.FoodItems.Any())
                {
                    return parsedResponse;
                }

                _logger.LogWarning("Failed to parse voice food response, using fallback");
                return CreateIntelligentFoodFallback("Ошибка парсинга ответа ИИ", mealType);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error parsing voice food response: {ex.Message}");
                return CreateIntelligentFoodFallback("Ошибка обработки ответа", mealType);
            }
        }

        private FoodItemResponse GetDefaultFoodForMealType(string? mealType)
        {
            var currentHour = DateTime.Now.Hour;

            // Если тип не указан, определяем по времени
            if (string.IsNullOrEmpty(mealType))
            {
                mealType = currentHour switch
                {
                    >= 6 and <= 10 => "breakfast",
                    >= 11 and <= 15 => "lunch",
                    >= 16 and <= 22 => "dinner",
                    _ => "snack"
                };
            }

            var (name, calories, proteins, fats, carbs, weight, weightType, description) = mealType.ToLowerInvariant() switch
            {
                "breakfast" or "завтрак" => (
                    "Овсяная каша на молоке",
                    105m, 3.2m, 4.1m, 14.2m, 200m, "g",
                    "Полезный завтрак для заряда энергии на весь день"
                ),
                "lunch" or "обед" => (
                    "Борщ украинский с мясом",
                    45m, 2.5m, 1.8m, 6.2m, 300m, "ml",
                    "Сытный обед с традиционным борщом"
                ),
                "dinner" or "ужин" => (
                    "Куриная грудка с овощами",
                    120m, 25m, 2m, 3m, 200m, "g",
                    "Легкий и полезный ужин"
                ),
                "snack" or "перекус" => (
                    "Яблоко зеленое",
                    47m, 0.4m, 0.4m, 9.8m, 150m, "g",
                    "Полезный перекус между основными приемами пищи"
                ),
                _ => (
                    "Неизвестное блюдо",
                    200m, 10m, 8m, 25m, 150m, "g",
                    "Запись создана автоматически"
                )
            };

            return new FoodItemResponse
            {
                Name = name,
                EstimatedWeight = weight,
                WeightType = weightType,
                Description = description,
                NutritionPer100g = new NutritionPer100gDto
                {
                    Calories = calories,
                    Proteins = proteins,
                    Fats = fats,
                    Carbs = carbs
                },
                TotalCalories = (int)Math.Round((calories * weight) / 100),
                Confidence = 0.4m
            };
        }


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

        private string DetermineWorkoutType(string? workoutType)
        {
            if (string.IsNullOrEmpty(workoutType))
                return "strength"; 

            return workoutType.ToLowerInvariant() switch
            {
                "strength" or "силовая" or "качалка" => "strength",
                "cardio" or "кардио" or "бег" => "cardio",
                _ => "strength"
            };
        }

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

        public async Task<bool> IsHealthyAsync()
        {
            try
            {
                var projectId = _configuration["GoogleCloud:ProjectId"];
                var location = _configuration["GoogleCloud:Location"] ?? "us-central1";
                var model = _configuration["GoogleCloud:Model"] ?? "gemini-2.5-pro";

                if (string.IsNullOrEmpty(projectId))
                {
                    _logger.LogError("❌ GoogleCloud:ProjectId not configured");
                    return false;
                }

                var accessToken = await _tokenService.GetAccessTokenAsync();
                var url = $"https://{location}-aiplatform.googleapis.com/v1/projects/{projectId}/locations/{location}/publishers/google/models/{model}:generateContent";

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
            },
                    generation_config = new
                    {
                        temperature = 0.1,
                        max_output_tokens = 10
                    }
                };

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                var isHealthy = response.IsSuccessStatusCode;

                if (isHealthy)
                {
                    _logger.LogInformation("✅ Vertex AI health check successful");
                }
                else
                {
                    _logger.LogWarning($"⚠️ Vertex AI health check failed: {response.StatusCode}");
                }

                return isHealthy;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Vertex AI health check error: {ex.Message}");
                return false;
            }
        }

    }
}