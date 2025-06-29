using FitnessTracker.API.DTOs;
using FitnessTracker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace FitnessTracker.API.Controllers
{
    /// <summary>
    /// 🏃‍♂️ Управление активностями и тренировками
    /// </summary>
    [ApiController]
    [Route("api/activity")]
    [Authorize]
    [Produces("application/json")]
    public class ActivityController : ControllerBase
    {
        private readonly IActivityService _activityService;
        private readonly IMissionService _missionService;

        public ActivityController(IActivityService activityService, IMissionService missionService)
        {
            _activityService = activityService;
            _missionService = missionService;
        }

        /// <summary>
        /// 📋 Получить список активностей с фильтрами
        /// </summary>
        /// <param name="type">Тип тренировки: "strength" (силовая) или "cardio" (кардио)</param>
        /// <param name="startDate">Дата начала периода (YYYY-MM-DD)</param>
        /// <param name="endDate">Дата окончания периода (YYYY-MM-DD)</param>
        /// <returns>Список активностей пользователя</returns>
        /// <response code="200">Список активностей успешно получен</response>
        /// <response code="401">Требуется авторизация</response>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<ActivityDto>), 200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetActivities(
            [FromQuery] string? type = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var activities = await _activityService.GetUserActivitiesAsync(userId, type, startDate, endDate);
                return Ok(activities);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// ➕ Добавить новую тренировку
        /// </summary>
        /// <param name="request">
        /// Данные тренировки. ВАЖНО: 
        /// - Для силовой тренировки заполните strengthData
        /// - Для кардио тренировки заполните cardioData
        /// - Нельзя заполнять оба поля одновременно
        /// </param>
        /// <returns>Созданная активность</returns>
        /// <response code="200">Тренировка успешно добавлена</response>
        /// <response code="400">Неверные данные запроса</response>
        /// <response code="401">Требуется авторизация</response>
        [HttpPost]
        [ProducesResponseType(typeof(ActivityDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> AddActivity([FromBody] AddActivityRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                if (string.IsNullOrEmpty(request.Type))
                    return BadRequest(new { error = "Type is required" });

                if (request.StartDate == default)
                    return BadRequest(new { error = "StartDate is required" });

                var activity = await _activityService.AddActivityAsync(userId, request);

                await _missionService.UpdateMissionProgressAsync(userId, "activity", 1);

                return Ok(activity);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 👣 Добавить/обновить количество шагов за день
        /// </summary>
        /// <param name="request">Данные о шагах</param>
        /// <returns>Информация о шагах за день</returns>
        /// <response code="200">Шаги успешно добавлены/обновлены</response>
        /// <response code="400">Неверные данные</response>
        /// <response code="401">Требуется авторизация</response>
        /// <remarks>
        /// Если запись за указанную дату уже существует, она будет обновлена.
        /// Это позволяет часто обновлять данные о шагах без создания множественных записей.
        /// </remarks>
        /// <example>
        /// {
        ///   "steps": 10000,
        ///   "calories": 500,
        ///   "date": "2025-06-26T00:00:00Z"
        /// }
        /// </example>
        [HttpPost("steps")]
        [ProducesResponseType(typeof(StepsDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> AddSteps([FromBody] AddStepsRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var steps = await _activityService.AddStepsAsync(userId, request);
                return Ok(steps);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 📊 Получить статистику активностей с общим количеством калорий
        /// </summary>
        /// <param name="startDate">Дата начала периода (опционально)</param>
        /// <param name="endDate">Дата окончания периода (опционально)</param>
        /// <returns>Статистика активностей включая общие калории от тренировок и шагов</returns>
        /// <response code="200">Статистика успешно получена</response>
        /// <response code="401">Требуется авторизация</response>
        /// <remarks>
        /// Возвращается общее количество калорий и отдельно калории от каждого источника.
        /// </remarks>
        [HttpGet("stats")]
        [ProducesResponseType(200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetActivityStats([FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var stats = await _activityService.GetActivityStatsAsync(userId, startDate, endDate);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 🔍 Получить конкретную активность по ID
        /// </summary>
        /// <param name="activityId">ID активности</param>
        /// <returns>Данные активности</returns>
        [HttpGet("{activityId}")]
        [ProducesResponseType(typeof(ActivityDto), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetActivity(string activityId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var activity = await _activityService.GetActivityByIdAsync(userId, activityId);
                if (activity == null)
                    return NotFound();

                return Ok(activity);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// ✏️ Обновить активность
        /// </summary>
        /// <param name="activityId">ID активности</param>
        /// <param name="request">Обновленные данные</param>
        /// <returns>Обновленная активность</returns>
        [HttpPut("{activityId}")]
        [ProducesResponseType(typeof(ActivityDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> UpdateActivity(string activityId, [FromBody] UpdateActivityRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var updatedActivity = await _activityService.UpdateActivityAsync(userId, activityId, request);
                return Ok(updatedActivity);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 🗑️ Удалить активность
        /// </summary>
        /// <param name="activityId">ID активности</param>
        /// <returns>Результат удаления</returns>
        [HttpDelete("{activityId}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> DeleteActivity(string activityId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                await _activityService.DeleteActivityAsync(userId, activityId);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 📈 Получить шаги за определенную дату или все шаги
        /// </summary>
        /// <param name="date">Дата для получения шагов (опционально, по умолчанию - все записи)</param>
        /// <returns>Список записей о шагах (одна запись на день)</returns>
        /// <remarks>
        /// Если указана дата - возвращает шаги за этот день.
        /// Если дата не указана - возвращает все дневные записи.
        /// </remarks>
        [HttpGet("steps")]
        [ProducesResponseType(typeof(IEnumerable<StepsDto>), 200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetSteps([FromQuery] DateTime? date = null)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var steps = await _activityService.GetUserStepsAsync(userId, date);
                return Ok(steps);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}