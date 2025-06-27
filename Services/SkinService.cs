using FitnessTracker.API.DTOs;
using FitnessTracker.API.Models;
using FitnessTracker.API.Repositories;
using AutoMapper;

namespace FitnessTracker.API.Services
{
    public class SkinService : ISkinService
    {
        private readonly ISkinRepository _skinRepository;
        private readonly ILwCoinService _lwCoinService; 
        private readonly IMapper _mapper;

        public SkinService(ISkinRepository skinRepository, ILwCoinService lwCoinService, IMapper mapper)
        {
            _skinRepository = skinRepository;
            _lwCoinService = lwCoinService; 
            _mapper = mapper;
        }

        public async Task<IEnumerable<SkinDto>> GetAllSkinsAsync(string userId)
        {
            var skins = await _skinRepository.GetAllSkinsAsync();
            var userSkins = await _skinRepository.GetUserSkinsAsync(userId);
            var ownedSkinIds = userSkins.Select(us => us.SkinId).ToHashSet();

            var skinDtos = _mapper.Map<IEnumerable<SkinDto>>(skins);
            foreach (var skinDto in skinDtos)
            {
                skinDto.IsOwned = ownedSkinIds.Contains(skinDto.Id);
            }

            return skinDtos;
        }

        public async Task<bool> PurchaseSkinAsync(string userId, PurchaseSkinRequest request)
        {
            var skin = await _skinRepository.GetSkinByIdAsync(request.SkinId);
            if (skin == null)
                return false;

            if (await _skinRepository.UserOwnsSkinAsync(userId, request.SkinId))
                return false;

            if (!await _lwCoinService.SpendLwCoinsAsync(userId, skin.Cost, "purchase",
                $"Purchased skin: {skin.Name}", "skin"))
                return false;

            var userSkin = new UserSkin
            {
                UserId = userId,
                SkinId = request.SkinId
            };

            await _skinRepository.PurchaseSkinAsync(userSkin);
            return true;
        }

        public async Task<IEnumerable<SkinDto>> GetUserSkinsAsync(string userId)
        {
            var userSkins = await _skinRepository.GetUserSkinsAsync(userId);
            var skinDtos = userSkins.Select(us => _mapper.Map<SkinDto>(us.Skin));

            foreach (var skinDto in skinDtos)
            {
                skinDto.IsOwned = true;
            }

            return skinDtos;
        }
    }
}