namespace FitnessTracker.API.Services
{
    public interface IImageService
    {
        Task<string> SaveImageAsync(IFormFile image, string folder);
        Task<string> SaveImageAsync(byte[] imageData, string fileName, string folder);
        Task<bool> DeleteImageAsync(string imageUrl);
        string GetImageUrl(string fileName, string folder);
    }
}