uusing FitnessTracker.API.DTOs;
using FitnessTracker.API.Models;
using FitnessTracker.API.Repositories;

namespace FitnessTracker.API.Services
{
    public class ReferralService : IReferralService
    {
        private readonly IReferralRepository _referralRepository;
        private readonly ILwCoinService _lwCoinService;

        public ReferralService(IReferralRepository referralRepository, ILwCoinService lwCoinService)
        {
            _referralRepository = referralRepository;
            _lwCoinService = lwCoinService;
        }

        public async Task<bool> SetReferralAsync(string userId, SetReferralRequest request)
        {
            var referral = await _referralRepository.GetByCodeAsync(request.ReferralCode);
            if (referral == null || referral.ReferrerId == userId)
                return false;

            // Проверяем, не использовал ли уже этот пользователь реферальный код
            var existingReferrals = await _referralRepository.GetUserReferralsAsync(referral.ReferrerId);
            if (existingReferrals.Any(r => r.ReferredUserId == userId))
                return false;

            // Награждаем реферера 150 LW Coins (полмесяца подписки)
            await _lwCoinService.AddLwCoinsAsync(referral.ReferrerId, 150, "referral", $"Referral bonus for inviting user {userId}");

            // Создаем запись реферала
            var newReferral = new Referral
            {
                ReferrerId = referral.ReferrerId,
                ReferredUserId = userId,
                ReferralCode = request.ReferralCode,
                RewardCoins = 150 // Обновлено на LW Coins
            };

            await _referralRepository.CreateAsync(newReferral);
            return true;
        }

        public async Task<string> GenerateReferralCodeAsync(string userId)
        {
            string code;
            do
            {
                code = GenerateRandomCode();
            } while (await _referralRepository.CodeExistsAsync(code));

            var referral = new Referral
            {
                ReferrerId = userId,
                ReferralCode = code,
                ReferredUserId = string.Empty,
                RewardCoins = 150
            };

            await _referralRepository.CreateAsync(referral);
            return code;
        }

        public async Task<int> GetReferralCountAsync(string userId)
        {
            var referrals = await _referralRepository.GetUserReferralsAsync(userId);
            return referrals.Count(r => !string.IsNullOrEmpty(r.ReferredUserId));
        }

        private string GenerateRandomCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 8)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}