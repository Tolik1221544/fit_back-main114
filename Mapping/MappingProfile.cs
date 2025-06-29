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
            CreateMap<CardioData, CardioDataDto>();
            CreateMap<CardioDataDto, CardioData>();

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
        }
    }
}