using FitnessTracker.API.DTOs;
using FitnessTracker.API.Models;
using FitnessTracker.API.Repositories;
using AutoMapper;

namespace FitnessTracker.API.Services
{
    public class FoodIntakeService : IFoodIntakeService
    {
        private readonly IFoodIntakeRepository _foodIntakeRepository;
        private readonly IMissionService _missionService;
        private readonly IMapper _mapper;

        public FoodIntakeService(IFoodIntakeRepository foodIntakeRepository, IMissionService missionService, IMapper mapper)
        {
            _foodIntakeRepository = foodIntakeRepository;
            _missionService = missionService;
            _mapper = mapper;
        }

        public async Task<IEnumerable<FoodIntakeDto>> GetUserFoodIntakesAsync(string userId)
        {
            var foodIntakes = await _foodIntakeRepository.GetByUserIdAsync(userId);
            return _mapper.Map<IEnumerable<FoodIntakeDto>>(foodIntakes);
        }

        public async Task<IEnumerable<FoodIntakeDto>> GetUserFoodIntakesByDateAsync(string userId, DateTime date)
        {
            var foodIntakes = await _foodIntakeRepository.GetByUserIdAndDateAsync(userId, date);
            return _mapper.Map<IEnumerable<FoodIntakeDto>>(foodIntakes);
        }

        public async Task<IEnumerable<FoodIntakeDto>> AddFoodIntakeAsync(string userId, AddFoodIntakeRequest request)
        {
            var foodIntakes = request.Items.Select(item => new FoodIntake
            {
                UserId = userId,
                TempItemId = item.TempItemId,
                Name = item.Name,
                Weight = item.Weight,
                WeightType = item.WeightType,
                Image = item.Image,
                DateTime = request.DateTime,

                // Фактический расчет происходит при отображении/статистике
                NutritionPer100g = new NutritionPer100g
                {
                    Calories = item.NutritionPer100g.Calories,
                    Proteins = item.NutritionPer100g.Proteins,
                    Fats = item.NutritionPer100g.Fats,
                    Carbs = item.NutritionPer100g.Carbs
                }
            });

            var createdFoodIntakes = await _foodIntakeRepository.CreateManyAsync(foodIntakes);

            // Update mission progress
            await _missionService.UpdateMissionProgressAsync(userId, "food_intake", request.Items.Count);

            var currentHour = request.DateTime.Hour;
            if (currentHour >= 6 && currentHour <= 11)
            {
                await _missionService.UpdateMissionProgressAsync(userId, "breakfast_calories");
            }

            return _mapper.Map<IEnumerable<FoodIntakeDto>>(createdFoodIntakes);
        }

        public async Task<FoodIntakeDto> UpdateFoodIntakeAsync(string userId, string foodIntakeId, UpdateFoodIntakeRequest request)
        {
            var foodIntake = await _foodIntakeRepository.GetByIdAsync(foodIntakeId);
            if (foodIntake == null || foodIntake.UserId != userId)
                throw new ArgumentException("Food intake not found");

            foodIntake.Name = request.Name;
            foodIntake.Weight = request.Weight;
            foodIntake.WeightType = request.WeightType;

            foodIntake.NutritionPer100g.Calories = request.NutritionPer100g.Calories;
            foodIntake.NutritionPer100g.Proteins = request.NutritionPer100g.Proteins;
            foodIntake.NutritionPer100g.Fats = request.NutritionPer100g.Fats;
            foodIntake.NutritionPer100g.Carbs = request.NutritionPer100g.Carbs;

            var updatedFoodIntake = await _foodIntakeRepository.UpdateAsync(foodIntake);

            var currentHour = updatedFoodIntake.DateTime.Hour;
            if (currentHour >= 6 && currentHour <= 11)
            {
                await _missionService.UpdateMissionProgressAsync(userId, "breakfast_calories");
            }

            return _mapper.Map<FoodIntakeDto>(updatedFoodIntake);
        }

        public async Task DeleteFoodIntakeAsync(string userId, string foodIntakeId)
        {
            var foodIntake = await _foodIntakeRepository.GetByIdAsync(foodIntakeId);
            if (foodIntake == null || foodIntake.UserId != userId)
                throw new ArgumentException("Food intake not found");

            await _foodIntakeRepository.DeleteAsync(foodIntakeId);

            var currentHour = foodIntake.DateTime.Hour;
            if (currentHour >= 6 && currentHour <= 11)
            {
                await _missionService.UpdateMissionProgressAsync(userId, "breakfast_calories");
            }
        }

        public Task<ScanFoodResponse> ScanFoodAsync(string userId, byte[] imageData)
        {
            // In a real application, you would use AI/ML services to analyze the image
            // For now, we'll return mock data
            var mockFoodItems = new List<FoodIntakeDto>
            {
                new FoodIntakeDto
                {
                    Id = Guid.NewGuid().ToString(),
                    TempItemId = "temp1",
                    Name = "Apple",
                    Weight = 150,
                    WeightType = "g",
                    DateTime = DateTime.UtcNow,
                    NutritionPer100g = new NutritionPer100gDto
                    {
                        Calories = 52,
                        Proteins = 0.3m,
                        Fats = 0.2m,
                        Carbs = 14
                    }
                }
            };

            return Task.FromResult(new ScanFoodResponse
            {
                Items = mockFoodItems
            });
        }

        public async Task<FoodIntakeDto?> GetFoodItemByIdAsync(string userId, string foodIntakeId)
        {
            var foodIntake = await _foodIntakeRepository.GetByIdAsync(foodIntakeId);
            if (foodIntake == null || foodIntake.UserId != userId)
                return null;

            return _mapper.Map<FoodIntakeDto>(foodIntake);
        }
    }
}