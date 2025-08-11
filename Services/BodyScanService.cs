using FitnessTracker.API.DTOs;
using FitnessTracker.API.Models;
using FitnessTracker.API.Repositories;
using AutoMapper;

namespace FitnessTracker.API.Services
{
    public class BodyScanService : IBodyScanService
    {
        private readonly IBodyScanRepository _bodyScanRepository;
        private readonly IUserRepository _userRepository;
        private readonly IExperienceService _experienceService;
        private readonly IMissionService _missionService; 
        private readonly IGoalService _goalService;
        private readonly IMapper _mapper;
        private readonly ILogger<BodyScanService> _logger;

        public BodyScanService(
            IBodyScanRepository bodyScanRepository,
            IUserRepository userRepository,
            IExperienceService experienceService,
            IMissionService missionService, 
            IGoalService goalService, 
            IMapper mapper,
            ILogger<BodyScanService> logger)
        {
            _bodyScanRepository = bodyScanRepository;
            _userRepository = userRepository;
            _experienceService = experienceService;
            _missionService = missionService; 
            _goalService = goalService; 
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<IEnumerable<BodyScanDto>> GetUserBodyScansAsync(string userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var scans = await _bodyScanRepository.GetByUserIdAsync(userId);

            if (startDate.HasValue)
                scans = scans.Where(s => s.ScanDate >= startDate.Value);

            if (endDate.HasValue)
                scans = scans.Where(s => s.ScanDate <= endDate.Value);

            var scanDtos = _mapper.Map<IEnumerable<BodyScanDto>>(scans);

            _logger.LogInformation($"📊 Returning {scanDtos.Count()} body scans for user {userId} with weight data");

            return scanDtos.OrderByDescending(s => s.ScanDate);
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
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                throw new ArgumentException("User not found");

            var currentWeight = request.Weight > 0 ? request.Weight : user.Weight;
            var height = user.Height;
            var age = user.Age;
            var gender = user.Gender;

            _logger.LogInformation($"💪 Creating body scan for user {userId} with weight: {currentWeight}kg, height: {height}cm, age: {age}, gender: {gender}");

            int calculatedBMR = CalculateBasalMetabolicRate(currentWeight, height, age, gender);
            string bmrCategory = GetMetabolicRateCategory(calculatedBMR);

            var bodyScan = new BodyScan
            {
                UserId = userId,
                FrontImageUrl = request.FrontImageUrl,
                SideImageUrl = request.SideImageUrl,
                BackImageUrl = request.BackImageUrl,
                Weight = currentWeight,
                BodyFatPercentage = request.BodyFatPercentage,
                MusclePercentage = request.MusclePercentage,
                WaistCircumference = request.WaistCircumference,
                ChestCircumference = request.ChestCircumference,
                HipCircumference = request.HipCircumference,
                BasalMetabolicRate = request.BasalMetabolicRate ?? calculatedBMR,
                MetabolicRateCategory = request.MetabolicRateCategory ?? bmrCategory,
                Notes = request.Notes,
                ScanDate = request.ScanDate
            };

            var createdScan = await _bodyScanRepository.CreateAsync(bodyScan);

            try
            {
                await _experienceService.AddExperienceAsync(userId, 75, "body_scan",
                    $"Body scan completed (Weight: {currentWeight}kg, BMR: {bodyScan.BasalMetabolicRate} kcal)");

                await _goalService.RecalculateDailyProgressAsync(userId, DateTime.UtcNow.Date);

                await _missionService.UpdateMissionProgressAsync(userId, "body_scan", 1);
                await _missionService.UpdateMissionProgressAsync(userId, "weekly_body_scan", 1);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating progress after body scan: {ex.Message}");
            }

            _logger.LogInformation($"✅ Body scan created for user {userId} - Weight: {currentWeight}kg, BMR: {bodyScan.BasalMetabolicRate} kcal ({bmrCategory})");

            return _mapper.Map<BodyScanDto>(createdScan);
        }

        public async Task<BodyScanDto> UpdateBodyScanAsync(string userId, string scanId, UpdateBodyScanRequest request)
        {
            var scan = await _bodyScanRepository.GetByIdAsync(scanId);
            if (scan == null || scan.UserId != userId)
                throw new ArgumentException("Body scan not found");

            var user = await _userRepository.GetByIdAsync(userId);

            if (!string.IsNullOrEmpty(request.FrontImageUrl))
                scan.FrontImageUrl = request.FrontImageUrl;
            if (!string.IsNullOrEmpty(request.SideImageUrl))
                scan.SideImageUrl = request.SideImageUrl;
            if (!string.IsNullOrEmpty(request.BackImageUrl))
                scan.BackImageUrl = request.BackImageUrl;

            if (request.Weight.HasValue && request.Weight > 0)
            {
                scan.Weight = request.Weight.Value;

                if (user != null)
                {
                    var newBMR = CalculateBasalMetabolicRate(request.Weight.Value, user.Height, user.Age, user.Gender);
                    scan.BasalMetabolicRate = newBMR;
                    scan.MetabolicRateCategory = GetMetabolicRateCategory(newBMR);
                }
            }

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

            if (request.BasalMetabolicRate.HasValue)
                scan.BasalMetabolicRate = request.BasalMetabolicRate;
            if (!string.IsNullOrEmpty(request.MetabolicRateCategory))
                scan.MetabolicRateCategory = request.MetabolicRateCategory;
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
                    DaysBetweenScans = (int)(currentScan.ScanDate - previousScan.ScanDate).TotalDays,

                    BasalMetabolicRateDifference = (currentScan.BasalMetabolicRate ?? 0) - (previousScan.BasalMetabolicRate ?? 0),
                    MetabolicRateChange = GetMetabolicRateChange(
                        previousScan.MetabolicRateCategory ?? "Нормальный",
                        currentScan.MetabolicRateCategory ?? "Нормальный"
                    )
                };
            }

            return comparison;
        }

        private int CalculateBasalMetabolicRate(decimal weight, decimal height, int age, string gender)
        {
            double weightKg = (double)weight;
            double heightCm = (double)height;

            if (gender?.ToLowerInvariant().Contains("male") == true && !gender.ToLowerInvariant().Contains("female"))
            {
                return (int)Math.Round(10 * weightKg + 6.25 * heightCm - 5 * age + 5);
            }
            else
            {
                return (int)Math.Round(10 * weightKg + 6.25 * heightCm - 5 * age - 161);
            }
        }

        private string GetMetabolicRateCategory(int bmr)
        {
            return bmr switch
            {
                < 1200 => "Низкий",
                >= 1200 and <= 2000 => "Нормальный",
                > 2000 => "Высокий"
            };
        }

        private string GetMetabolicRateChange(string previousCategory, string currentCategory)
        {
            if (previousCategory == currentCategory)
                return "Без изменений";

            var categories = new Dictionary<string, int>
            {
                ["Низкий"] = 1,
                ["Нормальный"] = 2,
                ["Высокий"] = 3
            };

            var prevValue = categories.GetValueOrDefault(previousCategory, 2);
            var currValue = categories.GetValueOrDefault(currentCategory, 2);

            return currValue > prevValue ? "Улучшился" : "Ухудшился";
        }

    }
}