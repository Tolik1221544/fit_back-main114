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
            CreateMap<Activity, ActivityDto>();
            CreateMap<ActivityDto, Activity>();

            // Coin transaction mappings
            CreateMap<CoinTransaction, CoinTransactionDto>();

            // Skin mappings
            CreateMap<Skin, SkinDto>();

            // Mission mappings
            CreateMap<Mission, MissionDto>();
            CreateMap<Achievement, AchievementDto>();
        }
    }
}
