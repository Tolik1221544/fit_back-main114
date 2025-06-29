using FitnessTracker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FitnessTracker.API.Controllers
{
    /// <summary>
    /// 🎯 Управление миссиями и достижениями
    /// </summary>
    [ApiController]
    [Route("api/mission")]
    [Authorize]
    public class MissionController : ControllerBase
    {
        private readonly IMissionService _missionService;

        public MissionController(IMissionService missionService)
        {
            _missionService = missionService;
        }

        /// <summary>
        /// 📋 Получить список активных миссий пользователя
        /// </summary>
        /// <returns>Список миссий с прогрессом выполнения</returns>
        /// <response code="200">Список миссий успешно получен</response>
        /// <response code="401">Требуется авторизация</response>
        [HttpGet]
        public async Task<IActionResult> GetMissions()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var missions = await _missionService.GetUserMissionsAsync(userId);
                return Ok(missions);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 🏆 Получить список достижений пользователя
        /// </summary>
        /// <returns>Список достижений с прогрессом разблокировки</returns>
        /// <response code="200">Список достижений успешно получен</response>
        /// <response code="401">Требуется авторизация</response>
        [HttpGet("achievements")]
        public async Task<IActionResult> GetAchievements()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var achievements = await _missionService.GetUserAchievementsAsync(userId);
                return Ok(achievements);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 🎯 Обновить прогресс миссии вручную (для тестирования)
        /// </summary>
        /// <param name="missionType">Тип миссии для обновления</param>
        /// <param name="increment">Количество для увеличения прогресса</param>
        /// <returns>Результат обновления</returns>
        /// <response code="200">Прогресс успешно обновлен</response>
        /// <response code="401">Требуется авторизация</response>
        [HttpPost("update-progress")]
        public async Task<IActionResult> UpdateMissionProgress([FromQuery] string missionType, [FromQuery] int increment = 1)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                await _missionService.UpdateMissionProgressAsync(userId, missionType, increment);
                return Ok(new { success = true, message = $"Progress updated for {missionType}" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}