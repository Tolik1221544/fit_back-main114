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
        /// <returns>Список всех скинов с информацией о владении и experience boost</returns>
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
        ///     "isOwned": false,
        ///     "experienceBoost": 1.1,
        ///     "tier": 1,
        ///     "isActive": false
        ///   },
        ///   {
        ///     "id": "skin_machine",
        ///     "name": "Машина",
        ///     "cost": 500,
        ///     "imageUrl": "https://example.com/skins/machine.png", 
        ///     "description": "Скин для тех, кто работает как машина",
        ///     "isOwned": false,
        ///     "experienceBoost": 1.5,
        ///     "tier": 2,
        ///     "isActive": false
        ///   },
        ///   {
        ///     "id": "skin_superhuman",
        ///     "name": "Сверхчеловек",
        ///     "cost": 2000,
        ///     "imageUrl": "https://example.com/skins/superhuman.png",
        ///     "description": "Скин для сверхлюдей", 
        ///     "isOwned": false,
        ///     "experienceBoost": 2.0,
        ///     "tier": 3,
        ///     "isActive": false
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

        /// <summary>
        /// ⚡ Активировать скин для получения experience boost
        /// </summary>
        /// <param name="request">Данные для активации скина</param>
        /// <returns>Результат активации</returns>
        /// <response code="200">Скин успешно активирован</response>
        /// <response code="400">Скин не найден или не принадлежит пользователю</response>
        /// <response code="401">Требуется авторизация</response>
        /// <remarks>
        /// ✅ НОВОЕ: Активация скина дает буст к получаемому опыту:
        /// - Tier 1 скины: 1.1x буст (10% больше опыта)
        /// - Tier 2 скины: 1.5x буст (50% больше опыта)  
        /// - Tier 3 скины: 2.0x буст (100% больше опыта)
        /// Одновременно может быть активен только один скин.
        /// </remarks>
        /// <example>
        /// {
        ///   "skinId": "skin_athlete"
        /// }
        /// </example>
        [HttpPost("activate")]
        public async Task<IActionResult> ActivateSkin([FromBody] ActivateSkinRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _skinService.ActivateSkinAsync(userId, request);

                if (result)
                {
                    var activeSkin = await _skinService.GetActiveUserSkinAsync(userId);
                    return Ok(new
                    {
                        success = true,
                        message = $"Скин активирован! Буст опыта: {activeSkin?.ExperienceBoost}x",
                        experienceBoost = activeSkin?.ExperienceBoost ?? 1.0m
                    });
                }
                else
                {
                    return BadRequest(new { error = "Не удалось активировать скин. Убедитесь, что он вам принадлежит." });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 🌟 Получить активный скин пользователя
        /// </summary>
        /// <returns>Активный скин с информацией о буст опыта</returns>
        /// <response code="200">Активный скин получен</response>
        /// <response code="404">У пользователя нет активного скина</response>
        /// <response code="401">Требуется авторизация</response>
        [HttpGet("active")]
        public async Task<IActionResult> GetActiveSkin()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var activeSkin = await _skinService.GetActiveUserSkinAsync(userId);

                if (activeSkin == null)
                {
                    return NotFound(new { message = "У вас нет активного скина" });
                }

                return Ok(activeSkin);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 📈 Получить текущий буст опыта
        /// </summary>
        /// <returns>Текущий множитель опыта от активного скина</returns>
        /// <response code="200">Буст опыта получен</response>
        /// <response code="401">Требуется авторизация</response>
        [HttpGet("experience-boost")]
        public async Task<IActionResult> GetExperienceBoost()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var boost = await _skinService.GetUserExperienceBoostAsync(userId);
                return Ok(new { experienceBoost = boost });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}