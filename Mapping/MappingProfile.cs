using AutoMapper;
using FitnessTracker.API.Models;
using FitnessTracker.API.DTOs;

namespace FitnessTracker.API.Mapping
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<User, UserDto>()
                .ForMember(dest => dest.MaxExperience, opt => opt.Ignore())
                .ForMember(dest => dest.ExperienceToNextLevel, opt => opt.Ignore())
                .ForMember(dest => dest.ExperienceProgress, opt => opt.Ignore());

            CreateMap<UpdateUserProfileRequest, User>();

            // Food intake mappings
            CreateMap<FoodIntake, FoodIntakeDto>();
            CreateMap<FoodIntakeDto, FoodIntake>();
            CreateMap<NutritionPer100g, NutritionPer100gDto>();
            CreateMap<NutritionPer100gDto, NutritionPer100g>();

            // Activity mappings
            CreateMap<Activity, ActivityDto>()
                .ForMember(dest => dest.StrengthData, opt => opt.MapFrom(src => src.StrengthData))
                .ForMember(dest => dest.CardioData, opt => opt.MapFrom(src => src.CardioData));
            CreateMap<ActivityDto, Activity>();

            CreateMap<StrengthData, StrengthDataDto>();
            CreateMap<StrengthDataDto, StrengthData>();
            CreateMap<StrengthSet, StrengthSetDto>();
            CreateMap<StrengthSetDto, StrengthSet>();

            CreateMap<CardioData, CardioDataDto>();
            CreateMap<CardioDataDto, CardioData>();
           
            CreateMap<PlankData, PlankDataDto>();
            CreateMap<PlankDataDto, PlankData>();

            CreateMap<JumpRopeData, JumpRopeDataDto>();
            CreateMap<JumpRopeDataDto, JumpRopeData>();

            // Steps mappings
            CreateMap<Steps, StepsDto>();
            CreateMap<StepsDto, Steps>();

            // LW Coin transaction mappings
            CreateMap<LwCoinTransaction, LwCoinTransactionDto>();

            // Experience transaction mappings
            CreateMap<ExperienceTransaction, ExperienceTransactionDto>();

            // Body scan mappings
            CreateMap<BodyScan, BodyScanDto>();
            CreateMap<BodyScanDto, BodyScan>();

            CreateMap<Skin, SkinDto>()
                .ForMember(dest => dest.IsOwned, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore());

            // Mission mappings
            CreateMap<Mission, MissionDto>();
            CreateMap<Achievement, AchievementDto>();

            // Referral mappings
            CreateMap<Referral, ReferredUserDto>()
                .ForMember(dest => dest.Email, opt => opt.Ignore()) // Will be set manually
                .ForMember(dest => dest.Name, opt => opt.Ignore()) // Will be set manually
                .ForMember(dest => dest.Level, opt => opt.Ignore()) // Will be set manually
                .ForMember(dest => dest.JoinedAt, opt => opt.MapFrom(src => src.CreatedAt))
                .ForMember(dest => dest.RewardCoins, opt => opt.MapFrom(src => src.RewardCoins))
                .ForMember(dest => dest.IsPremium, opt => opt.Ignore()) // Will be set manually
                .ForMember(dest => dest.Status, opt => opt.Ignore()); // Will be set manually

            // ✅ НОВЫЕ МАППИНГИ: Goal mappings
            CreateMap<Goal, GoalDto>()
                .ForMember(dest => dest.TodayProgress, opt => opt.Ignore()) // Will be set manually
                .ForMember(dest => dest.TotalDays, opt => opt.Ignore()) // Will be calculated
                .ForMember(dest => dest.CompletedDays, opt => opt.Ignore()) // Will be calculated
                .ForMember(dest => dest.AverageProgress, opt => opt.Ignore()); // Will be calculated

            CreateMap<CreateGoalRequest, Goal>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.UserId, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.StartDate, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.ProgressPercentage, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.Ignore())
                .ForMember(dest => dest.CompletedAt, opt => opt.Ignore())
                .ForMember(dest => dest.User, opt => opt.Ignore())
                .ForMember(dest => dest.DailyProgress, opt => opt.Ignore());

            CreateMap<DailyGoalProgress, DailyGoalProgressDto>()
                .ForMember(dest => dest.TargetCalories, opt => opt.Ignore()) // Will be set manually
                .ForMember(dest => dest.TargetProtein, opt => opt.Ignore()) // Will be set manually
                .ForMember(dest => dest.TargetCarbs, opt => opt.Ignore()) // Will be set manually
                .ForMember(dest => dest.TargetFats, opt => opt.Ignore()) // Will be set manually
                .ForMember(dest => dest.TargetSteps, opt => opt.Ignore()) // Will be set manually
                .ForMember(dest => dest.TargetWorkouts, opt => opt.Ignore()); // Will be set manually

            CreateMap<UpdateDailyProgressRequest, DailyGoalProgress>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.GoalId, opt => opt.Ignore())
                .ForMember(dest => dest.UserId, opt => opt.Ignore())
                .ForMember(dest => dest.ActualCalories, opt => opt.MapFrom(src => src.ManualCalories ?? 0))
                .ForMember(dest => dest.ActualProtein, opt => opt.MapFrom(src => src.ManualProtein ?? 0))
                .ForMember(dest => dest.ActualCarbs, opt => opt.MapFrom(src => src.ManualCarbs ?? 0))
                .ForMember(dest => dest.ActualFats, opt => opt.MapFrom(src => src.ManualFats ?? 0))
                .ForMember(dest => dest.ActualSteps, opt => opt.MapFrom(src => src.ManualSteps ?? 0))
                .ForMember(dest => dest.ActualWorkouts, opt => opt.MapFrom(src => src.ManualWorkouts ?? 0))
                .ForMember(dest => dest.ActualActiveMinutes, opt => opt.MapFrom(src => src.ManualActiveMinutes ?? 0))
                .ForMember(dest => dest.CaloriesProgress, opt => opt.Ignore()) // Will be calculated
                .ForMember(dest => dest.ProteinProgress, opt => opt.Ignore()) // Will be calculated
                .ForMember(dest => dest.CarbsProgress, opt => opt.Ignore()) // Will be calculated
                .ForMember(dest => dest.FatsProgress, opt => opt.Ignore()) // Will be calculated
                .ForMember(dest => dest.StepsProgress, opt => opt.Ignore()) // Will be calculated
                .ForMember(dest => dest.WorkoutProgress, opt => opt.Ignore()) // Will be calculated
                .ForMember(dest => dest.OverallProgress, opt => opt.Ignore()) // Will be calculated
                .ForMember(dest => dest.IsCompleted, opt => opt.Ignore()) // Will be calculated
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.Goal, opt => opt.Ignore())
                .ForMember(dest => dest.User, opt => opt.Ignore());
        }
    }
}