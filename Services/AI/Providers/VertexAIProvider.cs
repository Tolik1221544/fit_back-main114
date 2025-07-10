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
					ErrorMessage = $"������ �������: {ex.Message}"
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

				// ��������� �����������
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
					ErrorMessage = $"������ �������: {ex.Message}"
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
					ErrorMessage = $"������ ������� �����: {ex.Message}"
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
					ErrorMessage = $"������ ������� �����: {ex.Message}"
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
								new UniversalContent { Type = "text", Text = "������ 'OK' ���� �� ���������" }
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

			// ������������ ������������� ������ � ������ Vertex AI
			var vertexRequest = ConvertToVertexFormat(request);
			var jsonContent = JsonSerializer.Serialize(vertexRequest, new JsonSerializerOptions
			{
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase
			});

			_logger.LogDebug($"?? Sending request to Vertex AI: {url}");

			// �������� �����
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
			var prompt = @"������������� ��� ����������� ��� � �������� � ����� ��������� ������ � ������� JSON.

�� - ������� �� ������� � ������������ ����. ������������� ����������� � ��������:

?? �����: ������� � ������ �� ������ ���, �� � ��� �������!
- ��� ?
- ���� ?  
- ���� ??
- ������ ??
- ������� ??
- ����� ������ �������

?? ���������� �����: ������ �������� ��� ���� nutritionPer100g!
������� �� �������� proteins, fats, carbs ������� 0, ���� calories > 0!

������� ���������� ��� (������� �������):

?? ���� � ����:
- calories: 40-80 ����
- proteins: 2-4� (���� + �����)
- fats: 2-5� (������� + ����) 
- carbs: 4-10� (����� + �����)

?? ���� � �������:
- calories: 220-280 ����
- proteins: 6-9�
- fats: 1-3�  
- carbs: 45-55�

? �������:
- ��� ��� ������: calories: 2, proteins: 0.1, fats: 0.0, carbs: 0.3
- ��� � �������: calories: 25, proteins: 0.1, fats: 0.0, carbs: 6.2
- ���� ������: calories: 5, proteins: 0.2, fats: 0.0, carbs: 1.0
- ���� � �������: calories: 35, proteins: 1.8, fats: 1.5, carbs: 4.0
- ��� ��������: calories: 46, proteins: 0.1, fats: 0.1, carbs: 11.3
- ������: calories: 52, proteins: 2.8, fats: 2.5, carbs: 4.7

?? ���� � �����:
- calories: 150-300 ����
- proteins: 20-35�
- fats: 5-25�
- carbs: 0-2�

?? ����� � ������:
- calories: 15-50 ����
- proteins: 1-3�
- fats: 0.1-1�
- carbs: 2-8�

?? �������� ��������:
- calories: 50-300 ����  
- proteins: 2-20�
- fats: 2-30�
- carbs: 3-6�

���������� ������� (��������� ��� ������):

{
  ""name"": ""���� �� ��������"",
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
  ""name"": ""��� ������ ��� ������"",
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
  ""name"": ""���� ������"", 
  ""estimatedWeight"": 50,
  ""weightType"": ""g"",
  ""nutritionPer100g"": {
    ""calories"": 250,
    ""proteins"": 8.1,
    ""fats"": 1.0,
    ""carbs"": 48.8
  }
}

����������� ������� � ������:
? ��� ����� (����, ����, ����)
? ��� ������� (���, ����, ����, ������)
? ����, �������, �������
? �����, ������, ������
? �������� ��������

�� ��������� ������� ������ ������, ��� � ��� ���� �������!

������� ����������:
? ��� 4 ���� (calories, proteins, fats, carbs) ������ ���� ���������
? �� ��������� 0 ��� ���� ����� ������������ (����� ����)
? ��������� ���������� �������: ml ��� ���������, g ��� ��������
? ������� ��� ������� �������� ������� � �������

������� ��������: 
calories ? (proteins ? 4) + (fats ? 9) + (carbs ? 4)

����� ��������� ������ � ������� JSON:

{
  ""success"": true,
  ""foodItems"": [
    {
      ""name"": ""����"",
      ""estimatedWeight"": 300,
      ""weightType"": ""ml"",
      ""description"": ""������ �����"",
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
      ""name"": ""��� ������"",
      ""estimatedWeight"": 200,
      ""weightType"": ""ml"",
      ""description"": ""����� ������� ���"",
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
  ""fullDescription"": ""����, ���� � ���""
}

?? ������� ����� ���������:
- �������� �� ��� ������� �������� � �������?
- ��� ���� nutritionPer100g ���������?
- ��� �� ����������� ����� ���/����?

���� ����������� �� �������� ��� � ��������, �����:
{
  ""success"": false,
  ""errorMessage"": ""�� ����������� �� ���������� ��� ��� �������""
}";
			if (!string.IsNullOrEmpty(userPrompt))
			{
				prompt += $"\n\n�������������� ���������� �� ������������: {userPrompt}";
			}
			return prompt;
		}

		private string CreateBodyAnalysisPrompt(decimal? weight, decimal? height, int? age, string? gender, string? goals)
		{
			return @"������������� ����������� ���� � ����� ��������� ������ � ������� JSON.

�� - ������� �� ������� � ��������. ������������� ����������� � ��������:
1. ��������������� ������� ���� � ����
2. ��������������� ������� �������� �����
3. ��� ������������
4. ������ ������
5. ����� ���������
6. ��� (BMI) � ��� ���������
7. �������� ����� ������� (BMR) � ����
8. ��������� �����������
9. ������������

?? �����: ����������� ��������� �������� ����� ������� (BMR) ��������� �������:

��� ������: BMR = 88.362 + (13.397 ? ��� � ��) + (4.799 ? ���� � ��) - (5.677 ? ������� � �����)
��� ������: BMR = 447.593 + (9.247 ? ��� � ��) + (3.098 ? ���� � ��) - (4.330 ? ������� � �����)

��������� ��������� ������:
- ������: ����� 1200 ����
- ����������: �� 1200 �� 1800 ���� 
- �������: ����� 1800 ����

����� ��������� ������ � ������� JSON:

{
  ""success"": true,
  ""bodyAnalysis"": {
    ""estimatedBodyFatPercentage"": 15.5,
    ""estimatedMusclePercentage"": 42.0,
    ""bodyType"": ""��������"",
    ""postureAnalysis"": ""��������� ��������� � ������"",
    ""overallCondition"": ""������� ���������� �����"",
    ""bmi"": 23.4,
    ""bmiCategory"": ""���������� ���"",
    ""basalMetabolicRate"": 1200,
    ""metabolicRateCategory"": ""����������"",
    ""estimatedWaistCircumference"": 80,
    ""estimatedChestCircumference"": 95,
    ""estimatedHipCircumference"": 90,
    ""exerciseRecommendations"": [
      ""������� ���������� 3 ���� � ������"",
      ""������ 2 ���� � ������""
    ],
    ""nutritionRecommendations"": [
      ""��������� ����������� �����"",
      ""�������������� ��������""
    ],
    ""trainingFocus"": ""����� �������� �����""
  },
  ""recommendations"": [
    ""������������ 1"",
    ""������������ 2""
  ],
  ""fullAnalysis"": ""��������� ������ ����""
}

?? ����������� ����� basalMetabolicRate (����� � ����) � metabolicRateCategory (""������"", ""����������"" ��� ""�������"")";
		}

		private string CreateVoiceWorkoutAnalysisPrompt(string? workoutType)
		{
			return @"�� ������� �� ������������� ���� � �����������. ����������� ��������� �����.

�����: ������� ������ ������ JSON ��� ������ ```json

������� �������������:
1. ����� ��������� �������� ����������
2. ""������"" = ����� ���������� �� �������
3. ""���� ������"" = ""���� ������""  
4. ""���"" = ""��� ����"" ��� ""��� ������""
5. ""����������"" = ""����������""
6. ���� ������ ""������"" - ��������� ""���� ������""

������� �������:
- ""������ 21:15"" ? startTime: ""2025-07-08T21:15:00Z""
- ""��������� 22:00"" ? endTime: ""2025-07-08T22:00:00Z""
- ���� ���� ����� ? ������ 45 ����� ��� �������

������ ����:
- ���� ������ ? ""�����""
- ��� ? ""�����""  
- ���������� ? ""����""
- ������ (�����) ? ""�����""

��������� (��� ������):
{
  ""success"": true,
  ""transcribedText"": ""������ ����������� ��� JSON ��������"",
  ""workoutData"": {
    ""type"": ""strength"",
    ""startTime"": ""2025-07-08T21:15:00Z"",
    ""endTime"": ""2025-07-08T22:00:00Z"",
    ""estimatedCalories"": 300,
    ""strengthData"": [
      {
        ""name"": ""���� ������"",
        ""muscleGroup"": ""�����"",
        ""equipment"": ""������"",
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

�������:
""������ 25 ��"" ? name: ""���� ������"", muscleGroup: ""�����""
""��� 40 ���������"" ? name: ""��� ����"", muscleGroup: ""�����""
""���������� 30 ��"" ? name: ""����������"", muscleGroup: ""����""";
		}

		private string CreateVoiceFoodAnalysisPrompt(string? mealType)
		{
			return @"�� - ������� �� ������� ��������� ������� � ���. ���� ������ - ����� ������������ ���� � ������� ���������� � ���.

�����: ��� ������ ������ ���� ������ �� ������� �����. �������� ���� �� ���������� �� ����������.

���������� �����:
1. ������� ����������� ����� ��������� ����� - ������ ����� �����
2. ��������� ������ �������� ����, ������� ������� � ����� �� ������� �����
3. ��������� ������ ����������, ������� ���������� � �����
4. �� ������� ���� ����� ������� - ���� ������� ""���"", �� ��� ���, � �� ""������� ���""
5. �� �������� ���������� - ���� ������� 200 ��, �� ���� 200, � �� 250
6. ��� �������� ���� ������ ���� �� ������� ����� - �� ���������� �� ����������
7. ��� �������� ������ ���� �� ������� �����
8. ����������� ������ ����� � ������� ��������� (������/����� = �, ����������/�� = ��)

�������� ������:
1. ����������� ��������� �����
2. ����� ��������� ������ �����
3. �������� �������� ���� ��� ������ �� �������
4. �������� ������ ���������� ��� ����������
5. ����� ����������� �������� ��� ����������� �����
6. ������� ��� �������� ���� �� ������� �����

������ ������ (������ JSON):
{
  ""success"": true,
  ""transcribedText"": ""������ ����������� ����� ��� ���������"",
  ""foodItems"": [
    {
      ""name"": ""������ �������� ����� �� ����� �� ������� �����"",
      ""estimatedWeight"": ������_�����_��_�����,
      ""weightType"": ""g"" ��� ""ml"",
      ""description"": ""���������� �� ���������� �����: [������ �������� �� �������]"",
      ""nutritionPer100g"": {
        ""calories"": �����,
        ""proteins"": �����,
        ""fats"": �����,
        ""carbs"": �����
      },
      ""totalCalories"": �����_�������,
      ""confidence"": �����������_��_0_��_1
    }
  ],
  ""estimatedTotalCalories"": �����_�������
}

������� ��� ����� ����:
- ����, ����, ������, �������, ������ ��� ? ""ml""
- ����, ����, ����, ������� ��� ? ""g""

������� ������� �������������:
- ������� ""��� 200 �����������"" ? name: ""���"", estimatedWeight: 200, weightType: ""ml""
- ������� ""������ 150 �����"" ? name: ""������"", estimatedWeight: 150, weightType: ""g""
- ������� ""���� 300 ��"" ? name: ""����"", estimatedWeight: 300, weightType: ""ml""
- ������� ""��� 100 �����"" ? name: ""���"", estimatedWeight: 100, weightType: ""g""
- ������� ""������ 250 �����������"" ? name: ""������"", estimatedWeight: 250, weightType: ""ml""

�������� ���� �� ������� �����:
- ��� - ��� ��� (�� ""Fish soup"")
- ���� - ��� ���� (�� ""Borscht"")
- ������ - ��� ������ (�� ""Buckwheat"")
- ��� - ��� ��� (�� ""Rice"")
- ��������/��������� - ��� �������� (�� ""Potatoes"")
- ���� - ��� ���� (�� ""Meat"")
- ������ - ��� ������ (�� ""Chicken"")
- ������ - ��� ������ (�� ""Milk"")
- ���� - ��� ���� (�� ""Bread"")
- ���� - ��� ���� (�� ""Porridge"")
- ��� - ��� ��� (�� ""Soup"")
- ����� - ��� ����� (�� ""Salad"")

������� ���������:
- ������/�����/� = ��������� ""g""
- ����������/�� = ��������� ""ml""
- ����������/�� = �������� � ������ (1 �� = 1000 �)
- �����/� = �������� � ���������� (1 � = 1000 ��)

������ �����������:
- 0.9-1.0: ����� ������ �����, ������ �������� ����� ����������
- 0.7-0.8: ������ �����, ��������� ����� ����������
- 0.5-0.6: �� ����� ������ �����, ��������� �������
- ���� 0.5: ����� ������, ������ �����������

�������� ����������� ������:
- ��������� ������ ����������� ������ ��� ����������� �����
- ���� ������ ������ ���, ��������� ������ ��� �������� �����
- ������ ����������� ����� ������� �� ������ ������� ������
- ���� ��������� � ��������, ���� �� ������

���� ������������� �� �������:
{
  ""success"": false,
  ""errorMessage"": ""�� ������� ����� ���������� ���� ��� ���������� � ��� �� �����""
}

�������������� ����������:
- ������ �������� ������� �������� ���� �� ������� �����
- ������� �������� �� ������� ������ - ��� ����� ����������� �����
- �� ����������� ����������� ������ - ��������� ��, ��� ������� ���� �������
- ���� ����������� ��������� ����, ������ ��� � ������
- ������� ����������� ����� ������ ����� ����� ������ � �������������
- ���� ������� ������� ������������� (�������, ������� � �.�.), ������ �� � ������� ��������
- ��� ������ ������ ���� �� ������� �����

�������: ���� ������ - ���� ����������� ������, �� ���������� � �� �������� ��, ��� ������� ������� � �����! ��� �������� ���� ������ ���� �� ������� �����!";
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
					ErrorMessage = "�� ������� ���������� ����� �� ��"
				};
			}
			catch (Exception ex)
			{
				_logger.LogError($"? Error parsing food analysis: {ex.Message}");
				return new FoodScanResponse
				{
					Success = false,
					ErrorMessage = "������ ��������� ������"
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
					ErrorMessage = "�� ������� ���������� ����� �� ��"
				};
			}
			catch (Exception ex)
			{
				_logger.LogError($"? Error parsing body analysis: {ex.Message}");
				return new BodyScanResponse
				{
					Success = false,
					ErrorMessage = "������ ��������� ������"
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
					ErrorMessage = "�� ������� ���������� ����� �� ��"
				};
			}
			catch (Exception ex)
			{
				_logger.LogError($"? Error parsing voice workout: {ex.Message}");
				return new VoiceWorkoutResponse
				{
					Success = false,
					ErrorMessage = "������ ��������� ������"
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
					ErrorMessage = "�� ������� ���������� ����� �� ��"
				};
			}
			catch (Exception ex)
			{
				_logger.LogError($"? Error parsing voice food: {ex.Message}");
				return new VoiceFoodResponse
				{
					Success = false,
					ErrorMessage = "������ ��������� ������"
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