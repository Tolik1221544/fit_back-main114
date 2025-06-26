using FitnessTracker.API.DTOs;
using FitnessTracker.API.Models;
using FitnessTracker.API.Repositories;
using AutoMapper;

namespace FitnessTracker.API.Services
{
    public class MissionService : IMissionService
    {
        private readonly IMissionRepository _missionRepository;
        private readonly IAchievementService _achievementService;
        private readonly IExperienceService _experienceService;
        private readonly IMapper _mapper;
        private readonly ILogger<MissionService> _logger;

        public MissionService(
            IMissionRepository missionRepository,
            IAchievementService achievementService,
            IExperienceService experienceService,
            IMapper mapper,
            ILogger<MissionService> logger)
        {
            _missionRepository = missionRepository;
            _achievementService = achievementService;
            _experienceService = experienceService;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<IEnumerable<MissionDto>> GetUserMissionsAsync(string userId)
        {
            var missions = await _missionRepository.GetActiveMissionsAsync();
            var userMissions = await _missionRepository.GetUserMissionsAsync(userId);
            var userMissionDict = userMissions.ToDictionary(um => um.MissionId);

            var missionDtos = new List<MissionDto>();

            foreach (var mission in missions)
            {
                var userMission = userMissionDict.GetValueOrDefault(mission.Id);

                var missionDto = new MissionDto
                {
                    Id = mission.Id,
                    Title = mission.Title,
                    Icon = mission.Icon,
                    RewardExperience = mission.RewardExperience,
                    Type = mission.Type,
                    TargetValue = mission.TargetValue,
                    Progress = userMission?.Progress ?? 0,
                    IsCompleted = userMission?.IsCompleted ?? false,
                    CompletedAt = userMission?.CompletedAt
                };

                missionDtos.Add(missionDto);
            }

            return missionDtos.OrderBy(m => m.IsCompleted).ThenBy(m => m.Id);
        }

        public async Task<IEnumerable<AchievementDto>> GetUserAchievementsAsync(string userId)
        {
            return await _achievementService.GetUserAchievementsAsync(userId);
        }

        public async Task UpdateMissionProgressAsync(string userId, string missionType, int incrementValue = 1)
        {
            var missions = await _missionRepository.GetActiveMissionsAsync();
            var relevantMissions = missions.Where(m => m.Type == missionType).ToList();

            foreach (var mission in relevantMissions)
            {
                var userMission = await _missionRepository.GetUserMissionAsync(userId, mission.Id);

                if (userMission == null)
                {
                    // Create new user mission
                    userMission = new UserMission
                    {
                        UserId = userId,
                        MissionId = mission.Id,
                        Progress = incrementValue
                    };

                    await _missionRepository.CreateUserMissionAsync(userMission);
                }
                else if (!userMission.IsCompleted)
                {
                    // Update existing mission progress
                    userMission.Progress += incrementValue;
                    await _missionRepository.UpdateUserMissionAsync(userMission);
                }

                // Check if mission is completed
                if (!userMission.IsCompleted && userMission.Progress >= mission.TargetValue)
                {
                    userMission.IsCompleted = true;
                    userMission.CompletedAt = DateTime.UtcNow;
                    await _missionRepository.UpdateUserMissionAsync(userMission);

                    // Award experience
                    await _experienceService.AddExperienceAsync(userId, mission.RewardExperience,
                        "mission", $"Mission completed: {mission.Title}");

                    _logger.LogInformation($"Mission completed for user {userId}: {mission.Title}");
                }
            }

            // Check for achievements
            await _achievementService.CheckAndUnlockAchievementsAsync(userId);
        }
    }
}