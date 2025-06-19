namespace FitnessTracker.API.Models
{
    public class Referral
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ReferrerId { get; set; } = string.Empty;
        public string ReferredUserId { get; set; } = string.Empty;
        public string ReferralCode { get; set; } = string.Empty;
        public int RewardCoins { get; set; } = 50;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public User Referrer { get; set; } = null!;
    }
}
