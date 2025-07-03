using FitnessTracker.API.DTOs;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace FitnessTracker.API.Services
{
    public class GeminiService : IGeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GeminiService> _logger;
        private readonly string _apiKey;
        private readonly string _baseUrl;
        private readonly string _model;

        public GeminiService(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            _apiKey = _configuration["GeminiAI:ApiKey"] ?? throw new InvalidOperationException("Gemini API key not configured");
            _baseUrl = _configuration["GeminiAI:BaseUrl"] ?? "https://generativelanguage.googleapis.com/v1beta";
            _model = _configuration["GeminiAI:Model"] ?? "gemini-1.5-flash";

            _logger.LogInformation($"🤖 Gemini Service initialized with model: {_model}");
        }

        public async Task<FoodScanResponse> AnalyzeFoodImageAsync(byte[] imageData, string? userPrompt = null)
        {
            try
            {
                _logger.LogInformation("🍎 Analyzing food image with Gemini AI");

                // Проверяем качество изображения
                if (!await ValidateImageQualityAsync(imageData))
                {
                    return new FoodScanResponse
                    {
                        Success = false,
                        ErrorMessage = "Изображение низкого качества или не содержит еды"
                    };
                }

                var prompt = CreateFoodAnalysisPrompt(userPrompt);
                var base64Image = ConvertImageToBase64(imageData, "image/jpeg");

                var contents = new List<GeminiContent>
                {
                    new GeminiContent
                    {
                        Parts = new List<GeminiPart>
                        {
                            new GeminiPart { Text = prompt },
                            new GeminiPart
                            {
                                InlineData = new GeminiInlineData
                                {
                                    MimeType = "image/jpeg",
                                    Data = base64Image
                                }
                            }
                        }
                    }
                };

                var config = new GeminiGenerationConfig
                {
                    Temperature = 0, // Более точные ответы
                    MaxOutputTokens = 2048,
                    TopP = 1
                };

                var response = await SendGeminiRequestAsync(contents, config);

                if (response?.Candidates?.Any() == true)
                {
                    var text = response.Candidates[0]?.Content?.Parts?.FirstOrDefault()?.Text;
                    if (!string.IsNullOrEmpty(text))
                    {
                        return ParseFoodAnalysisResponse(text);
                    }
                }

                return new FoodScanResponse
                {
                    Success = false,
                    ErrorMessage = "Не удалось получить ответ от ИИ"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error analyzing food image: {ex.Message}");
                return new FoodScanResponse
                {
                    Success = false,
                    ErrorMessage = $"Ошибка анализа: {ex.Message}"
                };
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
                _logger.LogInformation("💪 Analyzing body images with Gemini AI");

                if (frontImageData == null && sideImageData == null)
                {
                    return new BodyScanResponse
                    {
                        Success = false,
                        ErrorMessage = "Необходимо минимум одно изображение (фронтальное или боковое)"
                    };
                }

                var prompt = CreateBodyAnalysisPrompt(weight, height, age, gender, goals);
                var parts = new List<GeminiPart> { new GeminiPart { Text = prompt } };

                // Добавляем изображения
                if (frontImageData != null)
                {
                    parts.Add(new GeminiPart
                    {
                        InlineData = new GeminiInlineData
                        {
                            MimeType = "image/jpeg",
                            Data = ConvertImageToBase64(frontImageData, "image/jpeg")
                        }
                    });
                }

                if (sideImageData != null)
                {
                    parts.Add(new GeminiPart
                    {
                        InlineData = new GeminiInlineData
                        {
                            MimeType = "image/jpeg",
                            Data = ConvertImageToBase64(sideImageData, "image/jpeg")
                        }
                    });
                }

                if (backImageData != null)
                {
                    parts.Add(new GeminiPart
                    {
                        InlineData = new GeminiInlineData
                        {
                            MimeType = "image/jpeg",
                            Data = ConvertImageToBase64(backImageData, "image/jpeg")
                        }
                    });
                }

                var contents = new List<GeminiContent>
                {
                    new GeminiContent { Parts = parts }
                };

                var config = new GeminiGenerationConfig
                {
                    Temperature = 0,
                    MaxOutputTokens = 4096,
                    TopP = 1
                };

                var response = await SendGeminiRequestAsync(contents, config);

                if (response?.Candidates?.Any() == true)
                {
                    var text = response.Candidates[0]?.Content?.Parts?.FirstOrDefault()?.Text;
                    if (!string.IsNullOrEmpty(text))
                    {
                        return ParseBodyAnalysisResponse(text);
                    }
                }

                return new BodyScanResponse
                {
                    Success = false,
                    ErrorMessage = "Не удалось получить ответ от ИИ"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error analyzing body images: {ex.Message}");
                return new BodyScanResponse
                {
                    Success = false,
                    ErrorMessage = $"Ошибка анализа: {ex.Message}"
                };
            }
        }

        public async Task<VoiceWorkoutResponse> AnalyzeVoiceWorkoutAsync(byte[] audioData, string? workoutType = null)
        {
            try
            {
                _logger.LogInformation("🎤 Analyzing voice workout with Gemini AI");

                // Конвертируем аудио в base64
                var base64Audio = Convert.ToBase64String(audioData);

                var prompt = CreateVoiceWorkoutAnalysisPrompt(workoutType);

                var contents = new List<GeminiContent>
                {
                    new GeminiContent
                    {
                        Parts = new List<GeminiPart>
                        {
                            new GeminiPart { Text = prompt },
                            new GeminiPart
                            {
                                InlineData = new GeminiInlineData
                                {
                                    MimeType = "audio/wav",
                                    Data = base64Audio
                                }
                            }
                        }
                    }
                };

                var config = new GeminiGenerationConfig
                {
                    Temperature = 0,
                    MaxOutputTokens = 2048
                };

                var response = await SendGeminiRequestAsync(contents, config);

                if (response?.Candidates?.Any() == true)
                {
                    var text = response.Candidates[0]?.Content?.Parts?.FirstOrDefault()?.Text;
                    if (!string.IsNullOrEmpty(text))
                    {
                        return ParseVoiceWorkoutResponse(text);
                    }
                }

                return new VoiceWorkoutResponse
                {
                    Success = false,
                    ErrorMessage = "Не удалось распознать аудио"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error analyzing voice workout: {ex.Message}");
                return new VoiceWorkoutResponse
                {
                    Success = false,
                    ErrorMessage = $"Ошибка анализа аудио: {ex.Message}"
                };
            }
        }

        public async Task<VoiceFoodResponse> AnalyzeVoiceFoodAsync(byte[] audioData, string? mealType = null)
        {
            try
            {
                _logger.LogInformation("🗣️ Analyzing voice food with Gemini AI");

                var base64Audio = Convert.ToBase64String(audioData);
                var prompt = CreateVoiceFoodAnalysisPrompt(mealType);

                var contents = new List<GeminiContent>
                {
                    new GeminiContent
                    {
                        Parts = new List<GeminiPart>
                        {
                            new GeminiPart { Text = prompt },
                            new GeminiPart
                            {
                                InlineData = new GeminiInlineData
                                {
                                    MimeType = "audio/wav",
                                    Data = base64Audio
                                }
                            }
                        }
                    }
                };

                var config = new GeminiGenerationConfig
                {
                    Temperature = 0,
                    MaxOutputTokens = 2048
                };

                var response = await SendGeminiRequestAsync(contents, config);

                if (response?.Candidates?.Any() == true)
                {
                    var text = response.Candidates[0]?.Content?.Parts?.FirstOrDefault()?.Text;
                    if (!string.IsNullOrEmpty(text))
                    {
                        return ParseVoiceFoodResponse(text);
                    }
                }

                return new VoiceFoodResponse
                {
                    Success = false,
                    ErrorMessage = "Не удалось распознать аудио"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error analyzing voice food: {ex.Message}");
                return new VoiceFoodResponse
                {
                    Success = false,
                    ErrorMessage = $"Ошибка анализа аудио: {ex.Message}"
                };
            }
        }

        public async Task<GeminiResponse> SendGeminiRequestAsync(List<GeminiContent> contents, GeminiGenerationConfig? config = null)
        {
            try
            {
                var request = new GeminiRequest
                {
                    Contents = contents,
                    GenerationConfig = config ?? new GeminiGenerationConfig
                    {
                        Temperature = 1,
                        TopK = 1,
                        TopP = 1,
                        MaxOutputTokens = 2048
                    },
                    SafetySettings = new List<GeminiSafetySetting>
                    {
                        new GeminiSafetySetting { Category = "HARM_CATEGORY_HARASSMENT", Threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                        new GeminiSafetySetting { Category = "HARM_CATEGORY_HATE_SPEECH", Threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                        new GeminiSafetySetting { Category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", Threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                        new GeminiSafetySetting { Category = "HARM_CATEGORY_DANGEROUS_CONTENT", Threshold = "BLOCK_MEDIUM_AND_ABOVE" }
                    }
                };

                var url = $"{_baseUrl}/models/{_model}:generateContent?key={_apiKey}";
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                };

                var requestJson = JsonSerializer.Serialize(request, jsonOptions);
                _logger.LogDebug($"🔄 Sending request to Gemini: {url}");
                _logger.LogTrace($"📤 Request body: {requestJson}");

                var httpContent = new StringContent(requestJson, Encoding.UTF8, "application/json");
                var httpResponse = await _httpClient.PostAsync(url, httpContent);

                var responseContent = await httpResponse.Content.ReadAsStringAsync();
                _logger.LogTrace($"📥 Response: {responseContent}");

                if (!httpResponse.IsSuccessStatusCode)
                {
                    _logger.LogError($"❌ Gemini API error: {httpResponse.StatusCode} - {responseContent}");
                    throw new HttpRequestException($"Gemini API error: {httpResponse.StatusCode} - {responseContent}");
                }

                var response = JsonSerializer.Deserialize<GeminiResponse>(responseContent, jsonOptions);
                _logger.LogInformation($"✅ Received response from Gemini with {response?.Candidates?.Count ?? 0} candidates");

                return response ?? new GeminiResponse();
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error sending request to Gemini: {ex.Message}");
                throw;
            }
        }

        public string ConvertImageToBase64(byte[] imageData, string mimeType)
        {
            return Convert.ToBase64String(imageData);
        }

        public Task<bool> ValidateImageQualityAsync(byte[] imageData)
        {
            try
            {
                // Проверяем размер изображения (не менее 10KB)
                if (imageData.Length < 10 * 1024)
                {
                    _logger.LogWarning("⚠️ Image too small (less than 10KB)");
                    return Task.FromResult(false);
                }

                // Проверяем максимальный размер (не более 20MB)
                if (imageData.Length > 20 * 1024 * 1024)
                {
                    _logger.LogWarning("⚠️ Image too large (more than 20MB)");
                    return Task.FromResult(false);
                }

                // Проверяем, что это действительно изображение (по заголовку)
                var imageHeaders = new[]
                {
                    new byte[] { 0xFF, 0xD8, 0xFF }, // JPEG
                    new byte[] { 0x89, 0x50, 0x4E, 0x47 }, // PNG
                    new byte[] { 0x47, 0x49, 0x46 }, // GIF
                    new byte[] { 0x42, 0x4D }, // BMP
                    new byte[] { 0x49, 0x49, 0x2A, 0x00 }, // TIFF
                    new byte[] { 0x4D, 0x4D, 0x00, 0x2A } // TIFF
                };

                var hasValidHeader = imageHeaders.Any(header =>
                {
                    if (imageData.Length < header.Length) return false;
                    return header.SequenceEqual(imageData.Take(header.Length));
                });

                if (!hasValidHeader)
                {
                    _logger.LogWarning("⚠️ Invalid image format");
                    return Task.FromResult(false);
                }

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error validating image quality: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        #region Private Methods - Prompts

        private string CreateFoodAnalysisPrompt(string? userPrompt = null)
        {
            var prompt = @"Проанализируй это изображение еды и верни результат СТРОГО в формате JSON.

Ты - эксперт по питанию. Проанализируй изображение и определи:
1. Все блюда/продукты на изображении
2. Примерный вес/объем каждого продукта
3. Правильную единицу измерения (граммы для твердой еды, миллилитры для жидкостей)
4. Пищевую ценность на 100г/100мл для каждого продукта
5. Общее количество калорий

ВАЖНО: Используй правильные единицы измерения:
- Для жидкостей (супы, борщ, молоко, соки): ""ml"" (миллилитры)
- Для твердой еды (хлеб, мясо, овощи): ""g"" (граммы)
- Для порошков/специй: ""g"" (граммы)

Верни результат ТОЛЬКО в формате JSON без дополнительного текста:

{
  ""success"": true,
  ""foodItems"": [
    {
      ""name"": ""Название блюда/продукта"",
      ""estimatedWeight"": 150,
      ""weightType"": ""g"",
      ""description"": ""Краткое описание"",
      ""nutritionPer100g"": {
        ""calories"": 250,
        ""proteins"": 12.5,
        ""fats"": 8.2,
        ""carbs"": 35.1
      },
      ""totalCalories"": 375,
      ""confidence"": 0.8
    }
  ],
  ""estimatedCalories"": 375,
  ""fullDescription"": ""Полное описание всех блюд""
}

Если изображение не содержит еды, верни:
{
  ""success"": false,
  ""errorMessage"": ""На изображении не обнаружена еда""
}

Будь точным в оценке веса и калорий. Учитывай размеры порций.";

            if (!string.IsNullOrEmpty(userPrompt))
            {
                prompt += $"\n\nДополнительная информация от пользователя: {userPrompt}";
            }

            return prompt;
        }

        private string CreateBodyAnalysisPrompt(decimal? weight, decimal? height, int? age, string? gender, string? goals)
        {
            var prompt = @"Проанализируй изображения тела и верни результат СТРОГО в формате JSON.

Ты - эксперт по фитнесу и анатомии. Проанализируй изображения и определи:
1. Приблизительный процент жира в теле
2. Приблизительный процент мышечной массы
3. Тип телосложения
4. Анализ осанки
5. Общее состояние
6. Рекомендации

Верни результат ТОЛЬКО в формате JSON:

{
  ""success"": true,
  ""bodyAnalysis"": {
    ""estimatedBodyFatPercentage"": 15.5,
    ""estimatedMusclePercentage"": 42.0,
    ""bodyType"": ""Мезоморф"",
    ""postureAnalysis"": ""Небольшая сутулость в плечах"",
    ""overallCondition"": ""Хорошая физическая форма"",
    ""bmi"": 23.4,
    ""bmiCategory"": ""Нормальный вес"",
    ""estimatedWaistCircumference"": 80,
    ""estimatedChestCircumference"": 95,
    ""estimatedHipCircumference"": 90,
    ""exerciseRecommendations"": [
      ""Силовые тренировки 3 раза в неделю"",
      ""Кардио 2 раза в неделю""
    ],
    ""nutritionRecommendations"": [
      ""Увеличить потребление белка"",
      ""Контролировать углеводы""
    ],
    ""trainingFocus"": ""Набор мышечной массы""
  },
  ""recommendations"": [
    ""Рекомендация 1"",
    ""Рекомендация 2""
  ],
  ""fullAnalysis"": ""Детальный анализ тела""
}";

            var userInfo = new List<string>();
            if (weight.HasValue) userInfo.Add($"Вес: {weight}кг");
            if (height.HasValue) userInfo.Add($"Рост: {height}см");
            if (age.HasValue) userInfo.Add($"Возраст: {age} лет");
            if (!string.IsNullOrEmpty(gender)) userInfo.Add($"Пол: {gender}");
            if (!string.IsNullOrEmpty(goals)) userInfo.Add($"Цели: {goals}");

            if (userInfo.Any())
            {
                prompt += $"\n\nИнформация о пользователе: {string.Join(", ", userInfo)}";
            }

            return prompt;
        }

        private string CreateVoiceWorkoutAnalysisPrompt(string? workoutType)
        {
            return @"Распознай речь из аудио и извлеки информацию о тренировке. Верни результат в JSON:

{
  ""success"": true,
  ""transcribedText"": ""Расшифрованный текст"",
  ""workoutData"": {
    ""type"": ""strength"", // или ""cardio""
    ""startTime"": ""2024-01-01T10:00:00Z"",
    ""endTime"": ""2024-01-01T11:00:00Z"",
    ""estimatedCalories"": 300,
    ""strengthData"": {
      ""name"": ""Жим лежа"",
      ""muscleGroup"": ""Грудь"",
      ""equipment"": ""Штанга"",
      ""workingWeight"": 80,
      ""restTimeSeconds"": 120
    },
    ""notes"": [""Примечание 1""]
  }
}

Если не удалось распознать тренировку:
{
  ""success"": false,
  ""errorMessage"": ""Не удалось распознать информацию о тренировке""
}";
        }

        private string CreateVoiceFoodAnalysisPrompt(string? mealType)
        {
            return @"Распознай речь из аудио и извлеки информацию о еде. Верни результат в JSON:

{
  ""success"": true,
  ""transcribedText"": ""Расшифрованный текст"",
  ""foodItems"": [
    {
      ""name"": ""Овсянка"",
      ""estimatedWeight"": 100,
      ""weightType"": ""g"",
      ""description"": ""Овсяная каша с молоком"",
      ""nutritionPer100g"": {
        ""calories"": 389,
        ""proteins"": 16.9,
        ""fats"": 6.9,
        ""carbs"": 66.3
      },
      ""totalCalories"": 389,
      ""confidence"": 0.9
    }
  ],
  ""estimatedTotalCalories"": 389
}";
        }

        #endregion

        #region Private Methods - Response Parsers

        private FoodScanResponse ParseFoodAnalysisResponse(string jsonResponse)
        {
            try
            {
                // Очищаем ответ от лишних символов
                var cleanJson = ExtractJsonFromResponse(jsonResponse);
                _logger.LogDebug($"🍎 Parsing food analysis: {cleanJson}");

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var tempResponse = JsonSerializer.Deserialize<TempFoodScanResponse>(cleanJson, options);

                if (tempResponse != null && tempResponse.Success)
                {
                    var response = new FoodScanResponse
                    {
                        Success = tempResponse.Success,
                        ErrorMessage = tempResponse.ErrorMessage,
                        FullDescription = tempResponse.FullDescription,
                        EstimatedCalories = (int)Math.Round(tempResponse.EstimatedCalories),
                        FoodItems = tempResponse.FoodItems?.Select(item => new FoodItemResponse
                        {
                            Name = item.Name,
                            EstimatedWeight = item.EstimatedWeight,
                            WeightType = item.WeightType,
                            Description = item.Description,
                            TotalCalories = (int)Math.Round(item.TotalCalories),
                            Confidence = item.Confidence,
                            NutritionPer100g = item.NutritionPer100g
                        }).ToList() ?? new List<FoodItemResponse>()
                    };

                    _logger.LogInformation($"✅ Successfully parsed {response.FoodItems?.Count ?? 0} food items");
                    return response;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error parsing food analysis response: {ex.Message}");
                _logger.LogDebug($"Original response: {jsonResponse}");
            }

            return new FoodScanResponse
            {
                Success = false,
                ErrorMessage = "Не удалось обработать ответ от ИИ"
            };
        }

        private BodyScanResponse ParseBodyAnalysisResponse(string jsonResponse)
        {
            try
            {
                var cleanJson = ExtractJsonFromResponse(jsonResponse);
                _logger.LogDebug($"💪 Parsing body analysis: {cleanJson}");

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var response = JsonSerializer.Deserialize<BodyScanResponse>(cleanJson, options);

                if (response != null)
                {
                    response.Success = true;
                    _logger.LogInformation("✅ Successfully parsed body analysis");
                    return response;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error parsing body analysis response: {ex.Message}");
                _logger.LogDebug($"Original response: {jsonResponse}");
            }

            return new BodyScanResponse
            {
                Success = false,
                ErrorMessage = "Не удалось обработать ответ от ИИ"
            };
        }

        private VoiceWorkoutResponse ParseVoiceWorkoutResponse(string jsonResponse)
        {
            try
            {
                var cleanJson = ExtractJsonFromResponse(jsonResponse);
                _logger.LogDebug($"🎤 Parsing voice workout: {cleanJson}");

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var response = JsonSerializer.Deserialize<VoiceWorkoutResponse>(cleanJson, options);

                if (response != null)
                {
                    response.Success = true;
                    _logger.LogInformation("✅ Successfully parsed voice workout");
                    return response;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error parsing voice workout response: {ex.Message}");
                _logger.LogDebug($"Original response: {jsonResponse}");
            }

            return new VoiceWorkoutResponse
            {
                Success = false,
                ErrorMessage = "Не удалось обработать ответ от ИИ"
            };
        }

        private VoiceFoodResponse ParseVoiceFoodResponse(string jsonResponse)
        {
            try
            {
                var cleanJson = ExtractJsonFromResponse(jsonResponse);
                _logger.LogDebug($"🗣️ Parsing voice food: {cleanJson}");

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var tempResponse = JsonSerializer.Deserialize<TempVoiceFoodResponse>(cleanJson, options);

                if (tempResponse != null && tempResponse.Success)
                {
                    var response = new VoiceFoodResponse
                    {
                        Success = tempResponse.Success,
                        ErrorMessage = tempResponse.ErrorMessage,
                        TranscribedText = tempResponse.TranscribedText,
                        EstimatedTotalCalories = (int)Math.Round(tempResponse.EstimatedTotalCalories),
                        FoodItems = tempResponse.FoodItems?.Select(item => new FoodItemResponse
                        {
                            Name = item.Name,
                            EstimatedWeight = item.EstimatedWeight,
                            WeightType = item.WeightType,
                            Description = item.Description,
                            TotalCalories = (int)Math.Round(item.TotalCalories),
                            Confidence = item.Confidence,
                            NutritionPer100g = item.NutritionPer100g
                        }).ToList() ?? new List<FoodItemResponse>()
                    };

                    _logger.LogInformation($"✅ Successfully parsed voice food with {response.FoodItems?.Count ?? 0} items");
                    return response;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error parsing voice food response: {ex.Message}");
                _logger.LogDebug($"Original response: {jsonResponse}");
            }

            return new VoiceFoodResponse
            {
                Success = false,
                ErrorMessage = "Не удалось обработать ответ от ИИ"
            };
        }

        private string ExtractJsonFromResponse(string response)
        {
            // Ищем JSON в ответе (между { и })
            var jsonMatch = Regex.Match(response, @"\{.*\}", RegexOptions.Singleline);
            if (jsonMatch.Success)
            {
                return jsonMatch.Value;
            }

            // Если не найден JSON, возвращаем весь ответ
            return response.Trim();
        }

        #endregion

        #region Temporary Classes for Parsing Decimal Values

        /// <summary>
        /// Промежуточный класс для парсинга decimal значений из Gemini
        /// </summary>
        private class TempFoodScanResponse
        {
            public bool Success { get; set; }
            public string? ErrorMessage { get; set; }
            public List<TempFoodItemAnalysis>? FoodItems { get; set; }
            public decimal EstimatedCalories { get; set; }
            public string? FullDescription { get; set; }
        }

        private class TempFoodItemAnalysis
        {
            public string Name { get; set; } = string.Empty;
            public decimal EstimatedWeight { get; set; }
            public string WeightType { get; set; } = "g";
            public string? Description { get; set; }
            public NutritionPer100gDto NutritionPer100g { get; set; } = new NutritionPer100gDto();
            public decimal TotalCalories { get; set; }
            public decimal Confidence { get; set; }
        }

        private class TempVoiceFoodResponse
        {
            public bool Success { get; set; }
            public string? ErrorMessage { get; set; }
            public string? TranscribedText { get; set; }
            public List<TempFoodItemAnalysis>? FoodItems { get; set; }
            public decimal EstimatedTotalCalories { get; set; }
        }

        #endregion
    }
}