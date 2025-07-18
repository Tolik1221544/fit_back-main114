﻿using FitnessTracker.API.DTOs;
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
        private readonly ILogger<SkinService> _logger;

        public SkinService(ISkinRepository skinRepository, ILwCoinService lwCoinService, IMapper mapper, ILogger<SkinService> logger)
        {
            _skinRepository = skinRepository;
            _lwCoinService = lwCoinService;
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

            // ✅ ИСПРАВЛЕНО: Правильный порядок сортировки
            return skinDtos
                .OrderBy(s => s.Tier)           // Сначала по уровню (1, 2, 3)
                .ThenBy(s => s.Cost)            // Затем по цене (100, 200, 400...)
                .ThenBy(s => s.Name);           // И по имени для стабильности
        }

        public async Task<bool> PurchaseSkinAsync(string userId, PurchaseSkinRequest request)
        {
            var skin = await _skinRepository.GetSkinByIdAsync(request.SkinId);
            if (skin == null)
            {
                _logger.LogWarning($"Skin {request.SkinId} not found");
                return false;
            }

            if (await _skinRepository.UserOwnsSkinAsync(userId, request.SkinId))
            {
                _logger.LogWarning($"User {userId} already owns skin {request.SkinId}");
                return false;
            }

            // ✅ ИСПРАВЛЕНО: Используем правильную стоимость и систему трат
            if (!await _lwCoinService.SpendLwCoinsAsync(userId, skin.Cost, "skin_purchase",
                $"Purchased skin: {skin.Name}", "skin"))
            {
                _logger.LogWarning($"User {userId} doesn't have enough LW Coins for skin {request.SkinId}. Required: {skin.Cost}");
                return false;
            }

            var userSkin = new UserSkin
            {
                UserId = userId,
                SkinId = request.SkinId,
                IsActive = false // По умолчанию не активен
            };

            await _skinRepository.PurchaseSkinAsync(userSkin);

            _logger.LogInformation($"✅ User {userId} purchased skin '{skin.Name}' (Tier {skin.Tier}, {skin.ExperienceBoost}x XP boost) for {skin.Cost} coins");
            return true;
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
            // Проверяем, что пользователь владеет скином
            if (!await _skinRepository.UserOwnsSkinAsync(userId, request.SkinId))
            {
                _logger.LogWarning($"User {userId} doesn't own skin {request.SkinId}");
                return false;
            }

            // Деактивируем все скины пользователя
            await _skinRepository.DeactivateAllUserSkinsAsync(userId);

            // Активируем выбранный скин
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