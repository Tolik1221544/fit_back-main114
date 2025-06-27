namespace FitnessTracker.API.DTOs
{
    public class SendVerificationCodeRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    public class ConfirmEmailRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }

    public class GoogleAuthRequest
    {
        public string GoogleToken { get; set; } = string.Empty;
    }

    public class LogoutRequest
    {
        public string AccessToken { get; set; } = string.Empty;
    }

    public class AuthResponseDto
    {
        public string AccessToken { get; set; } = string.Empty;
        public UserDto User { get; set; } = new UserDto();
    }

    public class UserDto
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string RegisteredVia { get; set; } = string.Empty;
        public int Level { get; set; }
        public int Experience { get; set; }
        public int MaxExperience { get; set; } 
        public int ExperienceToNextLevel { get; set; } 
        public decimal ExperienceProgress { get; set; } 
        public int LwCoins { get; set; }
        public int Age { get; set; }
        public string Gender { get; set; } = string.Empty;
        public decimal Weight { get; set; }
        public decimal Height { get; set; }
        public bool IsEmailConfirmed { get; set; } = true;
        public DateTime JoinedAt { get; set; }
    }

    public class UpdateUserProfileRequest
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Gender { get; set; } = string.Empty;
        public decimal Weight { get; set; }
        public decimal Height { get; set; }
    }
}