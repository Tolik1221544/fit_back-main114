using FitnessTracker.API.DTOs;
using FitnessTracker.API.Services.AI;
using System.Text;
using System.Text.Json;

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
Проанализируй изображение еды и предоставь детальную информацию в JSON формате.

{userPrompt ?? ""}

ВАЖНЫЕ ПРАВИЛА ДЛЯ ЕДИНИЦ ИЗМЕРЕНИЯ:
1. Для ЖИДКИХ продуктов используй ""weightType"": ""ml"":
   - Супы, бульоны, борщ, щи
   - Напитки (чай, кофе, сок, компот)
   - Соусы, подливы, жидкие каши

2. Для ТВЕРДЫХ продуктов используй ""weightType"": ""g"":
   - Хлеб, мясо, рыба, овощи, фрукты
   - Каши, гарниры, выпечка, салаты

3. Определяй тип по консистенции продукта на изображении

Верни ТОЛЬКО JSON без дополнительного текста:
{{
  ""foodItems"": [
    {{
      ""name"": ""название блюда"",
      ""estimatedWeight"": количество_в_правильных_единицах,
      ""weightType"": ""ml или g"",
      ""description"": ""описание блюда"",
      ""nutritionPer100g"": {{
        ""calories"": калории_на_100г_или_100мл,
        ""proteins"": белки_на_100г_или_100мл,
        ""fats"": жиры_на_100г_или_100мл,
        ""carbs"": углеводы_на_100г_или_100мл
      }},
      ""totalCalories"": общие_калории,
      ""confidence"": уверенность_от_0_до_1
    }}
  ],
  ""estimatedCalories"": общие_калории_всех_блюд,
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
                    return new FoodScanResponse { Success = false, ErrorMessage = $"API error: {response.StatusCode}" };
                }

                return ParseFoodScanResponse(responseText);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error analyzing food image: {ex.Message}");
                return new FoodScanResponse { Success = false, ErrorMessage = ex.Message };
            }
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
                var projectId = _configuration["GoogleCloud:ProjectId"];
                var location = _configuration["GoogleCloud:Location"] ?? "us-central1";
                var model = _configuration["GoogleCloud:Model"] ?? "gemini-2.5-pro";

                var accessToken = await _tokenService.GetAccessTokenAsync();
                var url = $"https://{location}-aiplatform.googleapis.com/v1/projects/{projectId}/locations/{location}/publishers/google/models/{model}:generateContent";

                var base64Audio = Convert.ToBase64String(audioData);
                var mimeType = GetAudioMimeType(audioData);

                var prompt = $@"
Распознай речь из аудио и извлеки информацию о тренировке в JSON формате.

Тип тренировки: {workoutType ?? "любой"}

ВАЖНЫЕ ПРАВИЛА:
1. Если время НЕ указано явно - используй текущее время как startTime
2. Если указано только время начала - добавь 45 минут для endTime
3. Время указывай в ISO формате: ""2025-07-13T17:00:00Z""
4. Если время указано как ""17:00"" - преобразуй в ""2025-07-13T17:00:00Z""
5. Если время указано как ""в 17:00"" или ""начало в 17:00"" - это startTime
6. Если время указано как ""до 17:30"" или ""окончание в 17:30"" - это endTime

Верни ТОЛЬКО валидный JSON без дополнительного текста:
{{
  ""transcribedText"": ""точный распознанный текст"",
  ""workoutData"": {{
    ""type"": ""strength"",
    ""startTime"": ""2025-07-13T17:00:00Z"",
    ""endTime"": ""2025-07-13T17:30:00Z"",
    ""estimatedCalories"": 200,
    ""strengthData"": {{
      ""name"": ""Жим штанги лежа"",
      ""muscleGroup"": ""Грудь"",
      ""equipment"": ""Штанга"",
      ""workingWeight"": 25,
      ""restTimeSeconds"": 120,
      ""sets"": [
        {{
          ""setNumber"": 1,
          ""weight"": 25,
          ""reps"": 10,
          ""isCompleted"": true,
          ""notes"": """"
        }}
      ]
    }},
    ""cardioData"": null,
    ""notes"": [""Тренировка по голосовому вводу""]
  }}
}}

ПРИМЕРЫ:
- ""Штанга 25 кг"" → startTime: текущее время, endTime: +45 минут
- ""Штанга 25 кг начало в 17:00"" → startTime: ""2025-07-13T17:00:00Z"", endTime: ""2025-07-13T17:45:00Z""
- ""Штанга 25 кг начало в 17:00, окончание в 17:30"" → startTime: ""2025-07-13T17:00:00Z"", endTime: ""2025-07-13T17:30:00Z""";

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
                    return new VoiceWorkoutResponse { Success = false, ErrorMessage = $"API error: {response.StatusCode}" };
                }

                return ParseVoiceWorkoutResponse(responseText);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error analyzing voice workout: {ex.Message}");
                return new VoiceWorkoutResponse { Success = false, ErrorMessage = ex.Message };
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
                            return ParseVoiceWorkoutJsonResponse(responseContent);
                        }
                    }
                }

                return new VoiceWorkoutResponse { Success = false, ErrorMessage = "Invalid response format" };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error parsing voice workout response: {ex.Message}");
                return new VoiceWorkoutResponse { Success = false, ErrorMessage = "Failed to parse response" };
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
                _logger.LogInformation($"🎤 Parsing voice workout JSON: {jsonText}");

                var startIndex = jsonText.IndexOf('{');
                var lastIndex = jsonText.LastIndexOf('}');

                if (startIndex >= 0 && lastIndex > startIndex)
                {
                    var cleanJson = jsonText.Substring(startIndex, lastIndex - startIndex + 1);

                    using var document = JsonDocument.Parse(cleanJson);
                    var root = document.RootElement;

                    var response = new VoiceWorkoutResponse
                    {
                        Success = true,
                        TranscribedText = root.TryGetProperty("transcribedText", out var transcribed)
                            ? transcribed.GetString() : "Не удалось распознать текст"
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
                            response.WorkoutData.StrengthData = new StrengthDataDto
                            {
                                Name = SafeGetString(strengthData, "name", "Упражнение"),
                                MuscleGroup = SafeGetString(strengthData, "muscleGroup", "Не указано"),
                                Equipment = SafeGetString(strengthData, "equipment", "Не указано"),
                                WorkingWeight = SafeGetDecimal(strengthData, "workingWeight", 0),
                                RestTimeSeconds = SafeGetInt(strengthData, "restTimeSeconds", 120)
                            };

                            // Парсим подходы
                            if (strengthData.TryGetProperty("sets", out var setsArray) &&
                                setsArray.ValueKind == JsonValueKind.Array)
                            {
                                var sets = new List<StrengthSetDto>();
                                foreach (var setElement in setsArray.EnumerateArray())
                                {
                                    sets.Add(new StrengthSetDto
                                    {
                                        SetNumber = SafeGetInt(setElement, "setNumber", sets.Count + 1),
                                        Weight = SafeGetDecimal(setElement, "weight", response.WorkoutData.StrengthData.WorkingWeight),
                                        Reps = SafeGetInt(setElement, "reps", 10),
                                        IsCompleted = SafeGetBool(setElement, "isCompleted", true),
                                        Notes = SafeGetString(setElement, "notes", "")
                                    });
                                }
                                response.WorkoutData.StrengthData.Sets = sets;
                            }
                            else
                            {
                                // Создаем один подход по умолчанию
                                response.WorkoutData.StrengthData.Sets = new List<StrengthSetDto>
                        {
                            new StrengthSetDto
                            {
                                SetNumber = 1,
                                Weight = response.WorkoutData.StrengthData.WorkingWeight,
                                Reps = 10,
                                IsCompleted = true,
                                Notes = "Подход из голосового ввода"
                            }
                        };
                            }
                        }

                        if (workoutData.TryGetProperty("cardioData", out var cardioData) &&
                            cardioData.ValueKind != JsonValueKind.Null)
                        {
                            response.WorkoutData.CardioData = new CardioDataDto
                            {
                                CardioType = SafeGetString(cardioData, "cardioType", "Кардио"),
                                DistanceKm = SafeGetNullableDecimal(cardioData, "distanceKm"),
                                AvgPulse = SafeGetNullableInt(cardioData, "avgPulse"),
                                MaxPulse = SafeGetNullableInt(cardioData, "maxPulse"),
                                AvgPace = SafeGetString(cardioData, "avgPace", "")
                            };
                        }

                        // Парсим заметки
                        if (workoutData.TryGetProperty("notes", out var notes) &&
                            notes.ValueKind == JsonValueKind.Array)
                        {
                            response.WorkoutData.Notes = notes.EnumerateArray()
                                .Select(x => x.GetString() ?? "").Where(x => !string.IsNullOrEmpty(x)).ToList();
                        }
                        else
                        {
                            response.WorkoutData.Notes = new List<string> { "Тренировка добавлена голосом" };
                        }

                        // Автоматически устанавливаем endTime если не указан
                        if (response.WorkoutData.EndTime <= response.WorkoutData.StartTime)
                        {
                            response.WorkoutData.EndTime = response.WorkoutData.StartTime.AddMinutes(45);
                        }
                    }

                    _logger.LogInformation($"✅ Voice workout parsed successfully: {response.WorkoutData?.Type}");
                    return response;
                }

                _logger.LogError("❌ Invalid JSON format in voice workout response");
                return new VoiceWorkoutResponse { Success = false, ErrorMessage = "Invalid JSON format" };
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error parsing voice workout JSON: {ex.Message}");
                _logger.LogError($"JSON content: {jsonText}");
                return new VoiceWorkoutResponse { Success = false, ErrorMessage = "Failed to parse workout data" };
            }
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
            return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
                ? prop.GetString() ?? defaultValue
                : defaultValue;
        }

        private int SafeGetInt(JsonElement element, string propertyName, int defaultValue = 0)
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

        private decimal SafeGetDecimal(JsonElement element, string propertyName, decimal defaultValue = 0)
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