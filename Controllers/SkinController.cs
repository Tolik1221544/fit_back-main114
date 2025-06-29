using FitnessTracker.API.DTOs;
using FitnessTracker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FitnessTracker.API.Controllers
{
    /// <summary>
    /// 🎨 Управление скинами
    /// </summary>
    [ApiController]
    [Route("api/skin")]
    [Authorize]
    public class SkinController : ControllerBase
    {
        private readonly ISkinService _skinService;

        public SkinController(ISkinService skinService)
        {
            _skinService = skinService;
        }

        /// <summary>
        /// 📋 Получить все доступные скины
        /// </summary>
        /// <returns>Список всех скинов с информацией о владении</returns>
        /// <response code="200">Список скинов успешно получен</response>
        /// <response code="401">Требуется авторизация</response>
        /// <example>
        /// Возвращает:
        /// [
        ///   {
        ///     "id": "skin_athlete",
        ///     "name": "Атлет", 
        ///     "cost": 200,
        ///     "imageUrl": "https://example.com/skins/athlete.png",
        ///     "description": "Скин для настоящих спортсменов",
        ///     "isOwned": false
        ///   },
        ///   {
        ///     "id": "skin_machine",
        ///     "name": "Машина",
        ///     "cost": 500,
        ///     "imageUrl": "https://example.com/skins/machine.png", 
        ///     "description": "Скин для тех, кто работает как машина",
        ///     "isOwned": false
        ///   },
        ///   {
        ///     "id": "skin_superhuman",
        ///     "name": "Сверхчеловек",
        ///     "cost": 2000,
        ///     "imageUrl": "https://example.com/skins/superhuman.png",
        ///     "description": "Скин для сверхлюдей", 
        ///     "isOwned": false
        ///   }
        /// ]
        /// </example>
        [HttpGet]
        public async Task<IActionResult> GetAllSkins()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var skins = await _skinService.GetAllSkinsAsync(userId);
                return Ok(skins);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 👤 Получить скины пользователя
        /// </summary>
        /// <returns>Список скинов, которыми владеет пользователь</returns>
        /// <response code="200">Список скинов пользователя успешно получен</response>
        /// <response code="401">Требуется авторизация</response>
        [HttpGet("owned")]
        public async Task<IActionResult> GetUserSkins()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var skins = await _skinService.GetUserSkinsAsync(userId);
                return Ok(skins);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 💰 Купить скин за LW Coins
        /// </summary>
        /// <param name="request">Данные для покупки скина</param>
        /// <returns>Результат покупки</returns>
        /// <response code="200">Скин успешно куплен</response>
        /// <response code="400">Недостаточно LW Coins или скин уже куплен</response>
        /// <response code="401">Требуется авторизация</response>
        /// <example>
        /// {
        ///   "skinId": "skin_athlete"
        /// }
        /// </example>
        [HttpPost("purchase")]
        public async Task<IActionResult> PurchaseSkin([FromBody] PurchaseSkinRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _skinService.PurchaseSkinAsync(userId, request);

                if (result)
                {
                    return Ok(new { success = true, message = "Скин успешно куплен!" });
                }
                else
                {
                    return BadRequest(new { error = "Не удалось купить скин. Проверьте баланс LW Coins." });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}