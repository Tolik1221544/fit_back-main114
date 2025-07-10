using FitnessTracker.API.DTOs;
using FitnessTracker.API.Services.AI.Models;
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
				_logger.LogInformation("?? Analyzing food image with Vertex AI Gemini Pro 2.5");

				var prompt = CreateFoodAnalysisPrompt(userPrompt);
				var request = new UniversalAIRequest
				{
					Messages = new List<UniversalMessage>
					{
						new UniversalMessage
						{
							Role = "user",
							Content = new List<UniversalContent>
							{
								new UniversalContent { Type = "text", Text = prompt },
								new UniversalContent
								{
									Type = "image",
									Media = new UniversalMedia
									{
										MimeType = "image/jpeg",
										Data = imageData
									}
								}
							}
						}
					},
					Config = new AIRequestConfig
					{
						Temperature = 0.0,
						MaxTokens = 2048
					}
				};

				var response = await SendVertexRequestAsync(request);
				return ParseFoodAnalysisResponse(response);
			}
			catch (Exception ex)
			{
				_logger.LogError($"? Error analyzing food image with Vertex AI: {ex.Message}");
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
				_logger.LogInformation("?? Analyzing body images with Vertex AI Gemini Pro 2.5");

				var prompt = CreateBodyAnalysisPrompt(weight, height, age, gender, goals);
				var content = new List<UniversalContent>
				{
					new UniversalContent { Type = "text", Text = prompt }
				};

				// Добавляем изображения
				if (frontImageData != null)
				{
					content.Add(new UniversalContent
					{
						Type = "image",
						Media = new UniversalMedia { MimeType = "image/jpeg", Data = frontImageData }
					});
				}

				if (sideImageData != null)
				{
					content.Add(new UniversalContent
					{
						Type = "image",
						Media = new UniversalMedia { MimeType = "image/jpeg", Data = sideImageData }
					});
				}

				if (backImageData != null)
				{
					content.Add(new UniversalContent
					{
						Type = "image",
						Media = new UniversalMedia { MimeType = "image/jpeg", Data = backImageData }
					});
				}

				var request = new UniversalAIRequest
				{
					Messages = new List<UniversalMessage>
					{
						new UniversalMessage { Role = "user", Content = content }
					},
					Config = new AIRequestConfig { Temperature = 0.0, MaxTokens = 4096 }
				};

				var response = await SendVertexRequestAsync(request);
				return ParseBodyAnalysisResponse(response);
			}
			catch (Exception ex)
			{
				_logger.LogError($"? Error analyzing body images: {ex.Message}");
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
				_logger.LogInformation("?? Analyzing voice workout with Vertex AI");

				var prompt = CreateVoiceWorkoutAnalysisPrompt(workoutType);
				var mimeType = DetectAudioMimeType(audioData);

				var request = new UniversalAIRequest
				{
					Messages = new List<UniversalMessage>
					{
						new UniversalMessage
						{
							Role = "user",
							Content = new List<UniversalContent>
							{
								new UniversalContent { Type = "text", Text = prompt },
								new UniversalContent
								{
									Type = "audio",
									Media = new UniversalMedia { MimeType = mimeType, Data = audioData }
								}
							}
						}
					},
					Config = new AIRequestConfig { Temperature = 0.0, MaxTokens = 2048 }
				};

				var response = await SendVertexRequestAsync(request);
				return ParseVoiceWorkoutResponse(response);
			}
			catch (Exception ex)
			{
				_logger.LogError($"? Error analyzing voice workout: {ex.Message}");
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
				_logger.LogInformation("??? Analyzing voice food with Vertex AI");

				var prompt = CreateVoiceFoodAnalysisPrompt(mealType);
				var mimeType = DetectAudioMimeType(audioData);

				var request = new UniversalAIRequest
				{
					Messages = new List<UniversalMessage>
					{
						new UniversalMessage
						{
							Role = "user",
							Content = new List<UniversalContent>
							{
								new UniversalContent { Type = "text", Text = prompt },
								new UniversalContent
								{
									Type = "audio",
									Media = new UniversalMedia { MimeType = mimeType, Data = audioData }
								}
							}
						}
					},
					Config = new AIRequestConfig { Temperature = 0.0, MaxTokens = 2048 }
				};

				var response = await SendVertexRequestAsync(request);
				return ParseVoiceFoodResponse(response);
			}
			catch (Exception ex)
			{
				_logger.LogError($"? Error analyzing voice food: {ex.Message}");
				return new VoiceFoodResponse
				{
					Success = false,
					ErrorMessage = $"Ошибка анализа аудио: {ex.Message}"
				};
			}
		}

		public async Task<bool> IsHealthyAsync()
		{
			try
			{
				var testRequest = new UniversalAIRequest
				{
					Messages = new List<UniversalMessage>
					{
						new UniversalMessage
						{
							Role = "user",
							Content = new List<UniversalContent>
							{
								new UniversalContent { Type = "text", Text = "Ответь 'OK' если ты работаешь" }
							}
						}
					},
					Config = new AIRequestConfig { Temperature = 0.0, MaxTokens = 10 }
				};

				var response = await SendVertexRequestAsync(testRequest);
				return !string.IsNullOrEmpty(response);
			}
			catch
			{
				return false;
			}
		}

		private async Task<string> SendVertexRequestAsync(UniversalAIRequest request)
		{
			var projectId = _configuration["GoogleCloud:ProjectId"];
			var location = _configuration["GoogleCloud:Location"] ?? "us-central1";
			var model = _configuration["GoogleCloud:Model"] ?? "gemini-2.5-pro";

			var url = $"https://{location}-aiplatform.googleapis.com/v1beta1/projects/{projectId}/locations/{location}/publishers/google/models/{model}:generateContent";

			// Конвертируем универсальный запрос в формат Vertex AI
			var vertexRequest = ConvertToVertexFormat(request);
			var jsonContent = JsonSerializer.Serialize(vertexRequest, new JsonSerializerOptions
			{
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase
			});

			_logger.LogDebug($"?? Sending request to Vertex AI: {url}");

			// Получаем токен
			var token = await _tokenService.GetAccessTokenAsync();

			var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
			{
				Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
			};
			httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

			var httpResponse = await _httpClient.SendAsync(httpRequest);
			var responseContent = await httpResponse.Content.ReadAsStringAsync();

			if (!httpResponse.IsSuccessStatusCode)
			{
				_logger.LogError($"? Vertex AI error: {httpResponse.StatusCode} - {responseContent}");
				throw new HttpRequestException($"Vertex AI error: {httpResponse.StatusCode}");
			}

			_logger.LogInformation("? Received response from Vertex AI");
			return ExtractTextFromVertexResponse(responseContent);
		}

		private object ConvertToVertexFormat(UniversalAIRequest request)
		{
			var contents = request.Messages.Select(msg => new
			{
				role = msg.Role,
				parts = msg.Content.Select(content =>
				{
					if (content.Type == "text")
					{
						return new { text = content.Text };
					}
					else if (content.Type == "image" || content.Type == "audio")
					{
						return new
						{
							inlineData = new
							{
								mimeType = content.Media!.MimeType,
								data = Convert.ToBase64String(content.Media.Data)
							}
						};
					}
					return new { text = content.Text };
				}).ToArray()
			}).ToArray();

			return new
			{
				contents = contents,
				generationConfig = new
				{
					temperature = request.Config.Temperature,
					topP = request.Config.TopP,
					maxOutputTokens = request.Config.MaxTokens
				}
			};
		}

		private string ExtractTextFromVertexResponse(string jsonResponse)
		{
			try
			{
				using var doc = JsonDocument.Parse(jsonResponse);
				var candidates = doc.RootElement.GetProperty("candidates");
				if (candidates.GetArrayLength() > 0)
				{
					var firstCandidate = candidates[0];
					var content = firstCandidate.GetProperty("content");
					var parts = content.GetProperty("parts");
					if (parts.GetArrayLength() > 0)
					{
						return parts[0].GetProperty("text").GetString() ?? "";
					}
				}
				return "";
			}
			catch (Exception ex)
			{
				_logger.LogError($"? Error parsing Vertex AI response: {ex.Message}");
				return "";
			}
		}

		private string CreateFoodAnalysisPrompt(string? userPrompt)
		{
			var prompt = @"Проанализируй это изображение еды И НАПИТКОВ и верни результат СТРОГО в формате JSON.

Ты - эксперт по питанию и калорийности блюд. Проанализируй изображение и определи:

?? ВАЖНО: Включай В СПИСОК не только еду, но и ВСЕ НАПИТКИ!
- Чай ?
- Кофе ?  
- Соки ??
- Молоко ??
- Компоты ??
- Любые другие напитки

?? КРИТИЧЕСКИ ВАЖНО: ВСЕГДА заполняй ВСЕ поля nutritionPer100g!
НИКОГДА не оставляй proteins, fats, carbs равными 0, если calories > 0!

ПРАВИЛА ЗАПОЛНЕНИЯ БЖУ (включая напитки):

?? БОРЩ И СУПЫ:
- calories: 40-80 ккал
- proteins: 2-4г (мясо + овощи)
- fats: 2-5г (сметана + мясо) 
- carbs: 4-10г (овощи + крупы)

?? ХЛЕБ И ВЫПЕЧКА:
- calories: 220-280 ккал
- proteins: 6-9г
- fats: 1-3г  
- carbs: 45-55г

? НАПИТКИ:
- Чай без сахара: calories: 2, proteins: 0.1, fats: 0.0, carbs: 0.3
- Чай с сахаром: calories: 25, proteins: 0.1, fats: 0.0, carbs: 6.2
- Кофе черный: calories: 5, proteins: 0.2, fats: 0.0, carbs: 1.0
- Кофе с молоком: calories: 35, proteins: 1.8, fats: 1.5, carbs: 4.0
- Сок яблочный: calories: 46, proteins: 0.1, fats: 0.1, carbs: 11.3
- Молоко: calories: 52, proteins: 2.8, fats: 2.5, carbs: 4.7

?? МЯСО И ПТИЦА:
- calories: 150-300 ккал
- proteins: 20-35г
- fats: 5-25г
- carbs: 0-2г

?? ОВОЩИ И ЗЕЛЕНЬ:
- calories: 15-50 ккал
- proteins: 1-3г
- fats: 0.1-1г
- carbs: 2-8г

?? МОЛОЧНЫЕ ПРОДУКТЫ:
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
? Все блюда (супы, каши, мясо)
? Все напитки (чай, кофе, соки, молоко)
? Хлеб, выпечку, десерты
? Овощи, фрукты, зелень
? Молочные продукты

НЕ ПРОПУСКАЙ напитки только потому, что у них мало калорий!

СТРОГИЕ ТРЕБОВАНИЯ:
? ВСЕ 4 поля (calories, proteins, fats, carbs) должны быть заполнены
? НЕ используй 0 для всех полей одновременно (кроме воды)
? Используй правильные единицы: ml для жидкостей, g для твердого
? Включай ВСЕ видимые продукты питания и напитки

ФОРМУЛА ПРОВЕРКИ: 
calories ? (proteins ? 4) + (fats ? 9) + (carbs ? 4)

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

?? ПРОВЕРЬ ПЕРЕД ОТПРАВКОЙ:
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
			return @"Проанализируй изображения тела и верни результат СТРОГО в формате JSON.

Ты - эксперт по фитнесу и анатомии. Проанализируй изображения и определи:
1. Приблизительный процент жира в теле
2. Приблизительный процент мышечной массы
3. Тип телосложения
4. Анализ осанки
5. Общее состояние
6. ИМТ (BMI) и его категорию
7. ОСНОВНОЙ ОБМЕН ВЕЩЕСТВ (BMR) в ккал
8. Категорию метаболизма
9. Рекомендации

?? ВАЖНО: Обязательно рассчитай основной обмен веществ (BMR) используя формулы:

Для мужчин: BMR = 88.362 + (13.397 ? вес в кг) + (4.799 ? рост в см) - (5.677 ? возраст в годах)
Для женщин: BMR = 447.593 + (9.247 ? вес в кг) + (3.098 ? рост в см) - (4.330 ? возраст в годах)

Категории основного обмена:
- Низкий: менее 1200 ккал
- Нормальный: от 1200 до 1800 ккал 
- Высокий: более 1800 ккал

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
    ""basalMetabolicRate"": 1200,
    ""metabolicRateCategory"": ""Нормальный"",
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
}

?? ОБЯЗАТЕЛЬНО укажи basalMetabolicRate (число в ккал) и metabolicRateCategory (""Низкий"", ""Нормальный"" или ""Высокий"")";
		}

		private string CreateVoiceWorkoutAnalysisPrompt(string? workoutType)
		{
			return @"Ты эксперт по распознаванию речи о тренировках. Внимательно прослушай аудио.

ВАЖНО: Отвечай ТОЛЬКО чистым JSON без обёрток ```json

ПРАВИЛА РАСПОЗНАВАНИЯ:
1. ТОЧНО распознай название упражнения
2. ""штанга"" = любое упражнение со штангой
3. ""тяга штанги"" = ""Тяга штанги""  
4. ""жим"" = ""Жим лежа"" или ""Жим штанги""
5. ""приседания"" = ""Приседания""
6. Если просто ""штанга"" - используй ""Тяга штанги""

ПРАВИЛА ВРЕМЕНИ:
- ""начало 21:15"" ? startTime: ""2025-07-08T21:15:00Z""
- ""окончание 22:00"" ? endTime: ""2025-07-08T22:00:00Z""
- Если одно время ? добавь 45 минут для второго

ГРУППЫ МЫШЦ:
- тяга штанги ? ""Спина""
- жим ? ""Грудь""  
- приседания ? ""Ноги""
- штанга (общее) ? ""Спина""

СТРУКТУРА (без обёрток):
{
  ""success"": true,
  ""transcribedText"": ""точная расшифровка без JSON символов"",
  ""workoutData"": {
    ""type"": ""strength"",
    ""startTime"": ""2025-07-08T21:15:00Z"",
    ""endTime"": ""2025-07-08T22:00:00Z"",
    ""estimatedCalories"": 300,
    ""strengthData"": [
      {
        ""name"": ""Тяга штанги"",
        ""muscleGroup"": ""Спина"",
        ""equipment"": ""Штанга"",
        ""workingWeight"": 25,
        ""restTimeSeconds"": 120,
        ""sets"": [
          {
            ""setNumber"": 1,
            ""weight"": 25,
            ""reps"": 10,
            ""isCompleted"": true
          }
        ]
      }
    ],
    ""cardioData"": null,
    ""notes"": []
  }
}

ПРИМЕРЫ:
""Штанга 25 кг"" ? name: ""Тяга штанги"", muscleGroup: ""Спина""
""Жим 40 килограмм"" ? name: ""Жим лежа"", muscleGroup: ""Грудь""
""Приседания 30 кг"" ? name: ""Приседания"", muscleGroup: ""Ноги""";
		}

		private string CreateVoiceFoodAnalysisPrompt(string? mealType)
		{
			return @"Ты - эксперт по анализу голосовых записей о еде. Твоя задача - ТОЧНО расшифровать речь и извлечь информацию о еде.

ВАЖНО: Все ответы должны быть ТОЛЬКО на русском языке. Названия блюд НЕ переводить на английский.

КРИТИЧЕСКИ ВАЖНО:
1. Сначала максимально точно расшифруй аудио - каждое слово важно
2. Используй ТОЧНЫЕ названия блюд, которые слышишь в аудио НА РУССКОМ ЯЗЫКЕ
3. Используй ТОЧНЫЕ количества, которые называются в аудио
4. НЕ заменяй одни блюда другими - если сказано ""уха"", то это УХА, а не ""овощной суп""
5. НЕ округляй количества - если сказано 200 мл, то пиши 200, а не 250
6. ВСЕ названия блюд должны быть на русском языке - НЕ переводить на английский
7. ВСЕ описания должны быть на русском языке
8. Внимательно слушай числа и единицы измерения (граммы/грамм = г, миллилитры/мл = мл)

АЛГОРИТМ РАБОТЫ:
1. Внимательно прослушай аудио
2. Точно расшифруй каждое слово
3. Определи названия блюд БЕЗ ЗАМЕНЫ на похожие
4. Определи точные количества БЕЗ ОКРУГЛЕНИЯ
5. Найди питательную ценность для КОНКРЕТНОГО блюда
6. Сохрани все названия блюд на русском языке

ФОРМАТ ОТВЕТА (строго JSON):
{
  ""success"": true,
  ""transcribedText"": ""ТОЧНАЯ расшифровка аудио без изменений"",
  ""foodItems"": [
    {
      ""name"": ""ТОЧНОЕ название блюда из аудио НА РУССКОМ ЯЗЫКЕ"",
      ""estimatedWeight"": ТОЧНОЕ_ЧИСЛО_ИЗ_АУДИО,
      ""weightType"": ""g"" или ""ml"",
      ""description"": ""Распознано из голосового ввода: [точное название на русском]"",
      ""nutritionPer100g"": {
        ""calories"": ЧИСЛО,
        ""proteins"": ЧИСЛО,
        ""fats"": ЧИСЛО,
        ""carbs"": ЧИСЛО
      },
      ""totalCalories"": ОБЩИЕ_КАЛОРИИ,
      ""confidence"": УВЕРЕННОСТЬ_ОТ_0_ДО_1
    }
  ],
  ""estimatedTotalCalories"": ОБЩИЕ_КАЛОРИИ
}

ПРАВИЛА ДЛЯ ТИПОВ ВЕСА:
- Супы, соки, молоко, напитки, жидкая еда ? ""ml""
- Каши, мясо, хлеб, твердая еда ? ""g""

ПРИМЕРЫ ТОЧНОГО РАСПОЗНАВАНИЯ:
- Слышишь ""уха 200 миллилитров"" ? name: ""Уха"", estimatedWeight: 200, weightType: ""ml""
- Слышишь ""гречка 150 грамм"" ? name: ""Гречка"", estimatedWeight: 150, weightType: ""g""
- Слышишь ""борщ 300 мл"" ? name: ""Борщ"", estimatedWeight: 300, weightType: ""ml""
- Слышишь ""рис 100 грамм"" ? name: ""Рис"", estimatedWeight: 100, weightType: ""g""
- Слышишь ""молоко 250 миллилитров"" ? name: ""Молоко"", estimatedWeight: 250, weightType: ""ml""

НАЗВАНИЯ БЛЮД НА РУССКОМ ЯЗЫКЕ:
- Уха - это УХА (НЕ ""Fish soup"")
- Борщ - это БОРЩ (НЕ ""Borscht"")
- Гречка - это ГРЕЧКА (НЕ ""Buckwheat"")
- Рис - это РИС (НЕ ""Rice"")
- Картошка/Картофель - это КАРТОШКА (НЕ ""Potatoes"")
- Мясо - это МЯСО (НЕ ""Meat"")
- Курица - это КУРИЦА (НЕ ""Chicken"")
- Молоко - это МОЛОКО (НЕ ""Milk"")
- Хлеб - это ХЛЕБ (НЕ ""Bread"")
- Каша - это КАША (НЕ ""Porridge"")
- Суп - это СУП (НЕ ""Soup"")
- Салат - это САЛАТ (НЕ ""Salad"")

ЕДИНИЦЫ ИЗМЕРЕНИЯ:
- Граммы/грамм/г = используй ""g""
- Миллилитры/мл = используй ""ml""
- Килограммы/кг = переводи в граммы (1 кг = 1000 г)
- Литры/л = переводи в миллилитры (1 л = 1000 мл)

УРОВНИ УВЕРЕННОСТИ:
- 0.9-1.0: Очень четкое аудио, точное название блюда распознано
- 0.7-0.8: Четкое аудио, категория блюда распознана
- 0.5-0.6: Не очень четкое аудио, примерная догадка
- Ниже 0.5: Очень неясно, низкая уверенность

ТОЧНОСТЬ ПИТАТЕЛЬНЫХ ДАННЫХ:
- Используй точные питательные данные для конкретного блюда
- Если точных данных нет, используй данные для похожего блюда
- Всегда рассчитывай общие калории на основе размера порции
- Будь осторожен с оценками, если не уверен

ЕСЛИ РАСПОЗНАВАНИЕ НЕ УДАЛОСЬ:
{
  ""success"": false,
  ""errorMessage"": ""Не удалось четко распознать речь или информацию о еде из аудио""
}

ДОПОЛНИТЕЛЬНЫЕ ИНСТРУКЦИИ:
- Всегда сохраняй русские названия блюд на русском языке
- Обращай внимание на размеры порций - они часто упоминаются точно
- Не предполагай стандартные порции - используй то, что реально было сказано
- Если упоминается несколько блюд, включи все в массив
- Высокую уверенность ставь только когда точно уверен в распознавании
- Если слышишь способы приготовления (жареный, вареный и т.д.), включи их в русское название
- Все ответы должны быть на русском языке

ЗАПОМНИ: Твоя задача - быть максимально точным, не додумывать и не заменять то, что реально сказано в аудио! ВСЕ НАЗВАНИЯ БЛЮД ДОЛЖНЫ БЫТЬ НА РУССКОМ ЯЗЫКЕ!";
		}

		private string DetectAudioMimeType(byte[] audioData)
		{
			if (audioData.Length < 4) return "audio/ogg";

			if (audioData[0] == 0x4F && audioData[1] == 0x67 && audioData[2] == 0x67 && audioData[3] == 0x53)
				return "audio/ogg";

			if (audioData[0] == 0xFF && (audioData[1] & 0xE0) == 0xE0)
				return "audio/mp3";

			if (audioData[0] == 0x52 && audioData[1] == 0x49 && audioData[2] == 0x46 && audioData[3] == 0x46)
				return "audio/wav";

			return "audio/ogg";
		}

		private FoodScanResponse ParseFoodAnalysisResponse(string response)
		{
			try
			{
				var cleanJson = ExtractJsonFromResponse(response);
				var options = new JsonSerializerOptions
				{
					PropertyNameCaseInsensitive = true,
					PropertyNamingPolicy = JsonNamingPolicy.CamelCase
				};

				return JsonSerializer.Deserialize<FoodScanResponse>(cleanJson, options) ?? new FoodScanResponse
				{
					Success = false,
					ErrorMessage = "Не удалось обработать ответ от ИИ"
				};
			}
			catch (Exception ex)
			{
				_logger.LogError($"? Error parsing food analysis: {ex.Message}");
				return new FoodScanResponse
				{
					Success = false,
					ErrorMessage = "Ошибка обработки ответа"
				};
			}
		}

		private BodyScanResponse ParseBodyAnalysisResponse(string response)
		{
			try
			{
				var cleanJson = ExtractJsonFromResponse(response);
				var options = new JsonSerializerOptions
				{
					PropertyNameCaseInsensitive = true,
					PropertyNamingPolicy = JsonNamingPolicy.CamelCase
				};

				return JsonSerializer.Deserialize<BodyScanResponse>(cleanJson, options) ?? new BodyScanResponse
				{
					Success = false,
					ErrorMessage = "Не удалось обработать ответ от ИИ"
				};
			}
			catch (Exception ex)
			{
				_logger.LogError($"? Error parsing body analysis: {ex.Message}");
				return new BodyScanResponse
				{
					Success = false,
					ErrorMessage = "Ошибка обработки ответа"
				};
			}
		}

		private VoiceWorkoutResponse ParseVoiceWorkoutResponse(string response)
		{
			try
			{
				var cleanJson = ExtractJsonFromResponse(response);
				var options = new JsonSerializerOptions
				{
					PropertyNameCaseInsensitive = true,
					PropertyNamingPolicy = JsonNamingPolicy.CamelCase
				};

				return JsonSerializer.Deserialize<VoiceWorkoutResponse>(cleanJson, options) ?? new VoiceWorkoutResponse
				{
					Success = false,
					ErrorMessage = "Не удалось обработать ответ от ИИ"
				};
			}
			catch (Exception ex)
			{
				_logger.LogError($"? Error parsing voice workout: {ex.Message}");
				return new VoiceWorkoutResponse
				{
					Success = false,
					ErrorMessage = "Ошибка обработки ответа"
				};
			}
		}

		private VoiceFoodResponse ParseVoiceFoodResponse(string response)
		{
			try
			{
				var cleanJson = ExtractJsonFromResponse(response);
				var options = new JsonSerializerOptions
				{
					PropertyNameCaseInsensitive = true,
					PropertyNamingPolicy = JsonNamingPolicy.CamelCase
				};

				return JsonSerializer.Deserialize<VoiceFoodResponse>(cleanJson, options) ?? new VoiceFoodResponse
				{
					Success = false,
					ErrorMessage = "Не удалось обработить ответ от ИИ"
				};
			}
			catch (Exception ex)
			{
				_logger.LogError($"? Error parsing voice food: {ex.Message}");
				return new VoiceFoodResponse
				{
					Success = false,
					ErrorMessage = "Ошибка обработки ответа"
				};
			}
		}

		private string ExtractJsonFromResponse(string response)
		{
			var jsonMatch = System.Text.RegularExpressions.Regex.Match(response, @"\{.*\}", System.Text.RegularExpressions.RegexOptions.Singleline);
			return jsonMatch.Success ? jsonMatch.Value : response.Trim();
		}
	}
}