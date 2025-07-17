using FitnessTracker.API.DTOs;
using FitnessTracker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FitnessTracker.API.Controllers
{
    /// <summary>
    /// 🎤 Управление голосовыми файлами пользователей
    /// </summary>
    [ApiController]
    [Route("api/voice-files")]
    [Authorize]
    [Produces("application/json")]
    public class VoiceFilesController : ControllerBase
    {
        private readonly IVoiceFileService _voiceFileService;
        private readonly ILogger<VoiceFilesController> _logger;

        public VoiceFilesController(
            IVoiceFileService voiceFileService,
            ILogger<VoiceFilesController> logger)
        {
            _voiceFileService = voiceFileService;
            _logger = logger;
        }

        /// <summary>
        /// 📋 Получить список всех голосовых файлов пользователя
        /// </summary>
        /// <returns>Список голосовых файлов с URL для скачивания</returns>
        /// <response code="200">Список файлов успешно получен</response>
        /// <response code="401">Требуется авторизация</response>
        /// <example>
        /// Возвращает:
        /// {
        ///   "totalFiles": 15,
        ///   "workoutFiles": 8,
        ///   "foodFiles": 7,
        ///   "files": [
        ///     {
        ///       "fileId": "abc123def456",
        ///       "fileName": "user123_workout_abc123def456_20250717_143022.wav",
        ///       "voiceType": "workout",
        ///       "sizeMB": 2.34,
        ///       "createdAt": "2025-07-17T14:30:22Z",
        ///       "downloadUrl": "/api/voice-files/download/abc123def456"
        ///     }
        ///   ]
        /// }
        /// </example>
        [HttpGet]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetUserVoiceFiles()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var allFiles = await _voiceFileService.GetUserVoiceFilesAsync(userId);
                var filesList = allFiles.ToList();

                var workoutFiles = filesList.Where(f => f.VoiceType == "workout").ToList();
                var foodFiles = filesList.Where(f => f.VoiceType == "food").ToList();

                return Ok(new
                {
                    totalFiles = filesList.Count,
                    workoutFiles = workoutFiles.Count,
                    foodFiles = foodFiles.Count,
                    totalSizeMB = Math.Round(filesList.Sum(f => f.SizeMB), 2),
                    files = filesList
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error getting voice files: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 📊 Статистика голосовых файлов пользователя
        /// </summary>
        /// <returns>Детальная статистика по голосовым файлам</returns>
        /// <response code="200">Статистика успешно получена</response>
        /// <response code="401">Требуется авторизация</response>
        /// <example>
        /// Возвращает:
        /// {
        ///   "totalFiles": 15,
        ///   "workoutFiles": 8,
        ///   "foodFiles": 7,
        ///   "totalSizeMB": 45.67,
        ///   "averageFileSizeMB": 3.04,
        ///   "oldestFileDate": "2025-06-15T10:20:30Z",
        ///   "newestFileDate": "2025-07-17T14:30:22Z",
        ///   "filesThisMonth": 12,
        ///   "filesToday": 3
        /// }
        /// </example>
        [HttpGet("stats")]
        [ProducesResponseType(typeof(VoiceFilesStatsDto), 200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetVoiceFilesStats()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var stats = await _voiceFileService.GetVoiceFilesStatsAsync(userId);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error getting voice files stats: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 🧹 Очистить старые голосовые файлы (admin only)
        /// </summary>
        /// <param name="maxAgeDays">Максимальный возраст файлов в днях (по умолчанию 30 дней)</param>
        /// <returns>Количество удаленных файлов</returns>
        /// <response code="200">Очистка выполнена успешно</response>
        /// <response code="401">Требуется авторизация</response>
        /// <remarks>
        /// Удаляет все голосовые файлы старше указанного количества дней.
        /// Используется для автоматической очистки дискового пространства.
        /// </remarks>
        [HttpPost("cleanup")]
        [ProducesResponseType(200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> CleanupOldFiles([FromQuery] int maxAgeDays = 30)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                // В реальном приложении здесь должна быть проверка прав администратора
                // Пока что разрешаем любому авторизованному пользователю

                var maxAge = TimeSpan.FromDays(maxAgeDays);
                var deletedCount = await _voiceFileService.CleanupOldFilesAsync(maxAge);

                return Ok(new
                {
                    cleaned = true,
                    deletedFiles = deletedCount,
                    maxAgeDays = maxAgeDays,
                    message = $"Cleaned up {deletedCount} files older than {maxAgeDays} days"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error during cleanup: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 🔍 Поиск голосовых файлов с фильтрами
        /// </summary>
        /// <param name="voiceType">Тип голосовых файлов: "workout" или "food" (опционально)</param>
        /// <param name="startDate">Дата начала периода (опционально)</param>
        /// <param name="endDate">Дата окончания периода (опционально)</param>
        /// <param name="page">Номер страницы (по умолчанию 1)</param>
        /// <param name="pageSize">Количество файлов на странице (по умолчанию 20)</param>
        /// <returns>Отфильтрованный список голосовых файлов с пагинацией</returns>
        /// <response code="200">Поиск выполнен успешно</response>
        /// <response code="401">Требуется авторизация</response>
        [HttpGet("search")]
        [ProducesResponseType(200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> SearchVoiceFiles(
            [FromQuery] string? voiceType = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var allFiles = await _voiceFileService.GetUserVoiceFilesAsync(userId);
                var query = allFiles.AsQueryable();

                // Фильтрация по типу
                if (!string.IsNullOrEmpty(voiceType))
                {
                    query = query.Where(f => f.VoiceType.Equals(voiceType, StringComparison.OrdinalIgnoreCase));
                }

                // Фильтрация по дате
                if (startDate.HasValue)
                {
                    query = query.Where(f => f.CreatedAt >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(f => f.CreatedAt <= endDate.Value);
                }

                // Сортировка по дате создания (новые сначала)
                query = query.OrderByDescending(f => f.CreatedAt);

                var totalCount = query.Count();
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                // Пагинация
                var files = query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return Ok(new
                {
                    files = files,
                    pagination = new
                    {
                        currentPage = page,
                        pageSize = pageSize,
                        totalCount = totalCount,
                        totalPages = totalPages,
                        hasNext = page < totalPages,
                        hasPrevious = page > 1
                    },
                    filters = new
                    {
                        voiceType = voiceType,
                        startDate = startDate,
                        endDate = endDate
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error searching voice files: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 📂 Получить файлы по типу
        /// </summary>
        /// <param name="voiceType">Тип файлов: "workout" или "food"</param>
        /// <returns>Список файлов указанного типа</returns>
        /// <response code="200">Файлы найдены</response>
        /// <response code="400">Неверный тип файлов</response>
        /// <response code="401">Требуется авторизация</response>
        [HttpGet("by-type/{voiceType}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetFilesByType(string voiceType)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                if (voiceType != "workout" && voiceType != "food")
                {
                    return BadRequest(new { error = "Voice type must be 'workout' or 'food'" });
                }

                var allFiles = await _voiceFileService.GetUserVoiceFilesAsync(userId);
                var filteredFiles = allFiles.Where(f => f.VoiceType.Equals(voiceType, StringComparison.OrdinalIgnoreCase))
                                           .OrderByDescending(f => f.CreatedAt)
                                           .ToList();

                return Ok(new
                {
                    voiceType = voiceType,
                    count = filteredFiles.Count,
                    totalSizeMB = Math.Round(filteredFiles.Sum(f => f.SizeMB), 2),
                    files = filteredFiles
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error getting files by type {voiceType}: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 📥 Скачать голосовой файл по ID
        /// </summary>
        /// <param name="fileId">Уникальный идентификатор файла</param>
        /// <returns>Аудио файл для скачивания</returns>
        /// <response code="200">Файл найден и возвращается для скачивания</response>
        /// <response code="404">Файл не найден</response>
        /// <response code="401">Требуется авторизация</response>
        /// <response code="403">Файл принадлежит другому пользователю</response>
        /// <remarks>
        /// Загружает оригинальный голосовой файл, который был сохранен при анализе через AI.
        /// 
        /// Поддерживаемые форматы:
        /// - WAV (audio/wav)
        /// - MP3 (audio/mpeg) 
        /// - OGG (audio/ogg)
        /// - WebM (audio/webm)
        /// - M4A (audio/mp4)
        /// 
        /// Файл автоматически скачивается в браузере с оригинальным именем.
        /// </remarks>
        [HttpGet("download/{fileId}")]
        [ProducesResponseType(typeof(FileResult), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        public async Task<IActionResult> DownloadVoiceFile(string fileId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _voiceFileService.DownloadVoiceFileAsync(userId, fileId);

                if (result == null)
                {
                    return NotFound(new { error = "Voice file not found or access denied" });
                }

                var (data, fileName, contentType) = result.Value;

                _logger.LogInformation($"📥 Downloaded voice file: {fileName} by {userId}");

                return File(data, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error downloading voice file {fileId}: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 🗑️ Удалить голосовой файл
        /// </summary>
        /// <param name="fileId">Уникальный идентификатор файла</param>
        /// <returns>Результат удаления</returns>
        /// <response code="200">Файл успешно удален</response>
        /// <response code="404">Файл не найден</response>
        /// <response code="401">Требуется авторизация</response>
        /// <response code="403">Файл принадлежит другому пользователю</response>
        [HttpDelete("{fileId}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        public async Task<IActionResult> DeleteVoiceFile(string fileId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var success = await _voiceFileService.DeleteVoiceFileAsync(userId, fileId);

                if (!success)
                {
                    return NotFound(new { error = "Voice file not found or access denied" });
                }

                _logger.LogInformation($"🗑️ Deleted voice file: {fileId} by {userId}");

                return Ok(new
                {
                    deleted = true,
                    fileId = fileId,
                    message = "Voice file deleted successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error deleting voice file {fileId}: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}