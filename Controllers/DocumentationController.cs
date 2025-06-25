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
                title = "🏃‍♂️ Fitness Tracker API Documentation",
                version = "2.1.0",
                description = "Complete API documentation for Fitness Tracker with LW Coin system",
                baseUrl = "https://your-api.com",

                authentication = new
                {
                    type = "Bearer Token",
                    description = "Получите токен через /api/auth/confirm-email",
                    format = "Authorization: Bearer {your_token}"
                },

                endpoints = new
                {
                    // 🔐 АУТЕНТИФИКАЦИЯ
                    auth = new[]
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

                    // 👤 ПРОФИЛЬ ПОЛЬЗОВАТЕЛЯ  
                    profile = new[]
                    {
                        new {
                            method = "GET",
                            path = "/api/user/profile",
                            description = "👤 Получить профиль пользователя",
                            auth = "required",
                            response = new { id = "...", email = "...", name = "...", level = 1, experience = 0 }
                        },
                        new {
                            method = "PUT",
                            path = "/api/user/profile",
                            description = "✏️ Обновить профиль",
                            auth = "required",
                            body = new { name = "John Doe", age = 25, gender = "male", weight = 70.5, height = 175.0 }
                        }
                    },

                    // 💰 LW COINS
                    lwCoins = new[]
                    {
                        new {
                            method = "GET",
                            path = "/api/lw-coin/balance",
                            description = "💰 Баланс LW Coins",
                            auth = "required",
                            response = new { balance = 300, monthlyAllowance = 300, usedThisMonth = 0, isPremium = false }
                        },
                        new {
                            method = "POST",
                            path = "/api/lw-coin/spend",
                            description = "💸 Потратить LW Coins",
                            auth = "required",
                            body = new { amount = 1, type = "photo", description = "Food scan", featureUsed = "photo" }
                        },
                        new {
                            method = "GET",
                            path = "/api/lw-coin/transactions",
                            description = "📊 История транзакций",
                            auth = "required",
                            response = new[] { new { amount = -1, type = "spent", spentOn = "food_scan", createdAt = "2025-06-24T..." } }
                        }
                    },

                    // 🏃‍♂️ АКТИВНОСТИ
                    activities = new[]
                    {
                        new {
                            method = "GET",
                            path = "/api/activity?startDate=2025-06-01&endDate=2025-06-30&type=strength",
                            description = "🏃‍♂️ Получить активности с фильтрами",
                            auth = "required",
                            filters = new { startDate = "YYYY-MM-DD", endDate = "YYYY-MM-DD", type = "strength|cardio" }
                        },
                        new {
                            method = "POST",
                            path = "/api/activity",
                            description = "➕ Добавить тренировку",
                            auth = "required",
                            bodyStrength = new {
                                type = "strength",
                                startDate = "2025-06-24T10:00:00Z",
                                startTime = "2025-06-24T10:00:00Z",
                                endDate = "2025-06-24T11:00:00Z",
                                endTime = "2025-06-24T11:00:00Z",
                                calories = 300,
                                strengthData = new {
                                    name = "Жим лежа",
                                    muscleGroup = "Грудь",
                                    equipment = "Штанга",
                                    workingWeight = 80,
                                    restTimeSeconds = 120
                                }
                            },
                            bodyCardio = new {
                                type = "cardio",
                                startDate = "2025-06-24T18:00:00Z",
                                calories = 400,
                                cardioData = new {
                                    cardioType = "Бег",
                                    distanceKm = 5.0,
                                    avgPulse = 150,
                                    maxPulse = 170,
                                    avgPace = "5:30"
                                }
                            }
                        },
                        new {
                            method = "POST",
                            path = "/api/activity/steps",
                            description = "👣 Добавить шаги",
                            auth = "required",
                            body = new { steps = 10000, calories = 500, date = "2025-06-24T00:00:00Z" }
                        },
                        new {
                            method = "GET",
                            path = "/api/activity/stats",
                            description = "📊 Статистика активностей",
                            auth = "required",
                            response = new { totalActivities = 15, totalCalories = 3500, activityTypes = new[] { new { type = "strength", count = 10 } } }
                        }
                    },

                    // 🍎 ПИТАНИЕ
                    nutrition = new[]
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
                                items = new[] {
                                    new {
                                        tempItemId = "temp1", // опционально
                                        name = "Овсянка",
                                        weight = 100,
                                        weightType = "g", // "g" или "ml"
                                        image = "https://example.com/oats.jpg", // опционально
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
                        }
                    },

                    // 🎯 МИССИИ
                    missions = new[]
                    {
                        new {
                            method = "GET",
                            path = "/api/mission",
                            description = "🎯 Получить активные миссии",
                            auth = "required",
                            response = new[] {
                                new {
                                    id = "mission1",
                                    title = "Первая тренировка",
                                    icon = "🏃‍♂️",
                                    rewardExperience = 50,
                                    progress = 0,
                                    targetValue = 1,
                                    isCompleted = false
                                }
                            }
                        },
                        new {
                            method = "GET",
                            path = "/api/mission/achievements",
                            description = "🏆 Получить достижения",
                            auth = "required",
                            response = new[] {
                                new {
                                    id = "achievement1",
                                    title = "Первые шаги",
                                    icon = "⭐",
                                    imageUrl = "https://example.com/achievement1.png",
                                    unlockedAt = "2025-06-24T10:00:00Z"
                                }
                            }
                        }
                    },

                    // 👥 РЕФЕРАЛЫ
                    referrals = new[]
                    {
                        new {
                            method = "GET",
                            path = "/api/referral/generate",
                            description = "🔗 Создать реферальный код",
                            auth = "required",
                            response = new { referralCode = "ABC12345", referralLink = "https://app.com/join?ref=ABC12345" }
                        },
                        new {
                            method = "GET",
                            path = "/api/referral/stats",
                            description = "📊 Статистика рефералов",
                            auth = "required",
                            response = new {
                                totalReferrals = 5,
                                monthlyReferrals = 2,
                                firstLevelReferrals = new[] {
                                    new { name = "John Doe", email = "j***@mail.com", level = 2, rewardCoins = 150 }
                                },
                                secondLevelReferrals = new[] {
                                    new { name = "Jane Smith", email = "j***@mail.com", level = 1, rewardCoins = 75 }
                                }
                            }
                        }
                    },

                    // 📸 СКАН ТЕЛА  
                    bodyScan = new[]
                    {
                        new {
                            method = "POST",
                            path = "/api/body-scan",
                            description = "📸 Добавить скан тела",
                            auth = "required",
                            body = new {
                                frontImageUrl = "https://example.com/front.jpg",
                                sideImageUrl = "https://example.com/side.jpg",
                                backImageUrl = "https://example.com/back.jpg", // опционально
                                weight = 70.5,
                                bodyFatPercentage = 15.2, // опционально
                                musclePercentage = 42.1, // опционально
                                waistCircumference = 80.0, // опционально
                                notes = "Заметки", // опционально
                                scanDate = "2025-06-24T10:00:00Z"
                            }
                        },
                        new {
                            method = "GET",
                            path = "/api/body-scan/comparison",
                            description = "📊 Сравнение сканов",
                            auth = "required",
                            response = new {
                                previousScan = new { weight = 72.0, scanDate = "2025-05-24T..." },
                                currentScan = new { weight = 70.5, scanDate = "2025-06-24T..." },
                                progress = new { weightDifference = -1.5, daysBetweenScans = 31 }
                            }
                        }
                    }
                },

                examples = new
                {
                    completeWorkflow = new[]
                    {
                        "1. POST /api/auth/send-code → Отправить код",
                        "2. POST /api/auth/confirm-email → Получить токен",
                        "3. PUT /api/user/profile → Заполнить профиль",
                        "4. POST /api/activity → Добавить тренировку",
                        "5. POST /api/food-intake → Записать питание",
                        "6. GET /api/mission → Проверить прогресс миссий",
                        "7. GET /api/lw-coin/balance → Проверить баланс"
                    }
                },

                errorCodes = new
                {
                    "400" = "Bad Request - Неверные данные",
                    "401" = "Unauthorized - Требуется авторизация",
                    "404" = "Not Found - Ресурс не найден",
                    "500" = "Internal Server Error - Ошибка сервера"
                }
            };

            return Ok(documentation);
        }
    }
}