using System.Net;
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

        public string ProviderName => "Vertex AI (Gemini 1.5 Flash)";

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
                var (url, accessToken) = await GetApiEndpointAsync();
                var base64Image = Convert.ToBase64String(imageData);
                var mimeType = DetectImageType(imageData);

                var prompt = CreateFoodAnalysisPrompt(userPrompt);
                var request = CreateGeminiRequest(prompt, base64Image, mimeType);

                var response = await SendRequestAsync(url, accessToken, request);
                if (!response.IsSuccess)
                {
                    return CreateErrorFoodResponse($"API Error: {response.StatusCode}");
                }

                return ParseFoodResponse(response.Content);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Food analysis failed: {ex.Message}");
                return CreateErrorFoodResponse($"Analysis error: {ex.Message}");
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
                var (url, accessToken) = await GetApiEndpointAsync();
                var images = PrepareBodyImages(frontImageData, sideImageData, backImageData);

                if (!images.Any())
                {
                    return CreateFallbackBodyResponse("No images provided", weight, height, age, gender);
                }

                var prompt = CreateBodyAnalysisPrompt(weight, height, age, gender, goals);
                var request = CreateGeminiRequestWithMultipleImages(prompt, images);

                var response = await SendRequestAsync(url, accessToken, request);
                if (!response.IsSuccess)
                {
                    return CreateFallbackBodyResponse($"API Error: {response.StatusCode}", weight, height, age, gender);
                }

                return ParseBodyResponse(response.Content, weight, height, age, gender);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Body analysis failed: {ex.Message}");
                return CreateFallbackBodyResponse($"Analysis error: {ex.Message}", weight, height, age, gender);
            }
        }

        public async Task<VoiceWorkoutResponse> AnalyzeVoiceWorkoutAsync(byte[] audioData, string? workoutType = null)
        {
            try
            {
                var (url, accessToken) = await GetApiEndpointAsync();
                var base64Audio = Convert.ToBase64String(audioData);
                var mimeType = DetectAudioType(audioData);

                var prompt = CreateVoiceWorkoutPrompt(workoutType);
                var request = CreateGeminiRequestWithAudio(prompt, base64Audio, mimeType);

                var response = await SendRequestAsync(url, accessToken, request);
                if (!response.IsSuccess)
                {
                    return CreateFallbackWorkoutResponse($"API Error: {response.StatusCode}", workoutType);
                }

                return ParseVoiceWorkoutResponse(response.Content, workoutType);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Voice workout analysis failed: {ex.Message}");
                return CreateFallbackWorkoutResponse($"Analysis error: {ex.Message}", workoutType);
            }
        }

        public async Task<VoiceFoodResponse> AnalyzeVoiceFoodAsync(byte[] audioData, string? mealType = null)
        {
            try
            {
                var (url, accessToken) = await GetApiEndpointAsync();
                var base64Audio = Convert.ToBase64String(audioData);
                var mimeType = DetectAudioType(audioData);

                var prompt = CreateVoiceFoodPrompt(mealType);
                var request = CreateGeminiRequestWithAudio(prompt, base64Audio, mimeType);

                var response = await SendRequestAsync(url, accessToken, request);
                if (!response.IsSuccess)
                {
                    return CreateFallbackVoiceFoodResponse($"API Error: {response.StatusCode}", mealType);
                }

                return ParseVoiceFoodResponse(response.Content, mealType);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Voice food analysis failed: {ex.Message}");
                return CreateFallbackVoiceFoodResponse($"Analysis error: {ex.Message}", mealType);
            }
        }

        public async Task<TextWorkoutResponse> AnalyzeTextWorkoutAsync(string workoutText, string? workoutType = null)
        {
            try
            {
                var (url, accessToken) = await GetApiEndpointAsync();
                var prompt = CreateTextWorkoutPrompt(workoutText, workoutType);
                var request = CreateGeminiTextRequest(prompt);

                var response = await SendRequestAsync(url, accessToken, request);
                if (!response.IsSuccess)
                {
                    return CreateFallbackTextWorkoutResponse($"API Error: {response.StatusCode}", workoutType);
                }

                return ParseTextWorkoutResponse(response.Content, workoutType);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Text workout analysis failed: {ex.Message}");
                return CreateFallbackTextWorkoutResponse($"Analysis error: {ex.Message}", workoutType);
            }
        }

        public async Task<TextFoodResponse> AnalyzeTextFoodAsync(string foodText, string? mealType = null)
        {
            try
            {
                var (url, accessToken) = await GetApiEndpointAsync();
                var prompt = CreateTextFoodPrompt(foodText, mealType);
                var request = CreateGeminiTextRequest(prompt);

                var response = await SendRequestAsync(url, accessToken, request);
                if (!response.IsSuccess)
                {
                    return CreateFallbackTextFoodResponse($"API Error: {response.StatusCode}", mealType);
                }

                return ParseTextFoodResponse(response.Content, mealType);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Text food analysis failed: {ex.Message}");
                return CreateFallbackTextFoodResponse($"Analysis error: {ex.Message}", mealType);
            }
        }

        public async Task<FoodCorrectionResponse> CorrectFoodItemAsync(string originalFoodName, string correctionText)
        {
            try
            {
                var (url, accessToken) = await GetApiEndpointAsync();
                var prompt = CreateFoodCorrectionPrompt(originalFoodName, correctionText);
                var request = CreateGeminiTextRequest(prompt);

                var response = await SendRequestAsync(url, accessToken, request);
                if (!response.IsSuccess)
                {
                    return new FoodCorrectionResponse
                    {
                        Success = false,
                        ErrorMessage = $"API Error: {response.StatusCode}"
                    };
                }

                return ParseFoodCorrectionResponse(response.Content);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Food correction failed: {ex.Message}");
                return new FoodCorrectionResponse
                {
                    Success = false,
                    ErrorMessage = $"Correction error: {ex.Message}"
                };
            }
        }

        public async Task<bool> IsHealthyAsync()
        {
            try
            {
                var (url, accessToken) = await GetApiEndpointAsync();
                var request = CreateGeminiTextRequest("Say 'OK' if you are working");

                var response = await SendRequestAsync(url, accessToken, request);
                return response.IsSuccess;
            }
            catch
            {
                return false;
            }
        }

        private string CreateFoodAnalysisPrompt(string? userPrompt)
        {
            return $@"Analyze this food image and return ONLY valid JSON.

{userPrompt ?? ""}

Requirements:
1. For LIQUIDS (soups, drinks, sauces): use ""weightType"": ""ml""
2. For SOLIDS (bread, meat, fruits): use ""weightType"": ""g""
3. If you see packaging labels with exact weight/volume - use that number

JSON format:
{{
  ""foodItems"": [
    {{
      ""name"": ""Food name in Russian"",
      ""estimatedWeight"": 150.0,
      ""weightType"": ""g"",
      ""description"": ""Brief description"",
      ""nutritionPer100g"": {{
        ""calories"": 250.0,
        ""proteins"": 10.0,
        ""fats"": 8.0,
        ""carbs"": 30.0
      }},
      ""totalCalories"": 375,
      ""confidence"": 0.8
    }}
  ],
  ""estimatedCalories"": 375,
  ""fullDescription"": ""Description of all items""
}}

CRITICAL: Return ONLY the JSON object, no other text.";
        }

        private string CreateBodyAnalysisPrompt(decimal? weight, decimal? height, int? age, string? gender, string? goals)
        {
            var w = weight ?? 70;
            var h = height ?? 170;
            var a = age ?? 25;
            var g = gender ?? "not specified";
            var bmr = g.ToLowerInvariant().Contains("male") && !g.ToLowerInvariant().Contains("female")
                ? (int)(10 * (double)w + 6.25 * (double)h - 5 * a + 5)
                : (int)(10 * (double)w + 6.25 * (double)h - 5 * a - 161);

            return $@"Analyze body images. User: {w}kg, {h}cm, {a}y, {g}. IMPORTANT: All text fields must be in Russian. Return JSON only:

{{""bodyAnalysis"":{{""estimatedBodyFatPercentage"":15.0,""estimatedMusclePercentage"":40.0,""bodyType"":""Атлетическое телосложение"",""postureAnalysis"":""Хорошая осанка"",""overallCondition"":""Хорошее состояние"",""bmi"":{Math.Round((double)w / Math.Pow((double)h / 100, 2), 1)},""bmiCategory"":""Нормальный вес"",""estimatedWaistCircumference"":80.0,""estimatedChestCircumference"":100.0,""estimatedHipCircumference"":95.0,""basalMetabolicRate"":{bmr},""metabolicRateCategory"":""Нормальный"",""exerciseRecommendations"":[""Силовые тренировки"",""Кардио упражнения""],""nutritionRecommendations"":[""Сбалансированное питание"",""Достаточное количество белка""],""trainingFocus"":""Общая физическая подготовка""}},""recommendations"":[""Продолжайте тренировки"",""Следите за питанием""],""fullAnalysis"":""Подробный анализ состояния тела на русском языке""}}";
        }

        private string CreateVoiceWorkoutPrompt(string? workoutType)
        {
            return @"Transcribe Russian workout audio. Determine workout type by exercises mentioned:

STRENGTH exercises: жим, приседания, отжимания, планка, подтягивания, качание пресса, махи, выпады, становая тяга
CARDIO exercises: бег, велосипед, кардио, ходьба, плавание, скакалка, прыжки

Return JSON only:

{""transcribedText"":""what you heard"",""workoutData"":{""type"":""strength"",""startTime"":""" + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") + @""",""endTime"":""" + DateTime.UtcNow.AddMinutes(30).ToString("yyyy-MM-ddTHH:mm:ssZ") + @""",""estimatedCalories"":200,""strengthData"":{""name"":""Exercise"",""muscleGroup"":""Muscles"",""equipment"":""None"",""workingWeight"":50,""restTimeSeconds"":90,""sets"":[{""setNumber"":1,""weight"":50,""reps"":10,""isCompleted"":true,""notes"":""Done""}]},""notes"":[""Recorded""]}}";
        }

        private string CreateVoiceFoodPrompt(string? mealType)
        {
            return $@"Transcribe this Russian audio about food and analyze what was eaten.

Expected meal type: {mealType ?? "any meal"}

IMPORTANT: You must return ONLY a valid JSON object, no other text before or after.

Listen for food names in Russian like: борщ, хлеб, мясо, рис, молоко, чай, кофе, сок, вода, etc.

UNITS RULES:
- For LIQUIDS (жидкости): супы, борщ, чай, кофе, молоко, сок, вода, компот, бульон, соусы, напитки → use ""weightType"": ""ml""
- For SOLIDS (твердая пища): хлеб, мясо, рыба, фрукты, овощи, каши, макароны, крупы → use ""weightType"": ""g""

JSON format to return:
{{
  ""transcribedText"": ""точный текст что услышали на русском"",
  ""foodItems"": [
    {{
      ""name"": ""Название блюда на русском"",
      ""estimatedWeight"": 200.0,
      ""weightType"": ""ml или g в зависимости от типа продукта"",
      ""description"": ""Описание блюда"",
      ""nutritionPer100g"": {{
        ""calories"": 200.0,
        ""proteins"": 15.0,
        ""fats"": 10.0,
        ""carbs"": 20.0
      }},
      ""totalCalories"": 400,
      ""confidence"": 0.7
    }}
  ],
  ""estimatedTotalCalories"": 400
}}

CRITICAL: Return ONLY the JSON object above, nothing else.";
        }

        private string CreateTextWorkoutPrompt(string workoutText, string? workoutType)
        {
            return $@"Analyze this workout description: ""{workoutText}""

Workout type: {workoutType ?? "auto-detect"}

Return ONLY this JSON:
{{
  ""processedText"": ""Processed description"",
  ""workoutData"": {{
    ""type"": ""strength"",
    ""startTime"": ""2025-07-23T17:00:00Z"",
    ""endTime"": ""2025-07-23T17:45:00Z"",
    ""estimatedCalories"": 200,
    ""strengthData"": {{
      ""name"": ""Exercise name"",
      ""muscleGroup"": ""Target muscles"",
      ""equipment"": ""Equipment"",
      ""workingWeight"": 40.0,
      ""restTimeSeconds"": 90,
      ""sets"": [{{
        ""setNumber"": 1,
        ""weight"": 40.0,
        ""reps"": 12,
        ""isCompleted"": true,
        ""notes"": ""Set completed""
      }}]
    }},
    ""notes"": [""Workout logged""]
  }}
}}";
        }

        private string CreateTextFoodPrompt(string foodText, string? mealType)
        {
            return $@"Analyze this food description: ""{foodText}""

Meal type: {mealType ?? "any"}

Return ONLY this JSON:
{{
  ""processedText"": ""Processed description"",
  ""foodItems"": [
    {{
      ""name"": ""Food name in Russian"",
      ""estimatedWeight"": 150.0,
      ""weightType"": ""g"",
      ""description"": ""Food description"",
      ""nutritionPer100g"": {{
        ""calories"": 250.0,
        ""proteins"": 12.0,
        ""fats"": 8.0,
        ""carbs"": 35.0
      }},
      ""totalCalories"": 375,
      ""confidence"": 0.8
    }}
  ],
  ""estimatedTotalCalories"": 375
}}";
        }

        private string CreateFoodCorrectionPrompt(string originalFoodName, string correctionText)
        {
            return $@"Correct this food item: ""{originalFoodName}"" with: ""{correctionText}""

Return ONLY this JSON:
{{
  ""correctedFoodItem"": {{
    ""name"": ""Updated food name"",
    ""estimatedWeight"": 180.0,
    ""weightType"": ""g"",
    ""description"": ""Updated description"",
    ""nutritionPer100g"": {{
      ""calories"": 280.0,
      ""proteins"": 14.0,
      ""fats"": 12.0,
      ""carbs"": 32.0
    }},
    ""totalCalories"": 504,
    ""confidence"": 0.85
  }},
  ""correctionExplanation"": ""How the correction was applied"",
  ""ingredients"": [""ingredient1"", ""ingredient2""]
}}";
        }

        private async Task<(string url, string accessToken)> GetApiEndpointAsync()
        {
            var projectId = _configuration["GoogleCloud:ProjectId"];
            var location = _configuration["GoogleCloud:Location"] ?? "us-central1";
            var model = _configuration["GoogleCloud:Model"] ?? "gemini-1.5-flash";

            if (string.IsNullOrEmpty(projectId))
                throw new InvalidOperationException("GoogleCloud:ProjectId not configured");

            var accessToken = await _tokenService.GetAccessTokenAsync();
            var url = $"https://{location}-aiplatform.googleapis.com/v1/projects/{projectId}/locations/{location}/publishers/google/models/{model}:generateContent";

            return (url, accessToken);
        }

        private object CreateGeminiRequest(string prompt, string base64Image, string mimeType)
        {
            return new
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
                    top_p = 0.8, 
                    candidate_count = 1
                },
                safety_settings = new[]
                {
            new { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_NONE" },
            new { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_NONE" },
            new { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_NONE" },
            new { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_NONE" }
        }
            };
        }

        private object CreateGeminiRequestWithMultipleImages(string prompt, List<(string base64, string mimeType)> images)
        {
            var parts = new List<object> { new { text = prompt } };

            foreach (var (base64, mimeType) in images)
            {
                parts.Add(new
                {
                    inline_data = new
                    {
                        mime_type = mimeType,
                        data = base64
                    }
                });
            }

            return new
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
                    top_p = 0.8, 
                    candidate_count = 1
                },
                safety_settings = new[]
                {
            new { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_NONE" },
            new { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_NONE" },
            new { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_NONE" },
            new { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_NONE" }
        }
            };
        }

        private object CreateGeminiRequestWithAudio(string prompt, string base64Audio, string mimeType)
        {
            return new
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
                    top_p = 0.8, 
                    candidate_count = 1
                }
            };
        }

        private object CreateGeminiTextRequest(string prompt)
        {
            return new
            {
                contents = new[]
                {
            new
            {
                role = "user",
                parts = new[] { new { text = prompt } }
            }
        },
                generation_config = new
                {
                    temperature = 0.1,
                    top_p = 0.8, 
                    candidate_count = 1
                }
            };
        }

        private async Task<(bool IsSuccess, string Content, int StatusCode)> SendRequestAsync(string url, string accessToken, object request)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                var responseText = await response.Content.ReadAsStringAsync();

                return (response.IsSuccessStatusCode, responseText, (int)response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Request failed: {ex.Message}");
                return (false, ex.Message, 500);
            }
        }

        private string DetectImageType(byte[] imageData)
        {
            if (imageData.Length >= 2)
            {
                if (imageData[0] == 0xFF && imageData[1] == 0xD8) return "image/jpeg";
                if (imageData.Length >= 8 && imageData[0] == 0x89 && imageData[1] == 0x50) return "image/png";
                if (imageData.Length >= 6 && imageData[0] == 0x47 && imageData[1] == 0x49) return "image/gif";
            }
            return "image/jpeg";
        }

        private string DetectAudioType(byte[] audioData)
        {
            if (audioData.Length >= 4)
            {
                if (audioData[0] == 0x52 && audioData[1] == 0x49) return "audio/wav";
                if (audioData[0] == 0xFF && (audioData[1] & 0xE0) == 0xE0) return "audio/mp3";
                if (audioData[0] == 0x4F && audioData[1] == 0x67) return "audio/ogg";
            }
            return "audio/ogg";
        }

        private List<(string base64, string mimeType)> PrepareBodyImages(byte[]? front, byte[]? side, byte[]? back)
        {
            var images = new List<(string, string)>();

            if (front != null && front.Length > 0)
                images.Add((Convert.ToBase64String(front), DetectImageType(front)));

            if (side != null && side.Length > 0)
                images.Add((Convert.ToBase64String(side), DetectImageType(side)));

            if (back != null && back.Length > 0)
                images.Add((Convert.ToBase64String(back), DetectImageType(back)));

            return images;
        }

        // ПАРСЕРЫ ОТВЕТОВ
        private FoodScanResponse ParseFoodResponse(string responseText)
        {
            try
            {
                using var document = JsonDocument.Parse(responseText);
                var candidates = document.RootElement.GetProperty("candidates");
                var firstCandidate = candidates[0];
                var content = firstCandidate.GetProperty("content");
                var parts = content.GetProperty("parts");
                var text = parts[0].GetProperty("text").GetString() ?? "";

                var startIndex = text.IndexOf('{');
                var lastIndex = text.LastIndexOf('}');

                if (startIndex >= 0 && lastIndex > startIndex)
                {
                    var jsonText = text.Substring(startIndex, lastIndex - startIndex + 1);
                    using var foodDoc = JsonDocument.Parse(jsonText);
                    var root = foodDoc.RootElement;

                    var foodItems = new List<FoodItemResponse>();
                    if (root.TryGetProperty("foodItems", out var items))
                    {
                        foreach (var item in items.EnumerateArray())
                        {
                            foodItems.Add(new FoodItemResponse
                            {
                                Name = GetString(item, "name"),
                                EstimatedWeight = GetDecimal(item, "estimatedWeight"),
                                WeightType = GetString(item, "weightType", "g"),
                                Description = GetString(item, "description"),
                                NutritionPer100g = ParseNutrition(item, "nutritionPer100g"),
                                TotalCalories = GetInt(item, "totalCalories"),
                                Confidence = GetDecimal(item, "confidence", 0.8m)
                            });
                        }
                    }

                    return new FoodScanResponse
                    {
                        Success = true,
                        FoodItems = foodItems,
                        EstimatedCalories = GetInt(root, "estimatedCalories"),
                        FullDescription = GetString(root, "fullDescription")
                    };
                }

                return CreateErrorFoodResponse("Invalid JSON format");
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Parse error: {ex.Message}");
                return CreateErrorFoodResponse($"Parse error: {ex.Message}");
            }
        }

        private BodyScanResponse ParseBodyResponse(string responseText, decimal? weight, decimal? height, int? age, string? gender)
        {
            try
            {
                _logger.LogInformation($"💪 Raw Gemini body response: {responseText}");

                using var document = JsonDocument.Parse(responseText);
                var root = document.RootElement;

                string fullText = "";

                // Безопасное извлечение текста
                if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                {
                    var firstCandidate = candidates[0];

                    // Проверяем finishReason
                    if (firstCandidate.TryGetProperty("finishReason", out var finishReason))
                    {
                        var reason = finishReason.GetString();
                        if (reason == "MAX_TOKENS")
                        {
                            _logger.LogWarning($"💪 Body analysis response cut off due to MAX_TOKENS");
                            return CreateFallbackBodyResponse("Response cut off - MAX_TOKENS limit", weight, height, age, gender);
                        }
                    }

                    if (firstCandidate.TryGetProperty("content", out var content))
                    {
                        if (content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                        {
                            var firstPart = parts[0];
                            if (firstPart.TryGetProperty("text", out var textProperty))
                            {
                                fullText = textProperty.GetString() ?? "";
                            }
                        }
                    }
                }

                _logger.LogInformation($"💪 Extracted body text: {fullText}");

                if (string.IsNullOrEmpty(fullText))
                {
                    return CreateFallbackBodyResponse("Empty response text", weight, height, age, gender);
                }

                var jsonStart = fullText.IndexOf('{');
                var jsonEnd = fullText.LastIndexOf('}');

                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var jsonText = fullText.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    _logger.LogInformation($"💪 Extracted JSON: {jsonText}");

                    jsonText = FixInvalidJson(jsonText);
                    _logger.LogInformation($"💪 Fixed JSON: {jsonText}");

                    using var bodyDoc = JsonDocument.Parse(jsonText);
                    var jsonRoot = bodyDoc.RootElement;

                    var bodyAnalysis = new BodyAnalysisDto();
                    if (jsonRoot.TryGetProperty("bodyAnalysis", out var analysis))
                    {
                        bodyAnalysis.EstimatedBodyFatPercentage = GetDecimal(analysis, "estimatedBodyFatPercentage");
                        bodyAnalysis.EstimatedMusclePercentage = GetDecimal(analysis, "estimatedMusclePercentage");
                        bodyAnalysis.BodyType = GetString(analysis, "bodyType");
                        bodyAnalysis.PostureAnalysis = GetString(analysis, "postureAnalysis");
                        bodyAnalysis.OverallCondition = GetString(analysis, "overallCondition");
                        bodyAnalysis.BMI = GetDecimal(analysis, "bmi");
                        bodyAnalysis.BMICategory = GetString(analysis, "bmiCategory");
                        bodyAnalysis.EstimatedWaistCircumference = GetDecimal(analysis, "estimatedWaistCircumference");
                        bodyAnalysis.EstimatedChestCircumference = GetDecimal(analysis, "estimatedChestCircumference");
                        bodyAnalysis.EstimatedHipCircumference = GetDecimal(analysis, "estimatedHipCircumference");
                        bodyAnalysis.BasalMetabolicRate = GetInt(analysis, "basalMetabolicRate");
                        bodyAnalysis.MetabolicRateCategory = GetString(analysis, "metabolicRateCategory");
                        bodyAnalysis.TrainingFocus = GetString(analysis, "trainingFocus");
                        bodyAnalysis.ExerciseRecommendations = GetStringArray(analysis, "exerciseRecommendations");
                        bodyAnalysis.NutritionRecommendations = GetStringArray(analysis, "nutritionRecommendations");
                    }

                    return new BodyScanResponse
                    {
                        Success = true,
                        BodyAnalysis = bodyAnalysis,
                        Recommendations = GetStringArray(jsonRoot, "recommendations"),
                        FullAnalysis = GetString(jsonRoot, "fullAnalysis")
                    };
                }

                return CreateFallbackBodyResponse("No JSON found in response", weight, height, age, gender);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Body parse error: {ex.Message}");
                return CreateFallbackBodyResponse($"Parse error: {ex.Message}", weight, height, age, gender);
            }
        }

        private VoiceWorkoutResponse ParseVoiceWorkoutResponse(string responseText, string? workoutType)
        {
            try
            {
                _logger.LogInformation($"🎤 Raw Gemini response: {responseText}");

                using var document = JsonDocument.Parse(responseText);
                var root = document.RootElement;

                string fullText = "";

                if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                {
                    var firstCandidate = candidates[0];

                    if (firstCandidate.TryGetProperty("finishReason", out var finishReason))
                    {
                        var reason = finishReason.GetString();
                        _logger.LogInformation($"🎤 Finish reason: {reason}");

                        if (reason == "MAX_TOKENS")
                        {
                            _logger.LogWarning($"🎤 Response was cut off due to MAX_TOKENS limit");
                            return CreateFallbackWorkoutResponse("Response cut off - MAX_TOKENS limit reached", workoutType);
                        }
                    }

                    if (firstCandidate.TryGetProperty("content", out var content))
                    {
                        if (content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                        {
                            var firstPart = parts[0];
                            if (firstPart.TryGetProperty("text", out var textProperty))
                            {
                                fullText = textProperty.GetString() ?? "";
                            }
                        }
                        else
                        {
                            _logger.LogWarning($"🎤 No 'parts' field found in content");
                            return CreateFallbackWorkoutResponse("No parts field in response", workoutType);
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"🎤 No 'content' field found in candidate");
                        return CreateFallbackWorkoutResponse("No content field in response", workoutType);
                    }
                }
                else
                {
                    _logger.LogWarning($"🎤 No 'candidates' field found or empty");
                    return CreateFallbackWorkoutResponse("No candidates in response", workoutType);
                }

                _logger.LogInformation($"🎤 Extracted text: {fullText}");

                if (string.IsNullOrEmpty(fullText))
                {
                    _logger.LogWarning($"🎤 Empty text extracted from response");
                    return CreateFallbackWorkoutResponse("Empty response text", workoutType);
                }

                var jsonStart = fullText.IndexOf('{');
                var jsonEnd = fullText.LastIndexOf('}');

                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var jsonText = fullText.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    _logger.LogInformation($"🎤 Extracted JSON: {jsonText}");

                    using var workoutDoc = JsonDocument.Parse(jsonText);
                    var jsonRoot = workoutDoc.RootElement;

                    var response = new VoiceWorkoutResponse
                    {
                        Success = true,
                        TranscribedText = GetString(jsonRoot, "transcribedText")
                    };

                    if (jsonRoot.TryGetProperty("workoutData", out var workoutData))
                    {
                        response.WorkoutData = new WorkoutDataResponse
                        {
                            Type = GetString(workoutData, "type", "strength"),
                            StartTime = GetDateTime(workoutData, "startTime"),
                            EndTime = GetDateTime(workoutData, "endTime"),
                            EstimatedCalories = GetInt(workoutData, "estimatedCalories"),
                            Notes = GetStringArray(workoutData, "notes")
                        };

                        if (workoutData.TryGetProperty("strengthData", out var strengthData))
                        {
                            response.WorkoutData.StrengthData = ParseStrengthData(strengthData);
                        }
                    }

                    return response;
                }
                else
                {
                    _logger.LogWarning($"🎤 No JSON found in response text");
                    return CreateFallbackWorkoutResponse("No JSON found in response", workoutType);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Voice workout parse error: {ex.Message}");
                return CreateFallbackWorkoutResponse($"Parse error: {ex.Message}", workoutType);
            }
        }

        private string FixInvalidJson(string jsonText)
        {
            try
            {
                jsonText = jsonText.Trim();

                int openBraces = jsonText.Count(c => c == '{');
                int closeBraces = jsonText.Count(c => c == '}');
                int openBrackets = jsonText.Count(c => c == '[');
                int closeBrackets = jsonText.Count(c => c == ']');

                while (closeBraces < openBraces)
                {
                    jsonText += "}";
                    closeBraces++;
                }

                while (closeBrackets < openBrackets)
                {
                    jsonText += "]";
                    closeBrackets++;
                }

                var lastBrace = jsonText.LastIndexOf('}');
                if (lastBrace > 0 && lastBrace < jsonText.Length - 1)
                {
                    jsonText = jsonText.Substring(0, lastBrace + 1);
                }

                return jsonText;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error fixing JSON: {ex.Message}");
                return jsonText; 
            }
        }

        private VoiceFoodResponse ParseVoiceFoodResponse(string responseText, string? mealType)
        {
            try
            {
                var jsonText = ExtractJsonFromResponse(responseText);
                if (string.IsNullOrEmpty(jsonText))
                    return CreateFallbackVoiceFoodResponse("No JSON found", mealType);

                using var document = JsonDocument.Parse(jsonText);
                var root = document.RootElement;

                var foodItems = new List<FoodItemResponse>();
                if (root.TryGetProperty("foodItems", out var items))
                {
                    foreach (var item in items.EnumerateArray())
                    {
                        foodItems.Add(new FoodItemResponse
                        {
                            Name = GetString(item, "name"),
                            EstimatedWeight = GetDecimal(item, "estimatedWeight"),
                            WeightType = GetString(item, "weightType", "g"),
                            Description = GetString(item, "description"),
                            NutritionPer100g = ParseNutrition(item, "nutritionPer100g"),
                            TotalCalories = GetInt(item, "totalCalories"),
                            Confidence = GetDecimal(item, "confidence", 0.7m)
                        });
                    }
                }

                return new VoiceFoodResponse
                {
                    Success = true,
                    TranscribedText = GetString(root, "transcribedText"),
                    FoodItems = foodItems,
                    EstimatedTotalCalories = GetInt(root, "estimatedTotalCalories")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Voice food parse error: {ex.Message}");
                return CreateFallbackVoiceFoodResponse($"Parse error: {ex.Message}", mealType);
            }
        }

        private TextWorkoutResponse ParseTextWorkoutResponse(string responseText, string? workoutType)
        {
            try
            {
                var jsonText = ExtractJsonFromResponse(responseText);
                if (string.IsNullOrEmpty(jsonText))
                    return CreateFallbackTextWorkoutResponse("No JSON found", workoutType);

                using var document = JsonDocument.Parse(jsonText);
                var root = document.RootElement;

                var response = new TextWorkoutResponse
                {
                    Success = true,
                    ProcessedText = GetString(root, "processedText")
                };

                if (root.TryGetProperty("workoutData", out var workoutData))
                {
                    response.WorkoutData = new WorkoutDataResponse
                    {
                        Type = GetString(workoutData, "type", "strength"),
                        StartTime = GetDateTime(workoutData, "startTime"),
                        EndTime = GetDateTime(workoutData, "endTime"),
                        EstimatedCalories = GetInt(workoutData, "estimatedCalories"),
                        Notes = GetStringArray(workoutData, "notes")
                    };

                    if (workoutData.TryGetProperty("strengthData", out var strengthData))
                    {
                        response.WorkoutData.StrengthData = ParseStrengthData(strengthData);
                    }

                    if (workoutData.TryGetProperty("cardioData", out var cardioData))
                    {
                        response.WorkoutData.CardioData = ParseCardioData(cardioData);
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Text workout parse error: {ex.Message}");
                return CreateFallbackTextWorkoutResponse($"Parse error: {ex.Message}", workoutType);
            }
        }

        private TextFoodResponse ParseTextFoodResponse(string responseText, string? mealType)
        {
            try
            {
                var jsonText = ExtractJsonFromResponse(responseText);
                if (string.IsNullOrEmpty(jsonText))
                    return CreateFallbackTextFoodResponse("No JSON found", mealType);

                using var document = JsonDocument.Parse(jsonText);
                var root = document.RootElement;

                var foodItems = new List<FoodItemResponse>();
                if (root.TryGetProperty("foodItems", out var items))
                {
                    foreach (var item in items.EnumerateArray())
                    {
                        foodItems.Add(new FoodItemResponse
                        {
                            Name = GetString(item, "name"),
                            EstimatedWeight = GetDecimal(item, "estimatedWeight"),
                            WeightType = GetString(item, "weightType", "g"),
                            Description = GetString(item, "description"),
                            NutritionPer100g = ParseNutrition(item, "nutritionPer100g"),
                            TotalCalories = GetInt(item, "totalCalories"),
                            Confidence = GetDecimal(item, "confidence", 0.8m)
                        });
                    }
                }

                return new TextFoodResponse
                {
                    Success = true,
                    ProcessedText = GetString(root, "processedText"),
                    FoodItems = foodItems,
                    EstimatedTotalCalories = GetInt(root, "estimatedTotalCalories")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Text food parse error: {ex.Message}");
                return CreateFallbackTextFoodResponse($"Parse error: {ex.Message}", mealType);
            }
        }

        private FoodCorrectionResponse ParseFoodCorrectionResponse(string responseText)
        {
            try
            {
                var jsonText = ExtractJsonFromResponse(responseText);
                if (string.IsNullOrEmpty(jsonText))
                {
                    return new FoodCorrectionResponse
                    {
                        Success = false,
                        ErrorMessage = "No JSON found in response"
                    };
                }

                using var document = JsonDocument.Parse(jsonText);
                var root = document.RootElement;

                var correctedItem = new FoodItemResponse();
                if (root.TryGetProperty("correctedFoodItem", out var foodItem))
                {
                    correctedItem = new FoodItemResponse
                    {
                        Name = GetString(foodItem, "name"),
                        EstimatedWeight = GetDecimal(foodItem, "estimatedWeight"),
                        WeightType = GetString(foodItem, "weightType", "g"),
                        Description = GetString(foodItem, "description"),
                        NutritionPer100g = ParseNutrition(foodItem, "nutritionPer100g"),
                        TotalCalories = GetInt(foodItem, "totalCalories"),
                        Confidence = GetDecimal(foodItem, "confidence", 0.8m)
                    };
                }

                return new FoodCorrectionResponse
                {
                    Success = true,
                    CorrectedFoodItem = correctedItem,
                    CorrectionExplanation = GetString(root, "correctionExplanation"),
                    Ingredients = GetStringArray(root, "ingredients")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Food correction parse error: {ex.Message}");
                return new FoodCorrectionResponse
                {
                    Success = false,
                    ErrorMessage = $"Parse error: {ex.Message}"
                };
            }
        }

        // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ПАРСИНГА
        private string ExtractJsonFromResponse(string responseText)
        {
            try
            {
                using var document = JsonDocument.Parse(responseText);
                var candidates = document.RootElement.GetProperty("candidates");
                var firstCandidate = candidates[0];
                var content = firstCandidate.GetProperty("content");
                var parts = content.GetProperty("parts");
                var text = parts[0].GetProperty("text").GetString() ?? "";

                var startIndex = text.IndexOf('{');
                var lastIndex = text.LastIndexOf('}');

                if (startIndex >= 0 && lastIndex > startIndex)
                {
                    return text.Substring(startIndex, lastIndex - startIndex + 1);
                }

                return "";
            }
            catch
            {
                return "";
            }
        }

        private string GetString(JsonElement element, string propertyName, string defaultValue = "")
        {
            try
            {
                return element.TryGetProperty(propertyName, out var prop) ?
                    prop.GetString() ?? defaultValue : defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        private int GetInt(JsonElement element, string propertyName, int defaultValue = 0)
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

        private decimal GetDecimal(JsonElement element, string propertyName, decimal defaultValue = 0)
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

        private DateTime GetDateTime(JsonElement element, string propertyName)
        {
            try
            {
                if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
                {
                    var dateString = prop.GetString();
                    if (!string.IsNullOrEmpty(dateString) && DateTime.TryParse(dateString, out var parsedDate))
                        return parsedDate;
                }
                return DateTime.UtcNow;
            }
            catch
            {
                return DateTime.UtcNow;
            }
        }

        private List<string> GetStringArray(JsonElement element, string propertyName)
        {
            try
            {
                if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Array)
                {
                    return prop.EnumerateArray()
                        .Select(x => x.GetString() ?? "")
                        .Where(x => !string.IsNullOrEmpty(x))
                        .ToList();
                }
                return new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        private NutritionPer100gDto ParseNutrition(JsonElement element, string propertyName)
        {
            try
            {
                if (element.TryGetProperty(propertyName, out var nutrition))
                {
                    return new NutritionPer100gDto
                    {
                        Calories = GetDecimal(nutrition, "calories"),
                        Proteins = GetDecimal(nutrition, "proteins"),
                        Fats = GetDecimal(nutrition, "fats"),
                        Carbs = GetDecimal(nutrition, "carbs")
                    };
                }
                return new NutritionPer100gDto();
            }
            catch
            {
                return new NutritionPer100gDto();
            }
        }

        private StrengthDataDto ParseStrengthData(JsonElement strengthData)
        {
            try
            {
                var sets = new List<StrengthSetDto>();
                if (strengthData.TryGetProperty("sets", out var setsArray) && setsArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var setElement in setsArray.EnumerateArray())
                    {
                        sets.Add(new StrengthSetDto
                        {
                            SetNumber = GetInt(setElement, "setNumber", sets.Count + 1),
                            Weight = GetDecimal(setElement, "weight"),
                            Reps = GetInt(setElement, "reps"),
                            IsCompleted = GetBool(setElement, "isCompleted", true),
                            Notes = GetString(setElement, "notes")
                        });
                    }
                }

                return new StrengthDataDto
                {
                    Name = GetString(strengthData, "name", "Exercise"),
                    MuscleGroup = GetString(strengthData, "muscleGroup", "General"),
                    Equipment = GetString(strengthData, "equipment", "None"),
                    WorkingWeight = GetDecimal(strengthData, "workingWeight"),
                    RestTimeSeconds = GetInt(strengthData, "restTimeSeconds", 120),
                    Sets = sets
                };
            }
            catch
            {
                return new StrengthDataDto
                {
                    Name = "Exercise",
                    MuscleGroup = "General",
                    Equipment = "None",
                    Sets = new List<StrengthSetDto>()
                };
            }
        }

        private CardioDataDto ParseCardioData(JsonElement cardioData)
        {
            try
            {
                return new CardioDataDto
                {
                    CardioType = GetString(cardioData, "cardioType", "Cardio"),
                    DistanceKm = GetNullableDecimal(cardioData, "distanceKm"),
                    AvgPulse = GetNullableInt(cardioData, "avgPulse"),
                    MaxPulse = GetNullableInt(cardioData, "maxPulse"),
                    AvgPace = GetString(cardioData, "avgPace")
                };
            }
            catch
            {
                return new CardioDataDto
                {
                    CardioType = "Cardio"
                };
            }
        }

        private bool GetBool(JsonElement element, string propertyName, bool defaultValue = false)
        {
            try
            {
                return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.True
                    ? prop.GetBoolean()
                    : defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        private int? GetNullableInt(JsonElement element, string propertyName)
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
                return null;
            }
            catch
            {
                return null;
            }
        }

        private decimal? GetNullableDecimal(JsonElement element, string propertyName)
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
                return null;
            }
            catch
            {
                return null;
            }
        }

        // FALLBACK МЕТОДЫ
        private FoodScanResponse CreateErrorFoodResponse(string reason)
        {
            return new FoodScanResponse
            {
                Success = false,
                ErrorMessage = $"Не удалось проанализировать изображение: {reason}",
                FoodItems = new List<FoodItemResponse>(),
                EstimatedCalories = 0,
                FullDescription = "Анализ не выполнен"
            };
        }

        private BodyScanResponse CreateFallbackBodyResponse(string reason, decimal? weight, decimal? height, int? age, string? gender)
        {
            decimal bmi = 22.5m;
            int bmr = 1600;

            if (weight.HasValue && height.HasValue && weight > 0 && height > 0)
            {
                var heightInMeters = height.Value / 100;
                bmi = weight.Value / (heightInMeters * heightInMeters);

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
            }

            return new BodyScanResponse
            {
                Success = true,
                BodyAnalysis = new BodyAnalysisDto
                {
                    EstimatedBodyFatPercentage = 15m,
                    EstimatedMusclePercentage = 40m,
                    BodyType = "Среднее телосложение",
                    PostureAnalysis = "Анализ осанки недоступен",
                    OverallCondition = $"Анализ недоступен ({reason})",
                    BMI = Math.Round(bmi, 1),
                    BMICategory = bmi < 18.5m ? "Недостаточный вес" : bmi < 25m ? "Нормальный вес" : bmi < 30m ? "Избыточный вес" : "Ожирение",
                    EstimatedWaistCircumference = 80m,
                    EstimatedChestCircumference = 100m,
                    EstimatedHipCircumference = 95m,
                    BasalMetabolicRate = bmr,
                    MetabolicRateCategory = bmr < 1400 ? "Низкий" : bmr > 2000 ? "Высокий" : "Нормальный",
                    ExerciseRecommendations = new List<string> { "Регулярные упражнения", "Кардио нагрузки" },
                    NutritionRecommendations = new List<string> { "Сбалансированное питание", "Достаточное количество воды" },
                    TrainingFocus = "Общая физическая подготовка"
                },
                Recommendations = new List<string>
                {
                    "Рекомендуем повторить анализ с качественными фотографиями",
                    "Обратитесь к специалисту для точной оценки"
                },
                FullAnalysis = $"Автоматический анализ: {reason}"
            };
        }

        private VoiceWorkoutResponse CreateFallbackWorkoutResponse(string reason, string? workoutType)
        {
            var type = workoutType?.ToLowerInvariant() == "cardio" ? "cardio" : "strength";
            var startTime = DateTime.UtcNow;
            var endTime = startTime.AddMinutes(type == "cardio" ? 30 : 45);

            var workoutData = new WorkoutDataResponse
            {
                Type = type,
                StartTime = startTime,
                EndTime = endTime,
                EstimatedCalories = type == "cardio" ? 200 : 250,
                Notes = new List<string> { $"Автоматически созданная тренировка ({reason})" }
            };

            if (type == "strength")
            {
                workoutData.StrengthData = new StrengthDataDto
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
                workoutData.CardioData = new CardioDataDto
                {
                    CardioType = "Общее кардио"
                };
            }

            return new VoiceWorkoutResponse
            {
                Success = true,
                TranscribedText = $"Не удалось распознать аудио ({reason}), создана базовая тренировка",
                WorkoutData = workoutData
            };
        }

        private VoiceFoodResponse CreateFallbackVoiceFoodResponse(string reason, string? mealType)
        {
            var defaultFood = GetDefaultFoodForMeal(mealType);

            return new VoiceFoodResponse
            {
                Success = true,
                TranscribedText = $"Не удалось распознать аудио ({reason}), создана запись о питании",
                FoodItems = new List<FoodItemResponse> { defaultFood },
                EstimatedTotalCalories = defaultFood.TotalCalories
            };
        }

        private TextWorkoutResponse CreateFallbackTextWorkoutResponse(string reason, string? workoutType)
        {
            var fallbackWorkout = CreateFallbackWorkoutResponse(reason, workoutType);

            return new TextWorkoutResponse
            {
                Success = true,
                ProcessedText = $"Не удалось обработать текст ({reason}), создана базовая тренировка",
                WorkoutData = fallbackWorkout.WorkoutData
            };
        }

        private TextFoodResponse CreateFallbackTextFoodResponse(string reason, string? mealType)
        {
            var defaultFood = GetDefaultFoodForMeal(mealType);

            return new TextFoodResponse
            {
                Success = true,
                ProcessedText = $"Не удалось обработать текст ({reason}), создана запись о питании",
                FoodItems = new List<FoodItemResponse> { defaultFood },
                EstimatedTotalCalories = defaultFood.TotalCalories
            };
        }

        private FoodItemResponse GetDefaultFoodForMeal(string? mealType)
        {
            var currentHour = DateTime.Now.Hour;

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

            var (name, calories, proteins, fats, carbs, weight, weightType) = mealType.ToLowerInvariant() switch
            {
                "breakfast" or "завтрак" => ("Завтрак", 250m, 12m, 8m, 35m, 200m, "g"),
                "lunch" or "обед" => ("Обед", 400m, 25m, 15m, 45m, 300m, "g"),
                "dinner" or "ужин" => ("Ужин", 350m, 20m, 12m, 40m, 250m, "g"),
                "snack" or "перекус" => ("Перекус", 150m, 5m, 6m, 20m, 100m, "g"),
                _ => ("Блюдо", 200m, 10m, 8m, 25m, 150m, "g")
            };

            return new FoodItemResponse
            {
                Name = name,
                EstimatedWeight = weight,
                WeightType = weightType,
                Description = "Автоматически созданная запись",
                NutritionPer100g = new NutritionPer100gDto
                {
                    Calories = calories,
                    Proteins = proteins,
                    Fats = fats,
                    Carbs = carbs
                },
                TotalCalories = (int)Math.Round((calories * weight) / 100),
                Confidence = 0.3m
            };
        }
    }
}