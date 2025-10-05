namespace FitnessTracker.API.Models
{
    public class User
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string RegisteredVia { get; set; } = string.Empty;
        public string? AppleUserId { get; set; }
        public long? TelegramId { get; set; }
        public int Level { get; set; } = 1;
        public int Experience { get; set; } = 0;
        public int LwCoins { get; set; } = 300;
        public double FractionalLwCoins { get; set; } = 300.0;
        public int Age { get; set; }
        public string Gender { get; set; } = string.Empty;
        public decimal Weight { get; set; }
        public decimal Height { get; set; }
        public bool IsEmailConfirmed { get; set; } = true;
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastMonthlyRefill { get; set; } = DateTime.UtcNow;
        public string? ReferralCode { get; set; }
        public string? ReferredByUserId { get; set; }
        public int TotalReferrals { get; set; } = 0;
        public int TotalReferralRewards { get; set; } = 0;

        public string Locale { get; set; } = "ru_RU"; 

        // Navigation properties
        public ICollection<FoodIntake> FoodIntakes { get; set; } = new List<FoodIntake>();
        public ICollection<Activity> Activities { get; set; } = new List<Activity>();
        public ICollection<LwCoinTransaction> LwCoinTransactions { get; set; } = new List<LwCoinTransaction>();
        public ICollection<UserSkin> UserSkins { get; set; } = new List<UserSkin>();
        public ICollection<UserMission> UserMissions { get; set; } = new List<UserMission>();
        public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
        public ICollection<Referral> Referrals { get; set; } = new List<Referral>();
    }
}