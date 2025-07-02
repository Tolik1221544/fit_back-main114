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
                title = "🏃‍♂️ Fitness Tracker API Documentation with Gemini AI",
                version = "2.2.0",
                description = "Complete API documentation for Fitness Tracker with LW Coin system and Gemini AI integration",
                baseUrl = "https://your-api.com",

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
                            description = "🍎 ИИ анализ еды по фото (Gemini)",
                            auth = "required",
                            cost = "1 LW Coin",
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
                                fullDescription = "Анализ от ИИ"
                            }
                        },
                        new {
                            method = "POST",
                            path = "/api/ai/analyze-body",
                            description = "💪 ИИ анализ тела по фотографиям",
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
                                    exerciseRecommendations = new[] { "Силовые тренировки", "Кардио" },
                                    nutritionRecommendations = new[] { "Увеличить белок", "Контролировать углеводы" }
                                }
                            }
                        },
                        new {
                            method = "POST",
                            path = "/api/ai/voice-workout",
                            description = "🎤 Голосовой ввод тренировки",
                            auth = "required",
                            cost = "1 LW Coin",
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
                                    strengthData = new {
                                        name = "Жим лежа",
                                        muscleGroup = "Грудь",
                                        workingWeight = 80
                                    }
                                }
                            }
                        },
                        new {
                            method = "POST",
                            path = "/api/ai/voice-food",
                            description = "🗣️ Голосовой ввод питания",
                            auth = "required",
                            cost = "1 LW Coin",
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
                                        totalCalories = 150
                                    }
                                }
                            }
                        },
                        new {
                            method = "GET",
                            path = "/api/ai/status",
                            description = "🧠 Проверка статуса ИИ сервиса",
                            auth = "не требуется",
                            response = new {
                                service = "Gemini AI",
                                status = "Online",
                                features = new[] { "Food Analysis", "Body Analysis", "Voice Recognition" }
                            }
                        },
                        new {
                            method = "GET",
                            path = "/api/ai/usage-stats",
                            description = "📊 Статистика использования ИИ",
                            auth = "required",
                            response = new {
                                totalAIUsage = 45,
                                monthlyAIUsage = 12,
                                featureUsage = new {
                                    foodScans = 25,
                                    voiceWorkouts = 10,
                                    voiceFood = 8,
                                    bodyAnalysis = 2
                                }
                            }
                        }
                    },

                    nutrition = new object[]
                    {
                        new {
                            method = "GET",
                            path = "/api/food-intake?date=2025-06-24",
                            description = "🍎 Получить записи питания",
                            auth = "required",
                            filters = new { date = "YYYY-MM-DD (опционально)" }
                        },
                        new {
                            method = "POST",
                            path = "/api/food-intake",
                            description = "➕ Добавить прием пищи",
                            auth = "required",
                            body = new {
                                items = new object[] {
                                    new {
                                        name = "Овсянка",
                                        weight = 100,
                                        weightType = "g",
                                        nutritionPer100g = new {
                                            calories = 389,
                                            proteins = 16.9,
                                            fats = 6.9,
                                            carbs = 66.3
                                        }
                                    }
                                },
                                dateTime = "2025-06-24T08:00:00Z"
                            }
                        },
                        new {
                            method = "POST",
                            path = "/api/food-intake/scan",
                            description = "📸 Сканирование еды (устаревший метод)",
                            auth = "required",
                            cost = "1 LW Coin",
                            note = "⚠️ Используйте /api/ai/scan-food для лучших результатов"
                        },
                        new {
                            method = "POST",
                            path = "/api/food-intake/ai-scan",
                            description = "🤖 Новое сканирование еды с Gemini AI",
                            auth = "required",
                            cost = "1 LW Coin",
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
                            path = "/api/activity?startDate=2025-06-01&endDate=2025-06-30&type=strength",
                            description = "🏃‍♂️ Получить активности с фильтрами",
                            auth = "required"
                        },
                        new {
                            method = "POST",
                            path = "/api/activity",
                            description = "➕ Добавить тренировку",
                            auth = "required",
                            bodyStrength = new {
                                type = "strength",
                                startDate = "2025-06-24T10:00:00Z",
                                calories = 300,
                                strengthData = new {
                                    name = "Жим лежа",
                                    muscleGroup = "Грудь",
                                    equipment = "Штанга",
                                    workingWeight = 80
                                }
                            }
                        },
                        new {
                            method = "POST",
                            path = "/api/activity/steps",
                            description = "👣 Добавить/обновить шаги",
                            auth = "required",
                            body = new { steps = 10000, calories = 500, date = "2025-06-24T00:00:00Z" }
                        }
                    },

                    lwCoins = new object[]
                    {
                        new {
                            method = "GET",
                            path = "/api/lw-coin/balance",
                            description = "💰 Баланс LW Coins",
                            auth = "required",
                            response = new { balance = 300, monthlyAllowance = 300, isPremium = false }
                        },
                        new {
                            method = "GET",
                            path = "/api/lw-coin/pricing",
                            description = "💲 Прайс-лист",
                            auth = "не требуется",
                            response = new {
                                lwCoinPricing = new {
                                    photoCost = 1,
                                    voiceCost = 1,
                                    aiFeatures = "1 LW Coin за запрос"
                                }
                            }
                        }
                    },

                    bodyScan = new object[]
                    {
                        new {
                            method = "POST",
                            path = "/api/body-scan",
                            description = "📸 Добавить скан тела (ручной)",
                            auth = "required"
                        }
                    }
                },

                newFeatures = new
                {
                    geminiAI = new string[]
                    {
                        "🍎 Умное распознавание еды с точной калорийностью",
                        "💪 Анализ тела и рекомендации по тренировкам",
                        "🎤 Голосовой ввод тренировок и питания",
                        "📊 Подробная аналитика от ИИ",
                        "🧠 Персонализированные рекомендации"
                    }
                },

                examples = new
                {
                    aiWorkflow = new string[]
                    {
                        "✅ НОВОЕ: Работа с ИИ функциями",
                        "1. GET /api/ai/status → Проверить статус ИИ",
                        "2. POST /api/ai/scan-food → Сфотографировать еду",
                        "3. POST /api/ai/analyze-body → Анализ тела",
                        "4. POST /api/ai/voice-workout → Голосовой ввод тренировки",
                        "5. POST /api/ai/voice-food → Голосовой ввод питания",
                        "6. GET /api/ai/usage-stats → Статистика использования"
                    },
                    completeWorkflow = new string[]
                    {
                        "1. POST /api/auth/send-code → Отправить код",
                        "2. POST /api/auth/confirm-email → Получить токен",
                        "3. PUT /api/user/profile → Заполнить профиль",
                        "4. POST /api/ai/scan-food → ИИ анализ еды",
                        "5. POST /api/ai/voice-workout → Голосовой ввод тренировки",
                        "6. POST /api/ai/analyze-body → Анализ тела",
                        "7. GET /api/mission → Проверить прогресс миссий",
                        "8. GET /api/lw-coin/balance → Проверить баланс"
                    }
                },

                costs = new
                {
                    description = "Стоимость использования ИИ функций",
                    free = new string[]
                    {
                        "📸 Анализ тела (без ограничений)",
                        "📊 Просмотр статистики",
                        "🎯 Миссии и достижения"
                    },
                    paid = new string[]
                    {
                        "🍎 Сканирование еды: 1 LW Coin",
                        "🎤 Голосовой ввод тренировки: 1 LW Coin",
                        "🗣️ Голосовой ввод питания: 1 LW Coin"
                    },
                    premium = new string[]
                    {
                        "👑 Премиум подписка: безлимитное использование всех ИИ функций",
                        "💰 Стоимость: $8.99/месяц"
                    }
                },

                errorCodes = new
                {
                    BadRequest = "400 - Неверные данные или недостаточно LW Coins",
                    Unauthorized = "401 - Требуется авторизация",
                    NotFound = "404 - Ресурс не найден",
                    ServiceUnavailable = "503 - ИИ сервис недоступен",
                    InternalServerError = "500 - Ошибка сервера"
                }
            };

            return Ok(documentation);
        }
    }
}