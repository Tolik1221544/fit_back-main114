namespace FitnessTracker.API.DTOs
{
    public class UserDto
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string RegisteredVia { get; set; } = string.Empty;
        public int Level { get; set; }
        public int Coins { get; set; }
        public int Age { get; set; }
        public string Gender { get; set; } = string.Empty;
        public decimal Weight { get; set; }
        public decimal Height { get; set; }
        public DateTime JoinedAt { get; set; }
    }

    public class UpdateUserProfileRequest
    {
        public int Age { get; set; }
        public string Gender { get; set; } = string.Empty;
        public decimal Weight { get; set; }
        public decimal Height { get; set; }
    }
}
