namespace FitnessTracker.API.Services.AI
{
    public interface IGoogleCloudTokenService
    {
        Task<string> GetAccessTokenAsync();
        Task<bool> ValidateServiceAccountAsync();
    }
}