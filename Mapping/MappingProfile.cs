using AutoMapper;
using FitnessTracker.API.Models;
using FitnessTracker.API.DTOs;

namespace FitnessTracker.API.Mapping
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // User mappings
            CreateMap<User, UserDto>();
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

            // Coin transaction mappings
            CreateMap<CoinTransaction, CoinTransactionDto>();

            // LW Coin transaction mappings
            CreateMap<LwCoinTransaction, LwCoinTransactionDto>();

            // Skin mappings
            CreateMap<Skin, SkinDto>();

            // Mission mappings
            CreateMap<Mission, MissionDto>();
            CreateMap<Achievement, AchievementDto>();
        }
    }
}