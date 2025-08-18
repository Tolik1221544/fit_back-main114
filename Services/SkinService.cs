using FitnessTracker.API.DTOs;
using FitnessTracker.API.Models;
using FitnessTracker.API.Repositories;
using AutoMapper;

namespace FitnessTracker.API.Services
{
    public class SkinService : ISkinService
    {
        private readonly ISkinRepository _skinRepository;
        private readonly IUserRepository _userRepository;
        private readonly ILwCoinRepository _lwCoinRepository;
        private readonly ILocalizationService _localizationService;
        private readonly IMapper _mapper;
        private readonly ILogger<SkinService> _logger;

        public SkinService(
            ISkinRepository skinRepository,
            IUserRepository userRepository,
            ILwCoinRepository lwCoinRepository,
            ILocalizationService localizationService,
            IMapper mapper,
            ILogger<SkinService> logger)
        {
            _skinRepository = skinRepository ?? throw new ArgumentNullException(nameof(skinRepository));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _lwCoinRepository = lwCoinRepository ?? throw new ArgumentNullException(nameof(lwCoinRepository));
            _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IEnumerable<SkinDto>> GetAllSkinsAsync(string userId)
        {
            try
            {
                _logger.LogInformation($"Getting all skins for user {userId}");

                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogError("UserId is null or empty");
                    return new List<SkinDto>();
                }

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogError($"User {userId} not found");
                    return new List<SkinDto>();
                }

                var userLocale = await _localizationService.GetUserLocaleAsync(userId);
                _logger.LogInformation($"User locale: {userLocale}");

                var skins = await _skinRepository.GetAllSkinsAsync();
                if (skins == null)
                {
                    _logger.LogWarning("No skins found in repository");
                    return new List<SkinDto>();
                }

                var userSkins = await _skinRepository.GetUserSkinsAsync(userId);
                var ownedSkinIds = userSkins?.Select(us => us.SkinId).ToHashSet() ?? new HashSet<string>();
                var activeSkinId = userSkins?.FirstOrDefault(us => us.IsActive)?.SkinId;

                var skinDtos = _mapper.Map<IEnumerable<SkinDto>>(skins);
                if (skinDtos == null)
                {
                    _logger.LogError("Failed to map skins to DTOs");
                    return new List<SkinDto>();
                }

                foreach (var skinDto in skinDtos)
                {
                    if (skinDto == null) continue;

                    try
                    {
                        var skinKey = skinDto.Id?.Replace("skin_", "") ?? "";
                        skinDto.Name = _localizationService.Translate($"skin.{skinKey}", userLocale);
                        skinDto.Description = _localizationService.Translate($"skin.{skinKey}.desc", userLocale);

                        skinDto.IsOwned = ownedSkinIds.Contains(skinDto.Id ?? "");
                        skinDto.IsActive = skinDto.Id == activeSkinId;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error processing skin {skinDto.Id}: {ex.Message}");
                        skinDto.Name = skinDto.Name ?? "Unknown Skin";
                        skinDto.Description = skinDto.Description ?? "No description";
                        skinDto.IsOwned = false;
                        skinDto.IsActive = false;
                    }
                }

                var result = skinDtos
                    .Where(s => s != null)
                    .OrderBy(s => s.Tier)
                    .ThenBy(s => s.Cost)
                    .ThenBy(s => s.Name);

                _logger.LogInformation($"Successfully retrieved {result.Count()} skins for user {userId}");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting all skins for user {userId}: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return new List<SkinDto>();
            }
        }

        public async Task<bool> PurchaseSkinAsync(string userId, PurchaseSkinRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(userId) || request?.SkinId == null)
                {
                    _logger.LogWarning("Invalid parameters for skin purchase");
                    return false;
                }

                var skin = await _skinRepository.GetSkinByIdAsync(request.SkinId);
                if (skin == null)
                {
                    _logger.LogWarning($"❌ Skin {request.SkinId} not found");
                    return false;
                }

                if (await _skinRepository.UserOwnsSkinAsync(userId, request.SkinId))
                {
                    _logger.LogWarning($"❌ User {userId} already owns skin {request.SkinId}");
                    return false;
                }

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogError($"❌ User {userId} not found");
                    return false;
                }

                decimal currentBalance = user.FractionalLwCoins > 0 ? (decimal)user.FractionalLwCoins : user.LwCoins;
                decimal skinCost = skin.Cost;

                if (currentBalance < skinCost)
                {
                    _logger.LogWarning($"❌ Insufficient coins: user={userId}, balance={currentBalance}, required={skinCost}");
                    return false;
                }

                var newBalance = currentBalance - skinCost;
                user.FractionalLwCoins = (double)newBalance;
                user.LwCoins = (int)Math.Floor(newBalance);

                await _userRepository.UpdateAsync(user);

                var transaction = new LwCoinTransaction
                {
                    UserId = userId,
                    Amount = -(int)Math.Ceiling(skinCost),
                    FractionalAmount = (double)skinCost,
                    Type = "spent",
                    Description = $"Purchased skin: {skin.Name}",
                    FeatureUsed = "skin_purchase",
                    UsageDate = DateTime.UtcNow.Date.ToString("yyyy-MM-dd")
                };

                await _lwCoinRepository.CreateTransactionAsync(transaction);

                var userSkin = new UserSkin
                {
                    UserId = userId,
                    SkinId = request.SkinId,
                    IsActive = false
                };

                await _skinRepository.PurchaseSkinAsync(userSkin);

                _logger.LogInformation($"✅ User {userId} purchased skin '{skin.Name}' (Tier {skin.Tier}, {skin.ExperienceBoost}x XP) for {skinCost} coins. Balance: {currentBalance} → {newBalance}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error purchasing skin: {ex.Message}");
                return false;
            }
        }

        public async Task<IEnumerable<SkinDto>> GetUserSkinsAsync(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    return new List<SkinDto>();
                }

                var userSkins = await _skinRepository.GetUserSkinsAsync(userId);
                if (userSkins == null)
                {
                    return new List<SkinDto>();
                }

                var skinDtos = userSkins
                    .Where(us => us?.Skin != null)
                    .Select(us =>
                    {
                        try
                        {
                            var dto = _mapper.Map<SkinDto>(us.Skin);
                            if (dto != null)
                            {
                                dto.IsOwned = true;
                                dto.IsActive = us.IsActive;
                            }
                            return dto;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Error mapping user skin: {ex.Message}");
                            return null;
                        }
                    })
                    .Where(dto => dto != null)
                    .Cast<SkinDto>();

                return skinDtos
                    .OrderBy(s => s.Tier)
                    .ThenBy(s => s.Cost)
                    .ThenBy(s => s.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting user skins for {userId}: {ex.Message}");
                return new List<SkinDto>();
            }
        }

        public async Task<bool> ActivateSkinAsync(string userId, ActivateSkinRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(userId) || request?.SkinId == null)
                {
                    return false;
                }

                if (!await _skinRepository.UserOwnsSkinAsync(userId, request.SkinId))
                {
                    _logger.LogWarning($"❌ User {userId} doesn't own skin {request.SkinId}");
                    return false;
                }

                await _skinRepository.DeactivateAllUserSkinsAsync(userId);
                var success = await _skinRepository.ActivateUserSkinAsync(userId, request.SkinId);

                if (success)
                {
                    var skin = await _skinRepository.GetSkinByIdAsync(request.SkinId);
                    _logger.LogInformation($"✅ User {userId} activated skin '{skin?.Name}' with {skin?.ExperienceBoost}x XP boost");
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error activating skin: {ex.Message}");
                return false;
            }
        }

        public async Task<SkinDto?> GetActiveUserSkinAsync(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    return null;
                }

                var userSkins = await _skinRepository.GetUserSkinsAsync(userId);
                var activeSkin = userSkins?.FirstOrDefault(us => us.IsActive);

                if (activeSkin?.Skin == null) return null;

                var dto = _mapper.Map<SkinDto>(activeSkin.Skin);
                if (dto != null)
                {
                    dto.IsOwned = true;
                    dto.IsActive = true;
                }
                return dto;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting active skin for {userId}: {ex.Message}");
                return null;
            }
        }

        public async Task<decimal> GetUserExperienceBoostAsync(string userId)
        {
            try
            {
                var activeSkin = await GetActiveUserSkinAsync(userId);
                return activeSkin?.ExperienceBoost ?? 1.0m;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting experience boost for {userId}: {ex.Message}");
                return 1.0m;
            }
        }
    }
}