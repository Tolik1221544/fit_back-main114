using Microsoft.AspNetCore.Mvc;
using FitnessTracker.API.Services.AI;
using System.Text.Json;

namespace FitnessTracker.API.Controllers
{
    [ApiController]
    [Route("api/debug")]
    public class DebugVertexController : ControllerBase
    {
        private readonly IGoogleCloudTokenService _tokenService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DebugVertexController> _logger;

        public DebugVertexController(
            IGoogleCloudTokenService tokenService,
            IConfiguration configuration,
            ILogger<DebugVertexController> logger)
        {
            _tokenService = tokenService;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet("vertex-auth")]
        public async Task<IActionResult> TestVertexAuth()
        {
            try
            {
                _logger.LogInformation("🔍 Starting Vertex AI authentication debug...");

                // 1. Проверяем конфигурацию
                var projectId = _configuration["GoogleCloud:ProjectId"];
                var serviceAccountPath = _configuration["GoogleCloud:ServiceAccountPath"];
                var location = _configuration["GoogleCloud:Location"];

                _logger.LogInformation($"📋 Config - ProjectId: {projectId}, Path: {serviceAccountPath}, Location: {location}");

                // 2. Проверяем файл Service Account
                var fileExists = System.IO.File.Exists(serviceAccountPath);
                _logger.LogInformation($"📁 Service Account file exists: {fileExists}");

                if (fileExists)
                {
                    var jsonContent = await System.IO.File.ReadAllTextAsync(serviceAccountPath);
                    var serviceAccountData = JsonSerializer.Deserialize<JsonElement>(jsonContent);

                    var fileProjectId = serviceAccountData.GetProperty("project_id").GetString();
                    var clientEmail = serviceAccountData.GetProperty("client_email").GetString();

                    _logger.LogInformation($"📄 File ProjectId: {fileProjectId}, Email: {clientEmail}");
                }

                // 3. Тестируем получение токена
                var token = await _tokenService.GetAccessTokenAsync();
                var tokenPreview = $"{token[..Math.Min(20, token.Length)]}...{token[Math.Max(0, token.Length - 10)..]}";

                _logger.LogInformation($"🔑 Token obtained: {tokenPreview}");

                // 4. Тестируем простой запрос к Vertex AI
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var testUrl = $"https://aiplatform.googleapis.com/v1/projects/{projectId}/locations/{location}";
                _logger.LogInformation($"🌐 Testing URL: {testUrl}");

                var response = await httpClient.GetAsync(testUrl);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation($"📡 Response Status: {response.StatusCode}");
                _logger.LogInformation($"📡 Response Content: {responseContent[..Math.Min(500, responseContent.Length)]}");

                // 5. Тестируем прямой запрос к модели
                var modelUrl = $"https://{location}-aiplatform.googleapis.com/v1/projects/{projectId}/locations/{location}/publishers/google/models/gemini-2.5-flash:generateContent";

                var testRequest = new
                {
                    contents = new[]
                    {
                        new
                        {
                            role = "user",
                            parts = new[] { new { text = "Say 'Hello from Vertex AI!'" } }
                        }
                    },
                    generation_config = new { temperature = 0.1 }
                };

                var jsonRequest = JsonSerializer.Serialize(testRequest);
                var content = new StringContent(jsonRequest, System.Text.Encoding.UTF8, "application/json");

                _logger.LogInformation($"🤖 Testing model URL: {modelUrl}");
                var modelResponse = await httpClient.PostAsync(modelUrl, content);
                var modelResponseContent = await modelResponse.Content.ReadAsStringAsync();

                _logger.LogInformation($"🤖 Model Response Status: {modelResponse.StatusCode}");
                _logger.LogInformation($"🤖 Model Response Content: {modelResponseContent[..Math.Min(500, modelResponseContent.Length)]}");

                return Ok(new
                {
                    success = true,
                    config = new { projectId, serviceAccountPath, location },
                    fileExists,
                    tokenObtained = !string.IsNullOrEmpty(token),
                    tokenPreview,
                    apiTest = new
                    {
                        url = testUrl,
                        status = response.StatusCode.ToString(),
                        success = response.IsSuccessStatusCode
                    },
                    modelTest = new
                    {
                        url = modelUrl,
                        status = modelResponse.StatusCode.ToString(),
                        success = modelResponse.IsSuccessStatusCode,
                        response = modelResponseContent
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Debug failed: {ex.Message}");
                _logger.LogError($"❌ Stack trace: {ex.StackTrace}");

                return BadRequest(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }
    }
}