namespace FitnessTracker.API.DTOs
{
    public class SendCodeRequest
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

    public class AuthResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public UserDto User { get; set; } = null!;
    }
}
