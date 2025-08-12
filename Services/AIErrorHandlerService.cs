using FitnessTracker.API.DTOs;

namespace FitnessTracker.API.Services.AI
{
    public interface IAIErrorHandlerService
    {
        FoodScanResponse CreateFallbackFoodResponse(string reason, byte[]? imageData = null);
        VoiceWorkoutResponse CreateFallbackWorkoutResponse(string reason, string? workoutType = null);
        VoiceFoodResponse CreateFallbackVoiceFoodResponse(string reason, string? mealType = null);
        BodyScanResponse CreateFallbackBodyResponse(string reason);
        TextWorkoutResponse CreateFallbackTextWorkoutResponse(string reason, string? workoutType = null);
        TextFoodResponse CreateFallbackTextFoodResponse(string reason, string? mealType = null);
        bool ShouldRetryRequest(Exception ex, int currentAttempt);
    }

    public class AIErrorHandlerService : IAIErrorHandlerService
    {
        private readonly ILogger<AIErrorHandlerService> _logger;

        public AIErrorHandlerService(ILogger<AIErrorHandlerService> logger)
        {
            _logger = logger;
        }

        public FoodScanResponse CreateFallbackFoodResponse(string reason, byte[]? imageData = null)
        {
            _logger.LogInformation($"🍎 Food analysis failed: {reason}");

            var noFoodKeywords = new[]
            {
                "не обнаружены продукты питания",
                "не содержит еду",
                "не является едой",
                "no food detected"
            };

            bool isNoFoodDetected = noFoodKeywords.Any(keyword =>
                reason.ToLowerInvariant().Contains(keyword.ToLowerInvariant()));

            if (isNoFoodDetected)
            {
                return new FoodScanResponse
                {
                    Success = false,
                    ErrorMessage = "На изображении не обнаружены продукты питания. Пожалуйста, сфотографируйте еду или продукты.",
                    FoodItems = new List<FoodItemResponse>(),
                    EstimatedCalories = 0,
                    FullDescription = "Еда не найдена на изображении"
                };
            }

            return new FoodScanResponse
            {
                Success = false,
                ErrorMessage = $"Техническая ошибка анализа: {reason}. Попробуйте сделать более четкое фото еды.",
                FoodItems = new List<FoodItemResponse>(),
                EstimatedCalories = 0,
                FullDescription = "Анализ не выполнен из-за технической ошибки"
            };
        }

        public VoiceWorkoutResponse CreateFallbackWorkoutResponse(string reason, string? workoutType = null)
        {
            _logger.LogInformation($"🎤 Workout analysis failed: {reason}");

            var noWorkoutKeywords = new[]
            {
                "не удалось распознать информацию о тренировке",
                "отсутствует речь",
                "только фоновые звуки",
                "тишина",
                "не о тренировках"
            };

            bool isNoWorkoutDetected = noWorkoutKeywords.Any(keyword =>
                reason.ToLowerInvariant().Contains(keyword.ToLowerInvariant()));

            if (isNoWorkoutDetected)
            {
                return new VoiceWorkoutResponse
                {
                    Success = false,
                    ErrorMessage = "Не удалось распознать информацию о тренировке в аудиозаписи. Убедитесь, что говорите о физических упражнениях.",
                    TranscribedText = "Информация о тренировке не найдена",
                    WorkoutData = null
                };
            }

            return new VoiceWorkoutResponse
            {
                Success = false,
                ErrorMessage = $"Техническая ошибка анализа: {reason}. Попробуйте записать аудио заново.",
                TranscribedText = "Анализ не выполнен",
                WorkoutData = null
            };
        }


        public VoiceFoodResponse CreateFallbackVoiceFoodResponse(string reason, string? mealType = null)
        {
            _logger.LogInformation($"🗣️ Food voice analysis failed: {reason}");

            var noFoodKeywords = new[]
            {
                "не удалось распознать информацию о питании",
                "отсутствует речь",
                "только фоновые звуки",
                "тишина",
                "не о еде"
            };

            bool isNoFoodDetected = noFoodKeywords.Any(keyword =>
                reason.ToLowerInvariant().Contains(keyword.ToLowerInvariant()));

            if (isNoFoodDetected)
            {
                return new VoiceFoodResponse
                {
                    Success = false,
                    ErrorMessage = "Не удалось распознать информацию о питании в аудиозаписи. Убедитесь, что говорите о еде или напитках.",
                    TranscribedText = "Информация о питании не найдена",
                    FoodItems = new List<FoodItemResponse>(),
                    EstimatedTotalCalories = 0
                };
            }

            return new VoiceFoodResponse
            {
                Success = false,
                ErrorMessage = $"Техническая ошибка анализа: {reason}. Попробуйте записать аудио заново.",
                TranscribedText = "Анализ не выполнен",
                FoodItems = new List<FoodItemResponse>(),
                EstimatedTotalCalories = 0
            };
        }

        public BodyScanResponse CreateFallbackBodyResponse(string reason)
        {
            _logger.LogInformation($"💪 Creating fallback body response: {reason}");

            return new BodyScanResponse
            {
                Success = true,
                ErrorMessage = null,
                BodyAnalysis = new BodyAnalysisDto
                {
                    EstimatedBodyFatPercentage = 15m,
                    EstimatedMusclePercentage = 40m,
                    BodyType = "Средний тип телосложения",
                    PostureAnalysis = "Не удалось проанализировать осанку",
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

        public bool ShouldRetryRequest(Exception ex, int currentAttempt)
        {
            const int maxAttempts = 3;

            if (currentAttempt >= maxAttempts)
                return false;

            if (ex is HttpRequestException || ex is TaskCanceledException)
                return true;

            if (ex.Message.Contains("503") || ex.Message.Contains("502") || ex.Message.Contains("timeout"))
                return true;

            return false;
        }

        // Private helper methods

        private (string Name, decimal Weight, string WeightType, NutritionPer100gDto Nutrition) DetermineDefaultFood(byte[]? imageData)
        {
            if (imageData != null && imageData.Length > 1024 * 1024) 
            {
                return ("Основное блюдо", 200m, "g", new NutritionPer100gDto
                {
                    Calories = 300,
                    Proteins = 20,
                    Fats = 15,
                    Carbs = 25
                });
            }

            return ("Неизвестный продукт", 150m, "g", new NutritionPer100gDto
            {
                Calories = 200,
                Proteins = 10,
                Fats = 8,
                Carbs = 25
            });
        }

        public TextWorkoutResponse CreateFallbackTextWorkoutResponse(string reason, string? workoutType = null)
        {
            _logger.LogInformation($"📝 Creating fallback text workout response: {reason}");

            var type = DetermineWorkoutType(workoutType);
            var defaultWorkout = CreateDefaultWorkoutData(reason, type);

            return new TextWorkoutResponse
            {
                Success = true,
                ErrorMessage = null,
                ProcessedText = $"Не удалось обработать текст ({reason}), создана базовая тренировка",
                WorkoutData = defaultWorkout
            };
        }

        public TextFoodResponse CreateFallbackTextFoodResponse(string reason, string? mealType = null)
        {
            _logger.LogInformation($"📝 Creating fallback text food response: {reason}");

            var defaultFood = GetDefaultFoodForMeal(mealType);

            return new TextFoodResponse
            {
                Success = true,
                ErrorMessage = null,
                ProcessedText = $"Не удалось обработать текст ({reason}), создана базовая запись о питании",
                FoodItems = new List<FoodItemResponse> { defaultFood },
                EstimatedTotalCalories = defaultFood.TotalCalories
            };
        }

        private string DetermineWorkoutType(string? workoutType)
        {
            if (string.IsNullOrEmpty(workoutType))
                return "strength";

            var lowerType = workoutType.ToLowerInvariant();

            var cardioKeywords = new[] {
                "cardio", "кардио", "бег", "running", "cycling", "велосипед",
                "swimming", "плавание", "walking", "ходьба", "jogging", "bike"
            };
            if (cardioKeywords.Any(keyword => lowerType.Contains(keyword)))
                return "cardio";

            return "strength";
        }

        private ActivityDto CreateDefaultWorkoutData(string reason, string type)
        {
            var startTime = DateTime.UtcNow;
            var endTime = startTime.AddMinutes(type == "cardio" ? 30 : 45);

            var workout = new ActivityDto
            {
                Id = Guid.NewGuid().ToString(),
                Type = type,
                StartDate = startTime,
                EndDate = endTime,
                Calories = type == "cardio" ? 200 : 250,
                CreatedAt = DateTime.UtcNow,
                ActivityData = new ActivityDataDto
                {
                    Name = type == "strength" ? "Базовое упражнение" : "Общее кардио",
                    Category = type == "strength" ? "Strength" : "Cardio",
                    Equipment = type == "strength" ? "Собственный вес" : null,
                    Count = type == "strength" ? 10 : null,
                    MuscleGroup = type == "strength" ? "грудь" : null,
                    Weight = type == "strength" ? null : null,
                    RestTimeSeconds = type == "strength" ? 120 : null,
                    Sets = type == "strength" ? new List<ActivitySetDto>
            {
                new ActivitySetDto
                {
                    SetNumber = 1,
                    Weight = null,
                    Reps = 10,
                    IsCompleted = true
                }
            } : null,
                    Distance = type == "cardio" ? null : null,
                    AvgPace = type == "cardio" ? null : null,
                    AvgPulse = type == "cardio" ? null : null,
                    MaxPulse = type == "cardio" ? null : null
                }
            };

            return workout;
        }

        private FoodItemResponse GetDefaultFoodForMeal(string? mealType)
        {
            var (name, calories, proteins, fats, carbs, weight, weightType) = mealType?.ToLowerInvariant() switch
            {
                "breakfast" or "завтрак" => ("Завтрак", 250m, 12m, 8m, 35m, 200m, "g"),
                "lunch" or "обед" => ("Обед", 400m, 25m, 15m, 45m, 300m, "g"),
                "dinner" or "ужин" => ("Ужин", 350m, 20m, 12m, 40m, 250m, "g"),
                "snack" or "перекус" => ("Перекус", 150m, 5m, 6m, 20m, 100m, "g"),
                _ => ("Неизвестная еда", 200m, 10m, 8m, 25m, 150m, "g")
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