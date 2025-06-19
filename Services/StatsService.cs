using FitnessTracker.API.Repositories;

namespace FitnessTracker.API.Services
{
    public class StatsService : IStatsService
    {
        private readonly IFoodIntakeRepository _foodIntakeRepository;
        private readonly IActivityRepository _activityRepository;
        private readonly IUserRepository _userRepository;

        public StatsService(IFoodIntakeRepository foodIntakeRepository, IActivityRepository activityRepository, IUserRepository userRepository)
        {
            _foodIntakeRepository = foodIntakeRepository;
            _activityRepository = activityRepository;
            _userRepository = userRepository;
        }

        public async Task<object> GetUserStatsAsync(string userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            var totalFoodIntakes = (await _foodIntakeRepository.GetByUserIdAsync(userId)).Count();
            var totalActivities = await _activityRepository.GetUserActivityCountAsync(userId);

            return new
            {
                Level = user?.Level ?? 1,
                Coins = user?.Coins ?? 0,
                TotalFoodIntakes = totalFoodIntakes,
                TotalActivities = totalActivities,
                JoinedDays = user != null ? (DateTime.UtcNow - user.JoinedAt).Days : 0
            };
        }

        public async Task<object> GetNutritionStatsAsync(string userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var foodIntakes = await _foodIntakeRepository.GetByUserIdAsync(userId);
            
            if (startDate.HasValue)
                foodIntakes = foodIntakes.Where(f => f.DateTime >= startDate.Value);
            
            if (endDate.HasValue)
                foodIntakes = foodIntakes.Where(f => f.DateTime <= endDate.Value);

            var totalCalories = foodIntakes.Sum(f => (f.NutritionPer100g.Calories * f.Weight) / 100);
            var totalProteins = foodIntakes.Sum(f => (f.NutritionPer100g.Proteins * f.Weight) / 100);
            var totalFats = foodIntakes.Sum(f => (f.NutritionPer100g.Fats * f.Weight) / 100);
            var totalCarbs = foodIntakes.Sum(f => (f.NutritionPer100g.Carbs * f.Weight) / 100);

            return new
            {
                TotalCalories = totalCalories,
                TotalProteins = totalProteins,
                TotalFats = totalFats,
                TotalCarbs = totalCarbs,
                AverageCaloriesPerDay = foodIntakes.Any() ? totalCalories / Math.Max(1, (endDate?.Subtract(startDate ?? DateTime.UtcNow).Days ?? 1)) : 0
            };
        }

        public async Task<object> GetActivityStatsAsync(string userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var activities = await _activityRepository.GetByUserIdAsync(userId);
            
            if (startDate.HasValue)
                activities = activities.Where(a => a.CreatedAt >= startDate.Value);
            
            if (endDate.HasValue)
                activities = activities.Where(a => a.CreatedAt <= endDate.Value);

            var activityTypes = activities.GroupBy(a => a.Type)
                .Select(g => new { Type = g.Key, Count = g.Count() });

            return new
            {
                TotalActivities = activities.Count(),
                ActivityTypes = activityTypes,
                MostPopularActivity = activityTypes.OrderByDescending(a => a.Count).FirstOrDefault()?.Type ?? "None"
            };
        }
    }
}
