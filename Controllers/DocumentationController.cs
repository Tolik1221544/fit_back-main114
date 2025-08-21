using Microsoft.AspNetCore.Mvc;

namespace FitnessTracker.API.Controllers
{
    [ApiController]
    [Route("api/docs")]
    public class DocumentationController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetApiDocumentation()
        {
            var documentation = new
            {
                title = "🏃‍♂️ Fitness Tracker API Documentation with New Economic Model",
                version = "3.0.0",
                description = "Complete API documentation for Fitness Tracker with updated LW Coin pricing system and Gemini AI integration",
                baseUrl = "https://your-api.com",

                economicModel = new
                {
                    title = "💰 Новая ценовая модель LW Coins",
                    description = "Обновленная система ценообразования согласно требованиям заказчика",
                    newPricing = new
                    {
                        photoAnalysis = new { cost = 2.5m, description = "Анализ фото еды с помощью AI" },
                        voiceInput = new { cost = 1.5m, description = "Голосовой ввод тренировки/питания" },
                        textAnalysis = new { cost = 1.0m, description = "Текстовый анализ и обработка" },
                        bodyAnalysis = new { cost = 1.0m, description = "Анализ тела" },
                        exerciseTracking = new { cost = 0.0m, description = "Отслеживание упражнений (бесплатно)" }
                    },
                    dailyLimits = new
                    {
                        baseTier = new
                        {
                            dailyBudget = 10.0m,
                            calculation = "300 монет / 30 дней = 10 монет/день",
                            targetUsage = new
                            {
                                photos = 3,    
                                voice = 1,      
                                text = 2,      
                                total = 11.0m,
                                note = "Слегка превышает дневной лимит, нужна оптимизация"
                            },
                            optimizedUsage = new
                            {
                                photos = 3,   
                                voice = 1,     
                                text = 1,     
                                total = 10.0m, 
                                note = "Оптимальное использование для тарифа 'База'"
                            }
                        },
                        premiumTier = new { dailyBudget = "Unlimited", description = "Безлимитное использование" }
                    }
                },

                authentication = new
                {
                    type = "Bearer Token",
                    description = "Получите токен через /api/auth/confirm-email",
                    format = "Authorization: Bearer {your_token}"
                },

                endpoints = new
                {
                    auth = new object[]
                    {
                        new {
                            method = "POST",
                            path = "/api/auth/send-code",
                            description = "📧 Отправить код подтверждения на email",
                            body = new { email = "user@example.com" },
                            response = new { success = true, message = "Code sent" }
                        },
                        new {
                            method = "POST",
                            path = "/api/auth/confirm-email",
                            description = "✅ Подтвердить email и получить токен",
                            body = new { email = "user@example.com", code = "123456" },
                            response = new { accessToken = "jwt_token", user = new { id = "...", email = "...", lwCoins = 300 } }
                        }
                    },

                    ai = new object[]
                    {
                        new {
                            method = "POST",
                            path = "/api/ai/scan-food",
                            description = "🍎 ИИ анализ еды по фото (Gemini) - 2.5 монеты",
                            auth = "required",
                            cost = "2.5 LW Coins",
                            body = new {
                                image = "multipart/form-data",
                                userPrompt = "Дополнительные инструкции (опционально)",
                                saveResults = "true/false"
                            },
                            response = new {
                                success = true,
                                foodItems = new object[] {
                                    new {
                                        name = "Борщ",
                                        estimatedWeight = 300,
                                        totalCalories = 150,
                                        nutritionPer100g = new {
                                            calories = 50,
                                            proteins = 2.1,
                                            fats = 2.8,
                                            carbs = 6.7
                                        }
                                    }
                                },
                                estimatedCalories = 150,
                                fullDescription = "Анализ от ИИ",
                                imageUrl = "URL сохраненного изображения"
                            }
                        },
                        new {
                            method = "POST",
                            path = "/api/ai/analyze-body",
                            description = "💪 ИИ анализ тела по фотографиям - БЕСПЛАТНО",
                            auth = "required",
                            cost = "Бесплатно",
                            body = new {
                                frontImage = "multipart/form-data",
                                sideImage = "multipart/form-data (опционально)",
                                backImage = "multipart/form-data (опционально)",
                                currentWeight = 70.0,
                                height = 175.0,
                                age = 25,
                                gender = "male",
                                goals = "Набор мышечной массы"
                            },
                            response = new {
                                success = true,
                                bodyAnalysis = new {
                                    estimatedBodyFatPercentage = 15.5,
                                    estimatedMusclePercentage = 42.0,
                                    bodyType = "Мезоморф",
                                    bmi = 22.9,
                                    basalMetabolicRate = 1800,
                                    metabolicRateCategory = "Нормальный",
                                    exerciseRecommendations = new[] { "Силовые тренировки", "Кардио" },
                                    nutritionRecommendations = new[] { "Увеличить белок", "Контролировать углеводы" }
                                },
                                frontImageUrl = "URL фронтального изображения",
                                sideImageUrl = "URL бокового изображения"
                            }
                        },
                        new {
                            method = "POST",
                            path = "/api/ai/voice-workout",
                            description = "🎤 Голосовой ввод тренировки - 1.5 монеты",
                            auth = "required",
                            cost = "1.5 LW Coins",
                            body = new {
                                audioFile = "multipart/form-data (wav/mp3)",
                                workoutType = "strength/cardio (опционально)",
                                saveResults = "true/false"
                            },
                            response = new {
                                success = true,
                                transcribedText = "Сделал жим лежа 80 кг на 10 повторений",
                                workoutData = new {
                                    type = "strength",
                                    startTime = "2025-07-13T17:00:00Z",
                                    endTime = "2025-07-13T17:30:00Z",
                                    estimatedCalories = 200,
                                    strengthData = new {
                                        name = "Жим лежа",
                                        muscleGroup = "Грудь",
                                        workingWeight = 80,
                                        sets = new object[] {
                                            new { setNumber = 1, weight = 80, reps = 10, isCompleted = true }
                                        }
                                    }
                                }
                            }
                        },
                        new {
                            method = "POST",
                            path = "/api/ai/voice-food",
                            description = "🗣️ Голосовой ввод питания - 1.5 монеты",
                            auth = "required",
                            cost = "1.5 LW Coins",
                            body = new {
                                audioFile = "multipart/form-data (wav/mp3)",
                                mealType = "breakfast/lunch/dinner/snack (опционально)",
                                saveResults = "true/false"
                            },
                            response = new {
                                success = true,
                                transcribedText = "Съел тарелку борща и кусок хлеба",
                                foodItems = new object[] {
                                    new {
                                        name = "Борщ",
                                        estimatedWeight = 300,
                                        totalCalories = 150,
                                        weightType = "ml"
                                    },
                                    new {
                                        name = "Хлеб белый",
                                        estimatedWeight = 50,
                                        totalCalories = 132,
                                        weightType = "g"
                                    }
                                },
                                estimatedTotalCalories = 282
                            }
                        }
                    },

                    lwCoins = new object[]
                    {
                        new {
                            method = "GET",
                            path = "/api/lw-coin/balance",
                            description = "💰 Баланс LW Coins с дневными лимитами",
                            auth = "required",
                            response = new {
                                balance = 250,
                                monthlyAllowance = 300,
                                isPremium = false,
                                dailyUsage = 7.5m,
                                dailyLimit = 10.0m,
                                dailyRemaining = 2.5m
                            }
                        },
                        new {
                            method = "POST",
                            path = "/api/lw-coin/spend",
                            description = "💸 Потратить LW Coins с новыми ценами",
                            auth = "required",
                            body = new {
                                amount = 1,
                                type = "ai_scan",
                                description = "Photo analysis",
                                featureUsed = "photo"
                            },
                            note = "Система автоматически определит стоимость: photo=2.5, voice=1.5, text=1.0"
                        },
                        new {
                            method = "GET",
                            path = "/api/lw-coin/pricing",
                            description = "💲 Обновленный прайс-лист",
                            auth = "не требуется",
                            response = new {
                                lwCoinPricing = new {
                                    photoCost = 2.5m,
                                    voiceCost = 1.5m,
                                    textCost = 1.0m,
                                    bodyAnalysisCost = 1.0m,
                                    exerciseTrackingCost = 0.0m
                                },
                                dailyLimits = new {
                                    baseUserDailyLimit = 10.0m,
                                    targetUsage = "3 фото + 1 голос + 2 текста = 11 монет",
                                    optimizedUsage = "3 фото + 1 голос + 1 текст = 10 монет"
                                }
                            }
                        },
                        new {
                            method = "GET",
                            path = "/api/lw-coin/check-limit/{featureType}",
                            description = "📊 Проверить дневные лимиты",
                            auth = "required",
                            response = new {
                                dailyUsage = 7.5m,
                                dailyLimit = 10.0m,
                                dailyRemaining = 2.5m,
                                isPremium = false,
                                featureUsage = new {
                                    photo = 3,
                                    voice = 0,
                                    text = 0
                                }
                            }
                        }
                    },

                    nutrition = new object[]
                    {
                        new {
                            method = "GET",
                            path = "/api/food-intake?date=2025-07-13",
                            description = "🍎 Получить записи питания",
                            auth = "required",
                            filters = new { date = "YYYY-MM-DD (опционально)" }
                        },
                        new {
                            method = "POST",
                            path = "/api/food-intake/ai-scan",
                            description = "🤖 ИИ сканирование еды - 2.5 монеты",
                            auth = "required",
                            cost = "2.5 LW Coins",
                            body = new {
                                image = "multipart/form-data",
                                userPrompt = "Опциональные инструкции",
                                saveResults = "true/false"
                            }
                        }
                    },

                    activities = new object[]
                    {
                        new {
                            method = "GET",
                            path = "/api/activity?startDate=2025-07-01&endDate=2025-07-31&type=strength",
                            description = "🏃‍♂️ Получить активности с фильтрами - БЕСПЛАТНО",
                            auth = "required"
                        },
                        new {
                            method = "POST",
                            path = "/api/activity",
                            description = "➕ Добавить тренировку - БЕСПЛАТНО",
                            auth = "required",
                            cost = "Бесплатно",
                            bodyStrength = new {
                                type = "strength",
                                startDate = "2025-07-13T10:00:00Z",
                                calories = 300,
                                strengthData = new {
                                    name = "Жим лежа",
                                    muscleGroup = "Грудь",
                                    equipment = "Штанга",
                                    workingWeight = 80,
                                    sets = new object[] {
                                        new { setNumber = 1, weight = 80, reps = 10, isCompleted = true },
                                        new { setNumber = 2, weight = 80, reps = 8, isCompleted = true }
                                    }
                                }
                            }
                        }
                    }
                },

                examples = new
                {
                    newEconomicWorkflow = new string[]
                    {
                        "✅ НОВОЕ: Работа с обновленной ценовой моделью",
                        "1. GET /api/lw-coin/balance → Проверить дневной лимит (10 монет)",
                        "2. POST /api/ai/scan-food → Сфотографировать еду (-2.5 монеты)",
                        "3. POST /api/ai/scan-food → Еще одно фото (-2.5 монеты)",
                        "4. POST /api/ai/scan-food → Третье фото (-2.5 монеты)",
                        "5. POST /api/ai/voice-food → Голосовой ввод (-1.5 монеты)",
                        "6. Остаток: 10 - 7.5 - 1.5 = 1 монета",
                        "7. POST /api/ai/text-analysis → Текстовый анализ (-1.0 монета)",
                        "8. Дневной лимит исчерпан ✅"
                    },
                    dailyOptimalUsage = new string[]
                    {
                        "💡 Оптимальное использование для тарифа 'База':",
                        "🌅 Утром: 1 фото завтрака (-2.5 монеты)",
                        "🥗 Обед: 1 фото обеда (-2.5 монеты)",
                        "🍽️ Ужин: 1 фото ужина (-2.5 монеты)",
                        "🎤 Вечером: голосовой ввод тренировки (-1.5 монеты)",
                        "📝 Анализ: 1 текстовый запрос (-1.0 монета)",
                        "Итого: 10.0 монет = точно в дневном лимите ✅"
                    },
                    freeFeatures = new string[]
                    {
                        "🆓 Полностью бесплатные функции:",
                        "💪 Анализ тела с фотографиями (безлимитно)",
                        "🏃‍♂️ Отслеживание тренировок и упражнений",
                        "📊 Просмотр статистики и прогресса",
                        "🎯 Создание и отслеживание целей",
                        "🏆 Миссии и достижения",
                        "🎨 Система скинов с бустом опыта"
                    }
                },

                pricingTiers = new
                {
                    free = new
                    {
                        name = "Бесплатный",
                        monthlyCoins = 0,
                        dailyLimit = "Нет монет",
                        features = new[] { "Отслеживание упражнений", "Анализ тела", "Базовая статистика" }
                    },
                    @base = new
                    {
                        name = "База",
                        price = "$3.00/месяц",
                        monthlyCoins = 300,
                        dailyLimit = "10 монет/день",
                        targetUsage = "3 фото + 1 голос + 1 текст в день",
                        features = new[] {
                            "300 LW Coins в месяц",
                            "Дневной лимит 10 монет",
                            "ИИ анализ фото еды (2.5 монеты)",
                            "Голосовой ввод (1.5 монеты)",
                            "Текстовый анализ (1.0 монета)",
                            "Безлимитный анализ тела",
                            "Все бесплатные функции"
                        }
                    },
                    premium = new
                    {
                        name = "Pro",
                        price = "$8.99/месяц",
                        monthlyCoins = "Безлимитно",
                        dailyLimit = "Без ограничений",
                        targetUsage = "До 15 действий в день",
                        features = new[] {
                            "Безлимитное использование всех ИИ функций",
                            "Без дневных лимитов",
                            "Приоритетная поддержка",
                            "Расширенная аналитика",
                            "Все функции тарифа 'База'",
                            "Эксклюзивные скины"
                        }
                    }
                },

                financialProjection = new
                {
                    title = "💰 Финансовая модель на 10,000 пользователей",
                    userDistribution = new
                    {
                        free = new { percentage = "93%", count = 9300, plan = "Бесплатный" },
                        baseUsers = new { percentage = "6%", count = 600, plan = "База ($3)" },
                        proUsers = new { percentage = "1%", count = 100, plan = "Pro ($8.99)" }
                    },
                    monthlyRevenue = new
                    {
                        fromBase = "$1,800 (600 × $3.00)",
                        fromPro = "$899 (100 × $8.99)",
                        total = "$2,699"
                    },
                    monthlyCosts = new
                    {
                        freeUsers = "$744 (9300 × $0.08)",
                        baseUsers = "$338 (600 × $0.56)",
                        proUsers = "$106 (100 × $1.06)",
                        total = "$1,188"
                    },
                    monthlyProfit = "$1,511 ($2,699 - $1,188)",
                    profitMargin = "56%"
                },

                costs = new
                {
                    description = "Стоимость использования ИИ функций с новой моделью",
                    free = new string[]
                    {
                        "💪 Анализ тела (безлимитно)",
                        "🏃‍♂️ Отслеживание упражнений",
                        "📊 Просмотр статистики",
                        "🎯 Миссии и достижения"
                    },
                    paid = new string[]
                    {
                        "🍎 Фото-анализ еды: 2.5 LW Coins",
                        "🎤 Голосовой ввод тренировки: 1.5 LW Coins",
                        "🗣️ Голосовой ввод питания: 1.5 LW Coins",
                        "📝 Текстовый анализ: 1.0 LW Coin"
                    },
                    limits = new string[]
                    {
                        "📅 Тариф 'База': 10 монет в день",
                        "🎯 Целевое использование: 3 фото + 1 голос + 1 текст",
                        "💎 Премиум: безлимитное использование"
                    }
                },

                migration = new
                {
                    title = "🔄 Миграция на новую ценовую модель",
                    steps = new string[]
                    {
                        "1. Выполнить SQL миграцию: migration_update_pricing.sql",
                        "2. Обновить поля в базе данных (FractionalAmount, UsageDate)",
                        "3. Перезапустить API сервис",
                        "4. Проверить работу новых цен через /api/lw-coin/pricing",
                        "5. Уведомить пользователей об изменениях"
                    },
                    backwardCompatibility = "Полная совместимость с существующими данными"
                },

                errorCodes = new
                {
                    BadRequest = "400 - Неверные данные или превышен дневной лимит",
                    Unauthorized = "401 - Требуется авторизация",
                    NotFound = "404 - Ресурс не найден",
                    ServiceUnavailable = "503 - ИИ сервис недоступен",
                    InternalServerError = "500 - Ошибка сервера",
                    DailyLimitExceeded = "400 - Превышен дневной лимит в 10 монет",
                    InsufficientCoins = "400 - Недостаточно LW Coins"
                },

                changelog = new
                {
                    version = "3.0.0",
                    date = "2025-07-13",
                    changes = new string[]
                    {
                        "✅ Новая ценовая модель: Фото 2.5, Голос 1.5, Текст 1.0 монеты",
                        "✅ Дневные лимиты для тарифа 'База': 10 монет/день",
                        "✅ Поддержка дробных монет (2.5, 1.5, 1.0)",
                        "✅ Обновленная экономическая модель согласно требованиям",
                        "✅ Новые миссии и достижения связанные с экономией",
                        "✅ Расширенная аналитика использования монет",
                        "✅ Улучшенная система уведомлений о лимитах"
                    }
                }
            };

            return Ok(documentation);
        }
    }
}