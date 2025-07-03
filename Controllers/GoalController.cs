using FitnessTracker.API.DTOs;
using FitnessTracker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FitnessTracker.API.Controllers
{
    /// <summary>
    /// 🎯 Управление целями и ежедневным прогрессом
    /// </summary>
    [ApiController]
    [Route("api/goals")]
    [Authorize]
    [Produces("application/json")]
    public class GoalController : ControllerBase
    {
        private readonly IGoalService _goalService;
        private readonly ILogger<GoalController> _logger;

        public GoalController(IGoalService goalService, ILogger<GoalController> logger)
        {
            _goalService = goalService;
            _logger = logger;
        }

        /// <summary>
        /// 📋 Получить все цели пользователя
        /// </summary>
        /// <returns>Список целей с прогрессом</returns>
        /// <response code="200">Список целей успешно получен</response>
        /// <response code="401">Требуется авторизация</response>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<GoalDto>), 200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetUserGoals()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var goals = await _goalService.GetUserGoalsAsync(userId);
                return Ok(goals);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting user goals: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 🎯 Получить активную цель пользователя
        /// </summary>
        /// <returns>Активная цель с прогрессом за сегодня</returns>
        /// <response code="200">Активная цель получена</response>
        /// <response code="404">Активная цель не найдена</response>
        /// <response code="401">Требуется авторизация</response>
        [HttpGet("active")]
        [ProducesResponseType(typeof(GoalDto), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetActiveGoal()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var goal = await _goalService.GetActiveUserGoalAsync(userId);
                if (goal == null)
                    return NotFound(new { message = "Активная цель не найдена" });

                return Ok(goal);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting active goal: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// ➕ Создать новую цель
        /// </summary>
        /// <param name="request">Данные новой цели</param>
        /// <returns>Созданная цель</returns>
        /// <response code="200">Цель успешно создана</response>
        /// <response code="400">Неверные данные запроса</response>
        /// <response code="401">Требуется авторизация</response>
        /// <remarks>
        /// Типы целей:
        /// - weight_loss: Похудение
        /// - weight_maintain: Поддержание веса  
        /// - muscle_gain: Набор мышечной массы
        /// 
        /// При создании новой цели, предыдущие активные цели автоматически деактивируются.
        /// </remarks>
        /// <example>
        /// {
        ///   "goalType": "weight_loss",
        ///   "title": "Похудеть к лету",
        ///   "targetWeight": 70.0,
        ///   "currentWeight": 80.0,
        ///   "targetCalories": 1800,
        ///   "targetStepsPerDay": 10000,
        ///   "targetWorkoutsPerWeek": 4,
        ///   "endDate": "2025-08-01T00:00:00Z"
        /// }
        /// </example>
        [HttpPost]
        [ProducesResponseType(typeof(GoalDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> CreateGoal([FromBody] CreateGoalRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                if (string.IsNullOrEmpty(request.GoalType))
                    return BadRequest(new { error = "GoalType is required" });

                var validGoalTypes = new[] { "weight_loss", "weight_maintain", "muscle_gain" };
                if (!validGoalTypes.Contains(request.GoalType))
                    return BadRequest(new { error = "Invalid goal type. Must be one of: weight_loss, weight_maintain, muscle_gain" });

                var goal = await _goalService.CreateGoalAsync(userId, request);
                return Ok(goal);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating goal: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 🔍 Получить конкретную цель по ID
        /// </summary>
        /// <param name="goalId">ID цели</param>
        /// <returns>Данные цели</returns>
        [HttpGet("{goalId}")]
        [ProducesResponseType(typeof(GoalDto), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetGoal(string goalId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var goal = await _goalService.GetGoalByIdAsync(userId, goalId);
                if (goal == null)
                    return NotFound();

                return Ok(goal);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting goal {goalId}: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// ✏️ Обновить цель
        /// </summary>
        /// <param name="goalId">ID цели</param>
        /// <param name="request">Обновленные данные</param>
        /// <returns>Обновленная цель</returns>
        [HttpPut("{goalId}")]
        [ProducesResponseType(typeof(GoalDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> UpdateGoal(string goalId, [FromBody] UpdateGoalRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var updatedGoal = await _goalService.UpdateGoalAsync(userId, goalId, request);
                return Ok(updatedGoal);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating goal {goalId}: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 🗑️ Удалить цель
        /// </summary>
        /// <param name="goalId">ID цели</param>
        /// <returns>Результат удаления</returns>
        [HttpDelete("{goalId}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> DeleteGoal(string goalId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                await _goalService.DeleteGoalAsync(userId, goalId);
                return Ok(new { success = true });
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting goal {goalId}: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 📊 Получить прогресс за сегодня
        /// </summary>
        /// <returns>Прогресс по активной цели за сегодня</returns>
        /// <response code="200">Прогресс получен</response>
        /// <response code="404">Активная цель не найдена</response>
        /// <response code="401">Требуется авторизация</response>
        [HttpGet("progress/today")]
        [ProducesResponseType(typeof(DailyGoalProgressDto), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetTodayProgress()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var progress = await _goalService.GetTodayProgressAsync(userId);
                if (progress == null)
                    return NotFound(new { message = "Активная цель не найдена" });

                return Ok(progress);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting today progress: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 📈 Получить историю прогресса по цели
        /// </summary>
        /// <param name="goalId">ID цели</param>
        /// <param name="startDate">Дата начала периода (опционально)</param>
        /// <param name="endDate">Дата окончания периода (опционально)</param>
        /// <returns>История прогресса</returns>
        [HttpGet("{goalId}/progress")]
        [ProducesResponseType(typeof(IEnumerable<DailyGoalProgressDto>), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetProgressHistory(string goalId, [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var progress = await _goalService.GetProgressHistoryAsync(userId, goalId, startDate, endDate);
                return Ok(progress);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting progress history for goal {goalId}: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 📝 Обновить прогресс за день
        /// </summary>
        /// <param name="request">Данные прогресса</param>
        /// <returns>Обновленный прогресс</returns>
        /// <response code="200">Прогресс обновлен</response>
        /// <response code="400">Ошибка обновления</response>
        /// <response code="401">Требуется авторизация</response>
        /// <remarks>
        /// Система автоматически рассчитывает большинство показателей из активностей и питания.
        /// Вручную можно указать только те показатели, которые нужно переопределить.
        /// </remarks>
        /// <example>
        /// {
        ///   "date": "2025-07-03T00:00:00Z",
        ///   "actualWeight": 79.5,
        ///   "manualCalories": 1850,
        ///   "manualSteps": 12000
        /// }
        /// </example>
        [HttpPost("progress")]
        [ProducesResponseType(typeof(DailyGoalProgressDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> UpdateDailyProgress([FromBody] UpdateDailyProgressRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var progress = await _goalService.UpdateDailyProgressAsync(userId, request);
                return Ok(progress);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating daily progress: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 🔄 Пересчитать прогресс за конкретную дату
        /// </summary>
        /// <param name="date">Дата для пересчета</param>
        /// <returns>Результат пересчета</returns>
        /// <response code="200">Прогресс пересчитан</response>
        /// <response code="401">Требуется авторизация</response>
        [HttpPost("progress/recalculate")]
        [ProducesResponseType(200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> RecalculateProgress([FromQuery] DateTime date)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                await _goalService.RecalculateDailyProgressAsync(userId, date);
                return Ok(new { success = true, message = $"Прогресс за {date:yyyy-MM-dd} пересчитан" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error recalculating progress for {date}: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 📋 Получить шаблоны целей
        /// </summary>
        /// <returns>Список предустановленных шаблонов целей</returns>
        /// <response code="200">Шаблоны получены</response>
        /// <remarks>
        /// Возвращает готовые шаблоны для трех типов целей:
        /// - Похудение (weight_loss)
        /// - Поддержание веса (weight_maintain)
        /// - Набор мышечной массы (muscle_gain)
        /// 
        /// Каждый шаблон содержит рекомендуемые значения калорий, макросов и активности.
        /// </remarks>
        [HttpGet("templates")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(IEnumerable<GoalTemplateDto>), 200)]
        public async Task<IActionResult> GetGoalTemplates()
        {
            try
            {
                var templates = await _goalService.GetGoalTemplatesAsync();
                return Ok(templates);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting goal templates: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 🎯 Получить конкретный шаблон цели
        /// </summary>
        /// <param name="goalType">Тип цели (weight_loss, weight_maintain, muscle_gain)</param>
        /// <returns>Шаблон цели</returns>
        [HttpGet("templates/{goalType}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(GoalTemplateDto), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetGoalTemplate(string goalType)
        {
            try
            {
                var template = await _goalService.GetGoalTemplateAsync(goalType);
                if (template == null)
                    return NotFound();

                return Ok(template);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting goal template {goalType}: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}