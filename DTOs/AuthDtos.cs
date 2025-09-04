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
        public string? IdToken { get; set; }

      public string? ServerAuthCode { get; set; }
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

    public class AppleAuthRequest
    {
        public string IdToken { get; set; } = string.Empty;
        public string? AuthorizationCode { get; set; }
        public string? User { get; set; } 
    }

    public class GoogleTokenResponse
    {
        public string access_token { get; set; } = string.Empty;
        public int expires_in { get; set; }
        public string refresh_token { get; set; } = string.Empty;
        public string scope { get; set; } = string.Empty;
        public string token_type { get; set; } = string.Empty;
        public string id_token { get; set; } = string.Empty;
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
        public string Locale { get; set; } = "ru_RU";
        public DateTime JoinedAt { get; set; }
    }

    public class UpdateUserProfileRequest
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Gender { get; set; } = string.Empty;
        public decimal Weight { get; set; }
        public decimal Height { get; set; }
        public string? Locale { get; set; }
    }
}