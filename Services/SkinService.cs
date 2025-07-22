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
        private readonly IMapper _mapper;
        private readonly ILogger<SkinService> _logger;

        public SkinService(
            ISkinRepository skinRepository,
            IUserRepository userRepository,     
            ILwCoinRepository lwCoinRepository, 
            IMapper mapper,
            ILogger<SkinService> logger)
        {
            _skinRepository = skinRepository;
            _userRepository = userRepository;        
            _lwCoinRepository = lwCoinRepository;    
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<IEnumerable<SkinDto>> GetAllSkinsAsync(string userId)
        {
            var skins = await _skinRepository.GetAllSkinsAsync();
            var userSkins = await _skinRepository.GetUserSkinsAsync(userId);
            var ownedSkinIds = userSkins.Select(us => us.SkinId).ToHashSet();
            var activeSkinId = userSkins.FirstOrDefault(us => us.IsActive)?.SkinId;

            var skinDtos = _mapper.Map<IEnumerable<SkinDto>>(skins);
            foreach (var skinDto in skinDtos)
            {
                skinDto.IsOwned = ownedSkinIds.Contains(skinDto.Id);
                skinDto.IsActive = skinDto.Id == activeSkinId;
            }

            return skinDtos
                .OrderBy(s => s.Tier)
                .ThenBy(s => s.Cost)
                .ThenBy(s => s.Name);
        }

        public async Task<bool> PurchaseSkinAsync(string userId, PurchaseSkinRequest request)
        {
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

            try
            {
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

                user.FractionalLwCoins = (double)currentBalance;
                user.LwCoins = (int)Math.Floor(currentBalance);
                await _userRepository.UpdateAsync(user);

                return false;
            }
        }

        public async Task<IEnumerable<SkinDto>> GetUserSkinsAsync(string userId)
        {
            var userSkins = await _skinRepository.GetUserSkinsAsync(userId);
            var skinDtos = userSkins.Select(us =>
            {
                var dto = _mapper.Map<SkinDto>(us.Skin);
                dto.IsOwned = true;
                dto.IsActive = us.IsActive;
                return dto;
            });

            return skinDtos
                .OrderBy(s => s.Tier)
                .ThenBy(s => s.Cost)
                .ThenBy(s => s.Name);
        }

        public async Task<bool> ActivateSkinAsync(string userId, ActivateSkinRequest request)
        {
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

        public async Task<SkinDto?> GetActiveUserSkinAsync(string userId)
        {
            var userSkins = await _skinRepository.GetUserSkinsAsync(userId);
            var activeSkin = userSkins.FirstOrDefault(us => us.IsActive);

            if (activeSkin == null) return null;

            var dto = _mapper.Map<SkinDto>(activeSkin.Skin);
            dto.IsOwned = true;
            dto.IsActive = true;
            return dto;
        }

        public async Task<decimal> GetUserExperienceBoostAsync(string userId)
        {
            var activeSkin = await GetActiveUserSkinAsync(userId);
            return activeSkin?.ExperienceBoost ?? 1.0m;
        }
    }
}