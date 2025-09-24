using System.Security.Claims;
using FitnessTracker.API.Repositories;

namespace FitnessTracker.API.Services
{
    public interface ILocalizationService
    {
        Task<string> GetUserLocaleAsync(string userId);
        string GetLanguageFromLocale(string locale);
        string Translate(string key, string locale);
        Dictionary<string, string> GetAllTranslations(string locale);
    }

    public class LocalizationService : ILocalizationService
    {
        private readonly IUserRepository _userRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<LocalizationService> _logger;

        private readonly HashSet<string> _availableLanguages = new()
        {
            "ru", "en", "es", "de", "fr", "zh", "ja", "ko", "pt", "it", "ar", "hi", "tr", "pl", "uk"
        };

        private readonly Dictionary<string, Dictionary<string, string>> _translations = new()
        {
            ["ru"] = new Dictionary<string, string>
            {
                // Миссии
                ["mission.breakfast_500"] = "Съешь 500ккал на завтрак",
                ["mission.walk_5000"] = "Пройди 5000 шагов",
                ["mission.body_scan_weekly"] = "Скан тела каждую неделю",
                ["mission.daily_goal_80"] = "Выполни дневную цель на 80%",
                ["mission.weekly_goal_streak"] = "Неделя выполнения целей",
                ["mission.smart_spending"] = "Умная трата: уложись в дневной лимит",
                ["mission.photo_master"] = "Мастер фото: проанализируй 3 фото за день",

                // Скины
                ["skin.minimalist"] = "Минималист",
                ["skin.minimalist.desc"] = "Для пользователей, которые тратят меньше 5 монет в день",
                ["skin.economist"] = "Экономист",
                ["skin.economist.desc"] = "Для тех, кто умеет экономить монеты",
                ["skin.investor"] = "Инвестор",
                ["skin.investor.desc"] = "Для тех, кто накопил значительную сумму",
                ["skin.strategist"] = "Стратег",
                ["skin.strategist.desc"] = "Мастер планирования и долгосрочных целей",

                // Достижения
                ["achievement.first_workout"] = "Первая тренировка",
                ["achievement.workout_week"] = "Неделя тренировок",
                ["achievement.nutrition_master"] = "Мастер питания",
                ["achievement.body_analyzer"] = "Аналитик тела",
                ["achievement.referral_master"] = "Мастер рефералов",
                ["achievement.goal_setter"] = "Постановщик целей",
                ["achievement.goal_achiever"] = "Достигатор целей",
                ["achievement.consistency_master"] = "Мастер постоянства",
                ["achievement.budget_master"] = "Мастер бюджета",
                ["achievement.photo_expert"] = "Эксперт фотоанализа",

                // Общие
                ["strength"] = "Силовая",
                ["cardio"] = "Кардио",
                ["breakfast"] = "Завтрак",
                ["lunch"] = "Обед",
                ["dinner"] = "Ужин",
                ["snack"] = "Перекус",
                ["calories"] = "Калории",
                ["proteins"] = "Белки",
                ["fats"] = "Жиры",
                ["carbs"] = "Углеводы",
                ["weight"] = "Вес",
                ["height"] = "Рост",
                ["age"] = "Возраст",
                ["male"] = "Мужской",
                ["female"] = "Женский",

                // Ошибки
                ["error.insufficient_coins"] = "Недостаточно LW Coins",
                ["error.not_found"] = "Не найдено",
                ["error.invalid_data"] = "Неверные данные",
                ["error.server_error"] = "Ошибка сервера",
                ["error.file_too_large"] = "Файл слишком большой",
                ["error.analysis_failed"] = "Ошибка анализа",
                ["error.user_not_found"] = "Пользователь не найден",
                ["error.no_images"] = "Изображения не предоставлены",
                ["error.image_save_failed"] = "Ошибка сохранения изображения",
                ["error.body_analysis_failed"] = "Ошибка анализа тела"
            },

            ["en"] = new Dictionary<string, string>
            {
                // Миссии
                ["mission.breakfast_500"] = "Eat 500kcal for breakfast",
                ["mission.walk_5000"] = "Walk 5000 steps",
                ["mission.body_scan_weekly"] = "Weekly body scan",
                ["mission.daily_goal_80"] = "Complete daily goal at 80%",
                ["mission.weekly_goal_streak"] = "Week of goal completion",
                ["mission.smart_spending"] = "Smart spending: stay within daily limit",
                ["mission.photo_master"] = "Photo master: analyze 3 photos per day",

                // Скины
                ["skin.minimalist"] = "Minimalist",
                ["skin.minimalist.desc"] = "For users who spend less than 5 coins per day",
                ["skin.economist"] = "Economist",
                ["skin.economist.desc"] = "For those who know how to save coins",
                ["skin.investor"] = "Investor",
                ["skin.investor.desc"] = "For those who accumulated significant amount",
                ["skin.strategist"] = "Strategist",
                ["skin.strategist.desc"] = "Master of planning and long-term goals",

                // Достижения
                ["achievement.first_workout"] = "First Workout",
                ["achievement.workout_week"] = "Week of Workouts",
                ["achievement.nutrition_master"] = "Nutrition Master",
                ["achievement.body_analyzer"] = "Body Analyzer",
                ["achievement.referral_master"] = "Referral Master",
                ["achievement.goal_setter"] = "Goal Setter",
                ["achievement.goal_achiever"] = "Goal Achiever",
                ["achievement.consistency_master"] = "Consistency Master",
                ["achievement.budget_master"] = "Budget Master",
                ["achievement.photo_expert"] = "Photo Expert",

                // Общие
                ["strength"] = "Strength",
                ["cardio"] = "Cardio",
                ["breakfast"] = "Breakfast",
                ["lunch"] = "Lunch",
                ["dinner"] = "Dinner",
                ["snack"] = "Snack",
                ["calories"] = "Calories",
                ["proteins"] = "Proteins",
                ["fats"] = "Fats",
                ["carbs"] = "Carbs",
                ["weight"] = "Weight",
                ["height"] = "Height",
                ["age"] = "Age",
                ["male"] = "Male",
                ["female"] = "Female",

                // Ошибки
                ["error.insufficient_coins"] = "Insufficient LW Coins",
                ["error.not_found"] = "Not found",
                ["error.invalid_data"] = "Invalid data",
                ["error.server_error"] = "Server error",
                ["error.file_too_large"] = "File too large",
                ["error.analysis_failed"] = "Analysis failed",
                ["error.user_not_found"] = "User not found",
                ["error.no_images"] = "No images provided",
                ["error.image_save_failed"] = "Image save failed",
                ["error.body_analysis_failed"] = "Body analysis failed"
            },

            ["es"] = new Dictionary<string, string>
            {
                ["mission.breakfast_500"] = "Come 500kcal en el desayuno",
                ["mission.walk_5000"] = "Camina 5000 pasos",
                ["skin.minimalist"] = "Minimalista",
                ["achievement.first_workout"] = "Primer Entrenamiento",
                ["achievement.workout_week"] = "Semana de Entrenamientos",
                ["achievement.nutrition_master"] = "Maestro de Nutrición",
                ["strength"] = "Fuerza",
                ["cardio"] = "Cardio",
                ["breakfast"] = "Desayuno",
                ["lunch"] = "Almuerzo",
                ["dinner"] = "Cena",
                ["snack"] = "Merienda",
                ["error.insufficient_coins"] = "Monedas LW insuficientes",
                ["error.invalid_data"] = "Datos inválidos"
            },

            ["de"] = new Dictionary<string, string>
            {
                ["mission.breakfast_500"] = "Iss 500kcal zum Frühstück",
                ["mission.walk_5000"] = "Gehe 5000 Schritte",
                ["skin.minimalist"] = "Minimalist",
                ["achievement.first_workout"] = "Erstes Training",
                ["achievement.workout_week"] = "Trainingswoche",
                ["achievement.nutrition_master"] = "Ernährungsmeister",
                ["strength"] = "Kraft",
                ["cardio"] = "Cardio",
                ["breakfast"] = "Frühstück",
                ["lunch"] = "Mittagessen",
                ["dinner"] = "Abendessen",
                ["snack"] = "Snack",
                ["error.insufficient_coins"] = "Unzureichende LW-Münzen",
                ["error.invalid_data"] = "Ungültige Daten"
            },

            ["fr"] = new Dictionary<string, string>
            {
                ["mission.breakfast_500"] = "Mange 500kcal au petit-déjeuner",
                ["mission.walk_5000"] = "Marche 5000 pas",
                ["skin.minimalist"] = "Minimaliste",
                ["achievement.first_workout"] = "Premier Entraînement",
                ["achievement.workout_week"] = "Semaine d'Entraînements",
                ["achievement.nutrition_master"] = "Maître de la Nutrition",
                ["strength"] = "Force",
                ["cardio"] = "Cardio",
                ["breakfast"] = "Petit-déjeuner",
                ["lunch"] = "Déjeuner",
                ["dinner"] = "Dîner",
                ["snack"] = "Collation",
                ["error.insufficient_coins"] = "Pièces LW insuffisantes",
                ["error.invalid_data"] = "Données invalides"
            }
        };

        public LocalizationService(
            IUserRepository userRepository,
            IHttpContextAccessor httpContextAccessor,
            ILogger<LocalizationService> logger)
        {
            _userRepository = userRepository;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<string> GetUserLocaleAsync(string userId)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user != null && !string.IsNullOrEmpty(user.Locale))
                {
                    return user.Locale;
                }

                var acceptLanguage = _httpContextAccessor.HttpContext?.Request.Headers["Accept-Language"].FirstOrDefault();
                if (!string.IsNullOrEmpty(acceptLanguage))
                {
                    return acceptLanguage;
                }

                return "en";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting user locale: {ex.Message}");
                return "en";
            }
        }

        /// <summary>
        /// ✅ УМНЫЙ поиск языка из любой локали
        /// Поддерживает: en_US, en_EN, en_GB, en_ID, en_AU и т.д. → "en"
        /// Поддерживает: ru_RU, ru_BY, ru_KZ → "ru"
        /// </summary>
        public string GetLanguageFromLocale(string locale)
        {
            if (string.IsNullOrEmpty(locale))
                return "en";

            try
            {
                var lang = locale.ToLower().Substring(0, Math.Min(2, locale.Length));

                if (_availableLanguages.Contains(lang))
                {
                    _logger.LogInformation($"🌍 Locale '{locale}' → language '{lang}' (поддерживается)");
                    return lang;
                }

                _logger.LogWarning($"🌍 Locale '{locale}' → language '{lang}' не поддерживается, используем fallback 'en'");
                return "en";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error parsing locale '{locale}': {ex.Message}");
                return "en"; 
            }
        }

        public string Translate(string key, string locale)
        {
            var lang = GetLanguageFromLocale(locale);

            if (_translations.TryGetValue(lang, out var langDict))
            {
                if (langDict.TryGetValue(key, out var translation))
                {
                    return translation;
                }
            }

            if (lang != "en" && _translations.TryGetValue("en", out var enDict))
            {
                if (enDict.TryGetValue(key, out var enTranslation))
                {
                    _logger.LogDebug($"🌍 Translation fallback: key '{key}' для '{locale}' → английский перевод");
                    return enTranslation;
                }
            }

            if (lang != "ru" && _translations.TryGetValue("ru", out var ruDict))
            {
                if (ruDict.TryGetValue(key, out var ruTranslation))
                {
                    _logger.LogDebug($"🌍 Translation fallback: key '{key}' для '{locale}' → русский перевод");
                    return ruTranslation;
                }
            }

            _logger.LogWarning($"🌍 Translation not found: key '{key}' для locale '{locale}'");
            return key;
        }

        public Dictionary<string, string> GetAllTranslations(string locale)
        {
            var lang = GetLanguageFromLocale(locale);

            if (_translations.TryGetValue(lang, out var translations))
            {
                return new Dictionary<string, string>(translations);
            }

            if (_translations.TryGetValue("en", out var enTranslations))
            {
                return new Dictionary<string, string>(enTranslations);
            }

            return new Dictionary<string, string>();
        }

        /// <summary>
        /// ✅ Получить список всех поддерживаемых языков
        /// </summary>
        public HashSet<string> GetSupportedLanguages()
        {
            return new HashSet<string>(_availableLanguages);
        }

        /// <summary>
        /// ✅ Проверить поддерживается ли язык
        /// </summary>
        public bool IsLanguageSupported(string language)
        {
            return _availableLanguages.Contains(language?.ToLower());
        }
    }
}