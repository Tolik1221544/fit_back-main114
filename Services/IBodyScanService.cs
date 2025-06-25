using FitnessTracker.API.DTOs;

namespace FitnessTracker.API.Services
{
    public interface IBodyScanService
    {
        Task<IEnumerable<BodyScanDto>> GetUserBodyScansAsync(string userId, DateTime? startDate = null, DateTime? endDate = null);
        Task<BodyScanDto?> GetBodyScanByIdAsync(string userId, string scanId);
        Task<BodyScanDto> AddBodyScanAsync(string userId, AddBodyScanRequest request);
        Task<BodyScanDto> UpdateBodyScanAsync(string userId, string scanId, UpdateBodyScanRequest request);
        Task DeleteBodyScanAsync(string userId, string scanId);
        Task<BodyScanComparisonDto> GetBodyScanComparisonAsync(string userId, string? previousScanId = null);
    }
}