namespace FitnessTracker.API.Models
{
    public class User
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string RegisteredVia { get; set; } = "email";
        public int Level { get; set; } = 1;
        public int Coins { get; set; } = 100; 

        // LW Coin система
        public int LwCoins { get; set; } = 150; 
        public DateTime LastMonthlyRefill { get; set; } = DateTime.UtcNow;
        public bool HasPremiumSubscription { get; set; } = false;
        public DateTime? PremiumExpiresAt { get; set; }
        public int MonthlyLwCoinsUsed { get; set; } = 0;
        public DateTime CurrentMonthStart { get; set; } = DateTime.UtcNow;

        public int Age { get; set; }
        public string Gender { get; set; } = string.Empty;
        public decimal Weight { get; set; }
        public decimal Height { get; set; }
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public ICollection<FoodIntake> FoodIntakes { get; set; } = new List<FoodIntake>();
        public ICollection<Activity> Activities { get; set; } = new List<Activity>();
        public ICollection<UserSkin> UserSkins { get; set; } = new List<UserSkin>();
        public ICollection<Referral> Referrals { get; set; } = new List<Referral>();
        public ICollection<UserMission> UserMissions { get; set; } = new List<UserMission>();
        public ICollection<CoinTransaction> CoinTransactions { get; set; } = new List<CoinTransaction>();
        public ICollection<LwCoinTransaction> LwCoinTransactions { get; set; } = new List<LwCoinTransaction>();
        public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    }
}