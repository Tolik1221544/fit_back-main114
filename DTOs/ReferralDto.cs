namespace FitnessTracker.API.DTOs
{
    public class ReferralStatsDto
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty; 
        public string ReferralCode { get; set; } = string.Empty;
        public int Level { get; set; }
        public int TotalReferrals { get; set; }
        public int MonthlyReferrals { get; set; }
        public int TotalEarnedCoins { get; set; }
        public int MonthlyEarnedCoins { get; set; }
        public List<ReferredUserDto> FirstLevelReferrals { get; set; } = new List<ReferredUserDto>(); 
        public List<ReferredUserDto> SecondLevelReferrals { get; set; } = new List<ReferredUserDto>(); 
        public ReferralRankDto Rank { get; set; } = new ReferralRankDto();
        public List<ReferralLeaderboardDto> Leaderboard { get; set; } = new List<ReferralLeaderboardDto>();
    }

    public class ReferredUserDto
    {
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty; 
        public int Level { get; set; }
        public DateTime JoinedAt { get; set; }
        public int RewardCoins { get; set; } = 150;
        public bool IsPremium { get; set; }
        public string Status { get; set; } = "Active"; // Active, Inactive
        public int ReferralLevel { get; set; } = 1; 
    }

    public class ReferralRankDto
    {
        public int Position { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Badge { get; set; } = string.Empty;
        public int NextLevelRequirement { get; set; }
        public int Progress { get; set; }
    }

    public class ReferralLeaderboardDto
    {
        public int Position { get; set; }
        public string Email { get; set; } = string.Empty;
        public int Level { get; set; }
        public int TotalReferrals { get; set; }
        public int MonthlyReferrals { get; set; }
        public string Badge { get; set; } = string.Empty;
        public bool IsCurrentUser { get; set; }
    }

    public class GenerateReferralResponse
    {
        public string ReferralCode { get; set; } = string.Empty;
        public string ReferralLink { get; set; } = string.Empty;
        public string QrCodeUrl { get; set; } = string.Empty;
    }

    public class ValidateReferralRequest
    {
        public string ReferralCode { get; set; } = string.Empty;
    }

    public class ValidateReferralResponse
    {
        public bool IsValid { get; set; }
        public string ReferrerEmail { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
   
}