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

        private readonly Dictionary<string, Dictionary<string, string>> _translations = new()
        {
            ["ru"] = new Dictionary<string, string>
            {
                ["mission.breakfast_500"] = "Съешь 500ккал на завтрак",
                ["mission.walk_5000"] = "Пройди 5000 шагов",
                ["mission.body_scan_weekly"] = "Скан тела каждую неделю",
                ["mission.daily_goal_80"] = "Выполни дневную цель на 80%",
                ["mission.weekly_goal_streak"] = "Неделя выполнения целей",
                ["mission.smart_spending"] = "Умная трата: уложись в дневной лимит",
                ["mission.photo_master"] = "Мастер фото: проанализируй 3 фото за день",

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
                // Missions
                ["mission.breakfast_500"] = "Eat 500kcal for breakfast",
                ["mission.walk_5000"] = "Walk 5000 steps",
                ["mission.body_scan_weekly"] = "Weekly body scan",
                ["mission.daily_goal_80"] = "Complete daily goal at 80%",
                ["mission.weekly_goal_streak"] = "Week of goal completion",
                ["mission.smart_spending"] = "Smart spending: stay within daily limit",
                ["mission.photo_master"] = "Photo master: analyze 3 photos per day",

                // Skins
                ["skin.minimalist"] = "Minimalist",
                ["skin.minimalist.desc"] = "For users who spend less than 5 coins per day",
                ["skin.economist"] = "Economist",
                ["skin.economist.desc"] = "For those who know how to save coins",
                ["skin.investor"] = "Investor",
                ["skin.investor.desc"] = "For those who accumulated significant amount",
                ["skin.strategist"] = "Strategist",
                ["skin.strategist.desc"] = "Master of planning and long-term goals",

                // Achievements
                ["achievement.first_workout"] = "First workout",
                ["achievement.workout_week"] = "Week of workouts",
                ["achievement.nutrition_master"] = "Nutrition master",
                ["achievement.body_analyzer"] = "Body analyzer",
                ["achievement.referral_master"] = "Referral master",

                // Common
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

                // Errors
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

                return "ru_RU";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting user locale: {ex.Message}");
                return "ru_RU";
            }
        }

        public string GetLanguageFromLocale(string locale)
        {
            if (string.IsNullOrEmpty(locale))
                return "ru";

            var lang = locale.ToLower().Substring(0, Math.Min(2, locale.Length));

            return lang switch
            {
                "en" => "en",
                "ru" => "ru",
                "es" => "es",
                "de" => "de",
                "fr" => "fr",
                "zh" => "zh",
                "ja" => "ja",
                "ko" => "ko",
                "pt" => "pt",
                "it" => "it",
                _ => "en"
            };
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

            if (_translations.TryGetValue("en", out var enDict))
            {
                if (enDict.TryGetValue(key, out var enTranslation))
                {
                    return enTranslation;
                }
            }

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
    }
}