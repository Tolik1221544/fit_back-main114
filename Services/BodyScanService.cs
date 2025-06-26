using FitnessTracker.API.DTOs;
using FitnessTracker.API.Models;
using FitnessTracker.API.Repositories;
using AutoMapper;

namespace FitnessTracker.API.Services
{
    public class BodyScanService : IBodyScanService
    {
        private readonly IBodyScanRepository _bodyScanRepository;
        private readonly IMapper _mapper;

        public BodyScanService(IBodyScanRepository bodyScanRepository, IMapper mapper)
        {
            _bodyScanRepository = bodyScanRepository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<BodyScanDto>> GetUserBodyScansAsync(string userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var scans = await _bodyScanRepository.GetByUserIdAsync(userId);

            if (startDate.HasValue)
                scans = scans.Where(s => s.ScanDate >= startDate.Value);

            if (endDate.HasValue)
                scans = scans.Where(s => s.ScanDate <= endDate.Value);

            return _mapper.Map<IEnumerable<BodyScanDto>>(scans);
        }

        public async Task<BodyScanDto?> GetBodyScanByIdAsync(string userId, string scanId)
        {
            var scan = await _bodyScanRepository.GetByIdAsync(scanId);
            if (scan == null || scan.UserId != userId)
                return null;

            return _mapper.Map<BodyScanDto>(scan);
        }

        public async Task<BodyScanDto> AddBodyScanAsync(string userId, AddBodyScanRequest request)
        {
            var bodyScan = new BodyScan
            {
                UserId = userId,
                FrontImageUrl = request.FrontImageUrl,
                SideImageUrl = request.SideImageUrl,
                BackImageUrl = request.BackImageUrl,
                Weight = request.Weight,
                BodyFatPercentage = request.BodyFatPercentage,
                MusclePercentage = request.MusclePercentage,
                WaistCircumference = request.WaistCircumference,
                ChestCircumference = request.ChestCircumference,
                HipCircumference = request.HipCircumference,
                Notes = request.Notes,
                ScanDate = request.ScanDate
            };

            var createdScan = await _bodyScanRepository.CreateAsync(bodyScan);
            return _mapper.Map<BodyScanDto>(createdScan);
        }

        public async Task<BodyScanDto> UpdateBodyScanAsync(string userId, string scanId, UpdateBodyScanRequest request)
        {
            var scan = await _bodyScanRepository.GetByIdAsync(scanId);
            if (scan == null || scan.UserId != userId)
                throw new ArgumentException("Body scan not found");

            if (!string.IsNullOrEmpty(request.FrontImageUrl))
                scan.FrontImageUrl = request.FrontImageUrl;
            if (!string.IsNullOrEmpty(request.SideImageUrl))
                scan.SideImageUrl = request.SideImageUrl;
            if (!string.IsNullOrEmpty(request.BackImageUrl))
                scan.BackImageUrl = request.BackImageUrl;
            if (request.Weight.HasValue)
                scan.Weight = request.Weight.Value;
            if (request.BodyFatPercentage.HasValue)
                scan.BodyFatPercentage = request.BodyFatPercentage;
            if (request.MusclePercentage.HasValue)
                scan.MusclePercentage = request.MusclePercentage;
            if (request.WaistCircumference.HasValue)
                scan.WaistCircumference = request.WaistCircumference;
            if (request.ChestCircumference.HasValue)
                scan.ChestCircumference = request.ChestCircumference;
            if (request.HipCircumference.HasValue)
                scan.HipCircumference = request.HipCircumference;
            if (!string.IsNullOrEmpty(request.Notes))
                scan.Notes = request.Notes;

            var updatedScan = await _bodyScanRepository.UpdateAsync(scan);
            return _mapper.Map<BodyScanDto>(updatedScan);
        }

        public async Task DeleteBodyScanAsync(string userId, string scanId)
        {
            var scan = await _bodyScanRepository.GetByIdAsync(scanId);
            if (scan == null || scan.UserId != userId)
                throw new ArgumentException("Body scan not found");

            await _bodyScanRepository.DeleteAsync(scanId);
        }

        public async Task<BodyScanComparisonDto> GetBodyScanComparisonAsync(string userId, string? previousScanId = null)
        {
            var scans = await _bodyScanRepository.GetByUserIdAsync(userId);
            var orderedScans = scans.OrderByDescending(s => s.ScanDate).ToList();

            if (!orderedScans.Any())
                throw new ArgumentException("No body scans found");

            var currentScan = orderedScans.First();
            BodyScan? previousScan = null;

            if (!string.IsNullOrEmpty(previousScanId))
            {
                previousScan = orderedScans.FirstOrDefault(s => s.Id == previousScanId);
            }
            else if (orderedScans.Count > 1)
            {
                previousScan = orderedScans[1];
            }

            var comparison = new BodyScanComparisonDto
            {
                CurrentScan = _mapper.Map<BodyScanDto>(currentScan),
                PreviousScan = previousScan != null ? _mapper.Map<BodyScanDto>(previousScan) : null
            };

            if (previousScan != null)
            {
                comparison.Progress = new BodyScanProgressDto
                {
                    WeightDifference = currentScan.Weight - previousScan.Weight,
                    BodyFatDifference = (currentScan.BodyFatPercentage ?? 0) - (previousScan.BodyFatPercentage ?? 0),
                    MuscleDifference = (currentScan.MusclePercentage ?? 0) - (previousScan.MusclePercentage ?? 0),
                    WaistDifference = (currentScan.WaistCircumference ?? 0) - (previousScan.WaistCircumference ?? 0),
                    ChestDifference = (currentScan.ChestCircumference ?? 0) - (previousScan.ChestCircumference ?? 0),
                    HipDifference = (currentScan.HipCircumference ?? 0) - (previousScan.HipCircumference ?? 0),
                    DaysBetweenScans = (int)(currentScan.ScanDate - previousScan.ScanDate).TotalDays
                };
            }

            return comparison;
        }
    }
}