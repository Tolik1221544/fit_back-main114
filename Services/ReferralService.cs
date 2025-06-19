using FitnessTracker.API.DTOs;
using FitnessTracker.API.Models;
using FitnessTracker.API.Repositories;

namespace FitnessTracker.API.Services
{
    public class ReferralService : IReferralService
    {
        private readonly IReferralRepository _referralRepository;
        private readonly ICoinService _coinService;

        public ReferralService(IReferralRepository referralRepository, ICoinService coinService)
        {
            _referralRepository = referralRepository;
            _coinService = coinService;
        }

        public async Task<bool> SetReferralAsync(string userId, SetReferralRequest request)
        {
            var referral = await _referralRepository.GetByCodeAsync(request.ReferralCode);
            if (referral == null || referral.ReferrerId == userId)
                return false;

            // Award coins to the referrer
            await _coinService.EarnCoinsAsync(referral.ReferrerId, referral.RewardCoins, "Referral bonus");

            // Create new referral record
            var newReferral = new Referral
            {
                ReferrerId = referral.ReferrerId,
                ReferredUserId = userId,
                ReferralCode = request.ReferralCode
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
                ReferredUserId = string.Empty // This will be set when someone uses the code
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
