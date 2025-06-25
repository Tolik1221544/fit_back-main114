using FitnessTracker.API.DTOs;
using FitnessTracker.API.Models;
using FitnessTracker.API.Repositories;
using AutoMapper;

namespace FitnessTracker.API.Services
{
    public class MissionService : IMissionService
    {
        private readonly IMissionRepository _missionRepository;
        private readonly ICoinService _coinService;
        private readonly IMapper _mapper;

        public MissionService(IMissionRepository missionRepository, ICoinService coinService, IMapper mapper)
        {
            _missionRepository = missionRepository;
            _coinService = coinService;
            _mapper = mapper;
        }

        public async Task<IEnumerable<MissionDto>> GetUserMissionsAsync(string userId)
        {
            var missions = await _missionRepository.GetActiveMissionsAsync();
            var userMissions = await _missionRepository.GetUserMissionsAsync(userId);
            var userMissionDict = userMissions.ToDictionary(um => um.MissionId);

            var missionDtos = new List<MissionDto>();

            foreach (var mission in missions)
            {
                var missionDto = _mapper.Map<MissionDto>(mission);
                
                if (userMissionDict.TryGetValue(mission.Id, out var userMission))
                {
                    missionDto.Progress = userMission.Progress;
                    missionDto.IsCompleted = userMission.IsCompleted;
                }
                else
                {
                    // Create new user mission
                    var newUserMission = new UserMission
                    {
                        UserId = userId,
                        MissionId = mission.Id
                    };
                    await _missionRepository.CreateUserMissionAsync(newUserMission);
                }

                missionDtos.Add(missionDto);
            }

            return missionDtos;
        }

        public async Task<IEnumerable<AchievementDto>> GetUserAchievementsAsync(string userId)
        {
            var achievements = await _missionRepository.GetUserAchievementsAsync(userId);
            return _mapper.Map<IEnumerable<AchievementDto>>(achievements);
        }

        public async Task UpdateMissionProgressAsync(string userId, string missionType, int incrementValue = 1)
        {
            var missions = await _missionRepository.GetActiveMissionsAsync();
            var relevantMissions = missions.Where(m => m.Type == missionType);

            foreach (var mission in relevantMissions)
            {
                var userMission = await _missionRepository.GetUserMissionAsync(userId, mission.Id);
                if (userMission == null)
                {
                    userMission = new UserMission
                    {
                        UserId = userId,
                        MissionId = mission.Id
                    };
                    userMission = await _missionRepository.CreateUserMissionAsync(userMission);
                }

                if (!userMission.IsCompleted)
                {
                    userMission.Progress += incrementValue;

                    if (userMission.Progress >= mission.TargetValue)
                    {
                        userMission.IsCompleted = true;
                        userMission.CompletedAt = DateTime.UtcNow;

                        // Начисляем ОПЫТ вместо монет
                        await _experienceService.AddExperienceAsync(userId, mission.RewardExperience,
                            "mission", $"Mission completed: {mission.Title}");
                    }

                    await _missionRepository.UpdateUserMissionAsync(userMission);
                }
            }
        }
    }
}
