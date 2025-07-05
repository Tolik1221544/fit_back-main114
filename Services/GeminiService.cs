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

                // Определяем MIME тип аудио
                var mimeType = DetectAudioMimeType(audioData);
                _logger.LogInformation($"🎵 Detected audio format: {mimeType}");

                // Создаем prompt для анализа аудио
                var prompt = CreateVoiceWorkoutAnalysisPrompt(workoutType);
                var base64Audio = Convert.ToBase64String(audioData);

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
                                    MimeType = mimeType,
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

                // Определяем MIME тип аудио
                var mimeType = DetectAudioMimeType(audioData);
                _logger.LogInformation($"🎵 Detected audio format: {mimeType}");

                // Создаем prompt для анализа аудио
                var prompt = CreateVoiceFoodAnalysisPrompt(mealType);
                var base64Audio = Convert.ToBase64String(audioData);

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
                                    MimeType = mimeType,
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

        #region Private Methods - Audio Detection

        private string DetectAudioMimeType(byte[] audioData)
        {
            // Определяем тип аудио по заголовку файла
            if (audioData.Length < 4) return "audio/ogg";

            // OGG Vorbis
            if (audioData[0] == 0x4F && audioData[1] == 0x67 && audioData[2] == 0x67 && audioData[3] == 0x53)
                return "audio/ogg";

            // MP3
            if (audioData[0] == 0xFF && (audioData[1] & 0xE0) == 0xE0)
                return "audio/mp3";

            // WAV
            if (audioData[0] == 0x52 && audioData[1] == 0x49 && audioData[2] == 0x46 && audioData[3] == 0x46)
                return "audio/wav";

            // WebM
            if (audioData[0] == 0x1A && audioData[1] == 0x45 && audioData[2] == 0xDF && audioData[3] == 0xA3)
                return "audio/webm";

            return "audio/ogg"; // Default
        }

        #endregion

        #region Private Methods - Prompts

        private string CreateFoodAnalysisPrompt(string? userPrompt = null)
        {
            var prompt = @"Проанализируй это изображение еды И НАПИТКОВ и верни результат СТРОГО в формате JSON.

Ты - эксперт по питанию и калорийности блюд. Проанализируй изображение и определи:

🔥 ВАЖНО: Включай В СПИСОК не только еду, но и ВСЕ НАПИТКИ!
- Чай ☕
- Кофе ☕  
- Соки 🥤
- Молоко 🥛
- Компоты 🍹
- Любые другие напитки

🔥 КРИТИЧЕСКИ ВАЖНО: ВСЕГДА заполняй ВСЕ поля nutritionPer100g!
НИКОГДА не оставляй proteins, fats, carbs равными 0, если calories > 0!

ПРАВИЛА ЗАПОЛНЕНИЯ БЖУ (включая напитки):

📊 БОРЩ И СУПЫ:
- calories: 40-80 ккал
- proteins: 2-4г (мясо + овощи)
- fats: 2-5г (сметана + мясо) 
- carbs: 4-10г (овощи + крупы)

🍞 ХЛЕБ И ВЫПЕЧКА:
- calories: 220-280 ккал
- proteins: 6-9г
- fats: 1-3г  
- carbs: 45-55г

☕ НАПИТКИ:
- Чай без сахара: calories: 2, proteins: 0.1, fats: 0.0, carbs: 0.3
- Чай с сахаром: calories: 25, proteins: 0.1, fats: 0.0, carbs: 6.2
- Кофе черный: calories: 5, proteins: 0.2, fats: 0.0, carbs: 1.0
- Кофе с молоком: calories: 35, proteins: 1.8, fats: 1.5, carbs: 4.0
- Сок яблочный: calories: 46, proteins: 0.1, fats: 0.1, carbs: 11.3
- Молоко: calories: 52, proteins: 2.8, fats: 2.5, carbs: 4.7

🥩 МЯСО И ПТИЦА:
- calories: 150-300 ккал
- proteins: 20-35г
- fats: 5-25г
- carbs: 0-2г

🥬 ОВОЩИ И ЗЕЛЕНЬ:
- calories: 15-50 ккал
- proteins: 1-3г
- fats: 0.1-1г
- carbs: 2-8г

🥛 МОЛОЧНЫЕ ПРОДУКТЫ:
- calories: 50-300 ккал  
- proteins: 2-20г
- fats: 2-30г
- carbs: 3-6г

КОНКРЕТНЫЕ ПРИМЕРЫ (используй как шаблон):

{
  ""name"": ""Борщ со сметаной"",
  ""estimatedWeight"": 300,
  ""weightType"": ""ml"",
  ""nutritionPer100g"": {
    ""calories"": 65,
    ""proteins"": 3.2,
    ""fats"": 4.1,
    ""carbs"": 8.5
  }
}

{
  ""name"": ""Чай черный без сахара"",
  ""estimatedWeight"": 200,
  ""weightType"": ""ml"",
  ""nutritionPer100g"": {
    ""calories"": 2,
    ""proteins"": 0.1,
    ""fats"": 0.0,
    ""carbs"": 0.3
  }
}

{
  ""name"": ""Хлеб ржаной"", 
  ""estimatedWeight"": 50,
  ""weightType"": ""g"",
  ""nutritionPer100g"": {
    ""calories"": 250,
    ""proteins"": 8.1,
    ""fats"": 1.0,
    ""carbs"": 48.8
  }
}

ОБЯЗАТЕЛЬНО ВКЛЮЧАЙ В СПИСОК:
✅ Все блюда (супы, каши, мясо)
✅ Все напитки (чай, кофе, соки, молоко)
✅ Хлеб, выпечку, десерты
✅ Овощи, фрукты, зелень
✅ Молочные продукты

НЕ ПРОПУСКАЙ напитки только потому, что у них мало калорий!

СТРОГИЕ ТРЕБОВАНИЯ:
✅ ВСЕ 4 поля (calories, proteins, fats, carbs) должны быть заполнены
✅ НЕ используй 0 для всех полей одновременно (кроме воды)
✅ Используй правильные единицы: ml для жидкостей, g для твердого
✅ Включай ВСЕ видимые продукты питания и напитки

ФОРМУЛА ПРОВЕРКИ: 
calories ≈ (proteins × 4) + (fats × 9) + (carbs × 4)

Верни результат ТОЛЬКО в формате JSON:

{
  ""success"": true,
  ""foodItems"": [
    {
      ""name"": ""Борщ"",
      ""estimatedWeight"": 300,
      ""weightType"": ""ml"",
      ""description"": ""Порция борща"",
      ""nutritionPer100g"": {
        ""calories"": 65,
        ""proteins"": 3.2,
        ""fats"": 4.1,
        ""carbs"": 8.5
      },
      ""totalCalories"": 195,
      ""confidence"": 0.9
    },
    {
      ""name"": ""Чай черный"",
      ""estimatedWeight"": 200,
      ""weightType"": ""ml"",
      ""description"": ""Чашка черного чая"",
      ""nutritionPer100g"": {
        ""calories"": 2,
        ""proteins"": 0.1,
        ""fats"": 0.0,
        ""carbs"": 0.3
      },
      ""totalCalories"": 4,
      ""confidence"": 0.8
    }
  ],
  ""estimatedCalories"": 199,
  ""fullDescription"": ""Борщ, хлеб и чай""
}

🚨 ПРОВЕРЬ ПЕРЕД ОТПРАВКОЙ:
- Включены ли ВСЕ видимые продукты И напитки?
- Все поля nutritionPer100g заполнены?
- Нет ли пропущенных чашек чая/кофе?

Если изображение не содержит еды и напитков, верни:
{
  ""success"": false,
  ""errorMessage"": ""На изображении не обнаружена еда или напитки""
}";

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
      ""restTimeSeconds"": 120,
      ""sets"": [
        {
          ""setNumber"": 1,
          ""weight"": 80,
          ""reps"": 10,
          ""isCompleted"": true
        }
      ]
    },
    ""cardioData"": {
      ""cardioType"": ""Бег"",
      ""distanceKm"": 5.0,
      ""avgPulse"": 140,
      ""maxPulse"": 160
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
            return @"Распознай речь из аудио и извлеки информацию о еде. Верни результат СТРОГО в формате JSON:

{
  ""success"": true,
  ""transcribedText"": ""Расшифрованный текст из аудио"",
  ""foodItems"": [
    {
      ""name"": ""Название блюда"",
      ""estimatedWeight"": 100,
      ""weightType"": ""g"",
      ""description"": ""Описание блюда"",
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
}

Если не удалось распознать еду, верни:
{
  ""success"": false,
  ""errorMessage"": ""Не удалось распознать информацию о еде из аудио""
}

ВАЖНО: 
- Для жидкостей используй weightType: ""ml""
- Для твердой еды используй weightType: ""g""
- Будь точным в оценке веса порций
- Распознай максимально точно что говорится в аудио";
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
                    if (response.Success)
                    {
                        _logger.LogInformation("✅ Successfully parsed voice workout");
                        return response;
                    }
                    else
                    {
                        // Если Gemini вернул success: false, создаем fallback
                        return CreateFallbackWorkoutResponse(jsonResponse);
                    }
                }

                // Fallback для неструктурированного ответа
                return CreateFallbackWorkoutResponse(jsonResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error parsing voice workout response: {ex.Message}");
                _logger.LogDebug($"Original response: {jsonResponse}");

                return CreateFallbackWorkoutResponse(jsonResponse);
            }
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

                // Сначала пытаемся распарсить как полный JSON ответ
                try
                {
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
                catch (JsonException)
                {
                    // Если не удалось распарсить как структурированный JSON, создаем fallback ответ
                    _logger.LogWarning("⚠️ Failed to parse as structured JSON, creating fallback response");
                }

                // Fallback: создаем ответ на основе текста ответа от ИИ
                return CreateFallbackVoiceFoodResponse(jsonResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error parsing voice food response: {ex.Message}");
                _logger.LogDebug($"Original response: {jsonResponse}");

                // Возвращаем fallback ответ вместо ошибки
                return CreateFallbackVoiceFoodResponse(jsonResponse);
            }
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

        #region Private Methods - Fallback Responses

        private VoiceWorkoutResponse CreateFallbackWorkoutResponse(string aiResponse)
        {
            try
            {
                _logger.LogInformation("🎭 Creating fallback voice workout response");

                // Извлекаем полезную информацию из ответа ИИ
                var text = aiResponse.ToLowerInvariant();
                var workoutType = DetermineWorkoutType(text);

                var response = new VoiceWorkoutResponse
                {
                    Success = true,
                    TranscribedText = ExtractMeaningfulText(aiResponse),
                    WorkoutData = new WorkoutDataResponse // Исправлено: было VoiceWorkoutData
                    {
                        Type = workoutType,
                        StartTime = DateTime.UtcNow.AddMinutes(-30),
                        EndTime = DateTime.UtcNow,
                        EstimatedCalories = 200,
                        StrengthData = workoutType == "strength" ? new StrengthDataDto
                        {
                            Name = ExtractExerciseName(text),
                            MuscleGroup = "Разные группы мышц",
                            Equipment = ExtractEquipment(text),
                            WorkingWeight = ExtractWeight(text),
                            Sets = new List<StrengthSetDto>
                    {
                        new StrengthSetDto { SetNumber = 1, Weight = ExtractWeight(text), Reps = 10, IsCompleted = true }
                    }
                        } : null,
                        CardioData = workoutType == "cardio" ? new CardioDataDto
                        {
                            CardioType = ExtractCardioType(text),
                            DistanceKm = 3,
                            AvgPulse = 140
                        } : null,
                        Notes = new List<string> { $"Распознано из голосового ввода: {ExtractMeaningfulText(aiResponse)}" }
                    }
                };

                _logger.LogInformation($"✅ Created fallback workout response: {workoutType}");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error creating fallback workout response: {ex.Message}");

                return new VoiceWorkoutResponse
                {
                    Success = false,
                    ErrorMessage = "Не удалось обработить голосовой ввод о тренировке"
                };
            }
        }

        private VoiceFoodResponse CreateFallbackVoiceFoodResponse(string aiResponse)
        {
            try
            {
                _logger.LogInformation("🎭 Creating fallback voice food response");

                // Пытаемся извлечь полезную информацию из ответа ИИ
                var foodItems = new List<FoodItemResponse>();

                // Анализируем текст ответа на предмет упоминания еды
                var text = aiResponse.ToLowerInvariant();

                // База данных еды для fallback
                var foodDatabase = new Dictionary<string, (string displayName, decimal calories, decimal proteins, decimal fats, decimal carbs, decimal weight, string weightType)>
                {
                    
                    ["чай"] = ("Чай черный без сахара", 2, 0.1m, 0.0m, 0.3m, 200, "ml"),
                    ["чайный"] = ("Чай черный", 2, 0.1m, 0.0m, 0.3m, 200, "ml"),
                    ["кофе"] = ("Кофе черный", 5, 0.2m, 0.0m, 1.0m, 200, "ml"),
                    ["эспрессо"] = ("Эспрессо", 5, 0.2m, 0.0m, 1.0m, 60, "ml"),
                    ["капучино"] = ("Капучино", 70, 4.0m, 4.0m, 6.0m, 200, "ml"),
                    ["латте"] = ("Латте", 90, 5.0m, 5.0m, 8.0m, 250, "ml"),
                    ["сок"] = ("Сок яблочный", 46, 0.1m, 0.1m, 11.3m, 200, "ml"),
                    ["компот"] = ("Компот фруктовый", 60, 0.2m, 0.0m, 15.0m, 200, "ml"),
                    ["морс"] = ("Морс ягодный", 41, 0.1m, 0.0m, 10.2m, 200, "ml"),
                    ["какао"] = ("Какао на молоке", 67, 3.2m, 2.5m, 9.0m, 200, "ml"),
                    ["кисель"] = ("Кисель", 53, 0.0m, 0.0m, 13.0m, 200, "ml"),

                    ["борщ"] = ("Борщ со сметаной", 65, 3.2m, 4.1m, 8.5m, 300, "ml"), // Увеличили калории с 45 до 65
                    ["суп"] = ("Суп овощной", 45, 2.5m, 1.5m, 6.0m, 250, "ml"),

                    // Хлеб и выпечка
                    ["хлеб"] = ("Хлеб белый", 250, 8.1m, 1.0m, 48.8m, 50, "g"),
                    ["булка"] = ("Булка", 280, 8.5m, 2.5m, 52.0m, 60, "g"),
                    ["батон"] = ("Батон", 260, 7.9m, 2.9m, 50.1m, 50, "g"),

                    // Каши и крупы
                    ["овсянка"] = ("Овсяная каша", 88, 3.0m, 1.7m, 15.0m, 150, "g"),
                    ["каша"] = ("Каша молочная", 95, 3.5m, 2.1m, 16.5m, 150, "g"),
                    ["гречка"] = ("Гречневая каша", 132, 4.5m, 2.3m, 25.0m, 150, "g"),
                    ["рис"] = ("Рис отварной", 116, 2.2m, 0.5m, 25.0m, 150, "g"),
                    ["манка"] = ("Манная каша", 98, 3.0m, 3.2m, 15.3m, 150, "g"),

                    // Фрукты
                    ["банан"] = ("Банан", 89, 1.1m, 0.3m, 22.8m, 120, "g"),
                    ["яблоко"] = ("Яблоко", 52, 0.3m, 0.2m, 13.8m, 180, "g"),
                    ["апельсин"] = ("Апельсин", 43, 0.9m, 0.2m, 10.3m, 150, "g"),
                    ["груша"] = ("Груша", 42, 0.4m, 0.3m, 10.7m, 180, "g"),

                    // Мясо и птица 
                    ["курица"] = ("Куриная грудка", 165, 31.0m, 3.6m, 0.0m, 150, "g"),
                    ["куриная"] = ("Куриная грудка", 165, 31.0m, 3.6m, 0.0m, 150, "g"),
                    ["мясо"] = ("Говядина тушеная", 220, 25.0m, 12.0m, 0.0m, 120, "g"),
                    ["говядина"] = ("Говядина", 250, 26.0m, 15.0m, 0.0m, 120, "g"),
                    ["свинина"] = ("Свинина", 316, 21.0m, 25.0m, 0.0m, 120, "g"),
                    ["котлета"] = ("Котлета мясная", 280, 14.0m, 20.0m, 10.0m, 100, "g"),

                    // Рыба
                    ["рыба"] = ("Рыба отварная", 196, 22.0m, 11.0m, 0.0m, 150, "g"),
                    ["лосось"] = ("Лосось", 208, 25.4m, 10.8m, 0.0m, 150, "g"),
                    ["тунец"] = ("Тунец", 184, 30.0m, 6.0m, 0.0m, 150, "g"),

                    // Овощи
                    ["картошка"] = ("Картофель отварной", 82, 2.0m, 0.4m, 16.1m, 200, "g"),
                    ["картофель"] = ("Картофель отварной", 82, 2.0m, 0.4m, 16.1m, 200, "g"),
                    ["морковь"] = ("Морковь", 35, 1.3m, 0.1m, 7.2m, 100, "g"),
                    ["капуста"] = ("Капуста", 28, 1.8m, 0.1m, 5.4m, 150, "g"),
                    ["помидор"] = ("Помидор", 20, 0.6m, 0.2m, 4.2m, 150, "g"),
                    ["огурец"] = ("Огурец", 15, 0.8m, 0.1m, 2.5m, 150, "g"),
                    ["салат"] = ("Салат овощной", 35, 1.2m, 0.3m, 6.0m, 150, "g"),

                    // Молочные продукты
                    ["молоко"] = ("Молоко 2.5%", 52, 2.8m, 2.5m, 4.7m, 200, "ml"),
                    ["кефир"] = ("Кефир", 56, 2.8m, 3.2m, 4.1m, 200, "ml"),
                    ["творог"] = ("Творог 5%", 121, 17.2m, 5.0m, 1.8m, 100, "g"),
                    ["йогурт"] = ("Йогурт натуральный", 66, 5.0m, 3.5m, 3.5m, 150, "g"),
                    ["сыр"] = ("Сыр твердый", 364, 26.8m, 27.3m, 0.0m, 50, "g"),
                    ["сметана"] = ("Сметана 20%", 206, 2.8m, 20.0m, 3.2m, 30, "g"),

                    // Макароны и выпечка
                    ["макароны"] = ("Макароны отварные", 112, 3.5m, 0.4m, 23.0m, 150, "g"),
                    ["спагетти"] = ("Спагетти", 158, 5.8m, 0.9m, 30.9m, 150, "g"),
                    ["пельмени"] = ("Пельмени", 248, 11.9m, 12.4m, 23.0m, 150, "g"),

                    // Яйца
                    ["яйцо"] = ("Яйцо куриное", 155, 12.7m, 10.9m, 0.7m, 60, "g"),
                    ["омлет"] = ("Омлет из 2 яиц", 184, 14.0m, 15.4m, 2.0m, 120, "g"),
                    ["яичница"] = ("Яичница", 196, 13.6m, 14.8m, 0.9m, 100, "g"),

                    // Напитки
                    ["чай"] = ("Чай без сахара", 2, 0.0m, 0.0m, 0.3m, 200, "ml"),
                    ["кофе"] = ("Кофе черный", 7, 0.2m, 0.0m, 1.2m, 200, "ml"),
                    ["сок"] = ("Сок яблочный", 46, 0.1m, 0.1m, 11.3m, 200, "ml"),

                    // Сладости и десерты
                    ["печенье"] = ("Печенье", 417, 7.5m, 11.8m, 74.4m, 50, "g"),
                    ["торт"] = ("Торт", 344, 4.7m, 15.0m, 49.8m, 80, "g"),
                    ["шоколад"] = ("Шоколад молочный", 534, 6.9m, 35.7m, 52.4m, 30, "g"),

                    // Орехи
                    ["орехи"] = ("Орехи грецкие", 656, 16.2m, 60.8m, 11.1m, 30, "g"),
                    ["миндаль"] = ("Миндаль", 645, 18.6m, 57.7m, 16.2m, 30, "g"),

                    // Бобовые
                    ["фасоль"] = ("Фасоль отварная", 123, 7.8m, 0.5m, 21.5m, 150, "g"),
                    ["горох"] = ("Горох отварной", 60, 6.0m, 0.0m, 9.0m, 150, "g"),

                    // Дополнительные блюда
                    ["плов"] = ("Плов", 196, 4.9m, 6.7m, 30.5m, 200, "g"),
                    ["борщ"] = ("Борщ украинский", 65, 3.2m, 4.1m, 8.5m, 300, "ml"), 
                    ["щи"] = ("Щи", 38, 1.8m, 2.1m, 4.2m, 300, "ml"),
                    ["солянка"] = ("Солянка", 68, 4.2m, 4.8m, 3.5m, 300, "ml"),
                    ["бульон"] = ("Куриный бульон", 15, 2.0m, 0.5m, 0.0m, 300, "ml")
                };

                var foundFoods = new List<string>();

                foreach (var kvp in foodDatabase)
                {
                    if (text.Contains(kvp.Key))
                    {
                        foundFoods.Add(kvp.Key);
                    }
                }

                // Если нашли упоминания еды, создаем элементы
                if (foundFoods.Any())
                {
                    foreach (var foodKey in foundFoods.Take(3)) // Максимум 3 элемента
                    {
                        var (displayName, caloriesPer100g, proteins, fats, carbs, estimatedWeight, weightType) = foodDatabase[foodKey];
                        var totalCalories = (int)Math.Round((caloriesPer100g * estimatedWeight) / 100);

                        foodItems.Add(new FoodItemResponse
                        {
                            Name = displayName,
                            EstimatedWeight = estimatedWeight,
                            WeightType = weightType,
                            Description = "Распознано из голосового ввода",
                            NutritionPer100g = new NutritionPer100gDto
                            {
                                Calories = caloriesPer100g,
                                Proteins = proteins,
                                Fats = fats,
                                Carbs = carbs
                            },
                            TotalCalories = totalCalories,
                            Confidence = 0.7m
                        });
                    }
                }
                else
                {
                    // Если ничего не нашли, создаем общий элемент
                    foodItems.Add(new FoodItemResponse
                    {
                        Name = "Еда из голосового ввода",
                        EstimatedWeight = 100,
                        WeightType = "g",
                        Description = "Не удалось точно определить тип еды",
                        NutritionPer100g = new NutritionPer100gDto
                        {
                            Calories = 200,
                            Proteins = 10,
                            Fats = 8,
                            Carbs = 25
                        },
                        TotalCalories = 200,
                        Confidence = 0.5m
                    });
                }

                var response = new VoiceFoodResponse
                {
                    Success = true,
                    TranscribedText = ExtractMeaningfulText(aiResponse),
                    FoodItems = foodItems,
                    EstimatedTotalCalories = (int)foodItems.Sum(f => f.TotalCalories)
                };

                _logger.LogInformation($"✅ Created fallback voice food response with {foodItems.Count} items, {response.EstimatedTotalCalories} calories");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error creating fallback voice food response: {ex.Message}");

                return new VoiceFoodResponse
                {
                    Success = false,
                    ErrorMessage = "Не удалось обработать голосовой ввод о питании"
                };
            }
        }

        private string ExtractMeaningfulText(string aiResponse)
        {
            // Пытаемся извлечь осмысленный текст из ответа ИИ
            if (string.IsNullOrEmpty(aiResponse))
                return "Голосовой ввод";

            // Убираем JSON-подобные символы и лишние пробелы
            var cleanText = aiResponse
                .Replace("{", "")
                .Replace("}", "")
                .Replace("[", "")
                .Replace("]", "")
                .Replace("\"", "")
                .Replace("success", "")
                .Replace("false", "")
                .Replace("true", "")
                .Replace("errorMessage", "")
                .Replace(":", "")
                .Replace(",", " ")
                .Trim();

            // Ограничиваем длину
            if (cleanText.Length > 100)
                cleanText = cleanText.Substring(0, 100) + "...";

            return string.IsNullOrWhiteSpace(cleanText) ? "Голосовой ввод" : cleanText;
        }

        private string DetermineWorkoutType(string text)
        {
            var strengthKeywords = new[] { "жим", "приседания", "тяга", "подтягивания", "отжимания", "вес", "кг", "штанга", "гантели" };
            var cardioKeywords = new[] { "бег", "велосипед", "плавание", "ходьба", "кардио", "км", "минут" };

            if (strengthKeywords.Any(k => text.Contains(k))) return "strength";
            if (cardioKeywords.Any(k => text.Contains(k))) return "cardio";

            return "strength"; // Default
        }

        private string ExtractExerciseName(string text)
        {
            var exercises = new Dictionary<string, string>
            {
                ["жим"] = "Жим лежа",
                ["приседания"] = "Приседания",
                ["тяга"] = "Тяга штанги",
                ["подтягивания"] = "Подтягивания",
                ["отжимания"] = "Отжимания",
                ["штанга"] = "Упражнения со штангой",
                ["гантели"] = "Упражнения с гантелями"
            };

            foreach (var kvp in exercises)
            {
                if (text.Contains(kvp.Key))
                    return kvp.Value;
            }

            return "Общие упражнения";
        }

        private string ExtractEquipment(string text)
        {
            if (text.Contains("штанга")) return "Штанга";
            if (text.Contains("гантели")) return "Гантели";
            if (text.Contains("тренажер")) return "Тренажер";

            return "Не указано";
        }

        private decimal ExtractWeight(string text)
        {
            // Ищем числа перед "кг"
            var weightMatch = Regex.Match(text, @"(\d+)\s*кг");
            if (weightMatch.Success && decimal.TryParse(weightMatch.Groups[1].Value, out var weight))
            {
                return weight;
            }

            return 50; // Default weight
        }

        private string ExtractCardioType(string text)
        {
            var cardioTypes = new Dictionary<string, string>
            {
                ["бег"] = "Бег",
                ["велосипед"] = "Велосипед",
                ["плавание"] = "Плавание",
                ["ходьба"] = "Ходьба"
            };

            foreach (var kvp in cardioTypes)
            {
                if (text.Contains(kvp.Key))
                    return kvp.Value;
            }

            return "Кардио";
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

    // Extension method для ToTitleCase
    public static class StringExtensions
    {
        public static string ToTitleCase(this string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return char.ToUpper(input[0]) + input.Substring(1).ToLower();
        }
    }
}