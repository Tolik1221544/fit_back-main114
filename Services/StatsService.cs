using FitnessTracker.API.Repositories;

namespace FitnessTracker.API.Services
{
    public class StatsService : IStatsService
    {
        private readonly IFoodIntakeRepository _foodIntakeRepository;
        private readonly IActivityRepository _activityRepository;
        private readonly IUserRepository _userRepository;
        private readonly IStepsRepository _stepsRepository;

        public StatsService(
            IFoodIntakeRepository foodIntakeRepository,
            IActivityRepository activityRepository,
            IUserRepository userRepository,
            IStepsRepository stepsRepository)
        {
            _foodIntakeRepository = foodIntakeRepository;
            _activityRepository = activityRepository;
            _userRepository = userRepository;
            _stepsRepository = stepsRepository;
        }

        public async Task<object> GetUserStatsAsync(string userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            var totalFoodIntakes = (await _foodIntakeRepository.GetByUserIdAsync(userId)).Count();
            var totalActivities = await _activityRepository.GetUserActivityCountAsync(userId);

            return new
            {
                Level = user?.Level ?? 1,
                Experience = user?.Experience ?? 0,
                LwCoins = user?.LwCoins ?? 300,
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

            var dayCount = startDate.HasValue && endDate.HasValue
                ? Math.Max(1, (endDate.Value - startDate.Value).Days + 1)
                : Math.Max(1, foodIntakes.GroupBy(f => f.DateTime.Date).Count());

            return new
            {
                TotalCalories = Math.Round(totalCalories, 2),
                TotalProteins = Math.Round(totalProteins, 2),
                TotalFats = Math.Round(totalFats, 2),
                TotalCarbs = Math.Round(totalCarbs, 2),
                AverageCaloriesPerDay = Math.Round(totalCalories / dayCount, 2),
                AverageProteinsPerDay = Math.Round(totalProteins / dayCount, 2),
                AverageFatsPerDay = Math.Round(totalFats / dayCount, 2),
                AverageCarbsPerDay = Math.Round(totalCarbs / dayCount, 2),
                TotalMeals = foodIntakes.Count(),
                DaysTracked = dayCount
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

            var activityCalories = activities.Where(a => a.Calories.HasValue).Sum(a => a.Calories!.Value);

   
            var allSteps = await _stepsRepository.GetByUserIdAsync(userId);

            if (startDate.HasValue)
                allSteps = allSteps.Where(s => s.Date >= startDate.Value.Date);

            if (endDate.HasValue)
                allSteps = allSteps.Where(s => s.Date <= endDate.Value.Date);

            var stepsCalories = allSteps.Where(s => s.Calories.HasValue).Sum(s => s.Calories!.Value);

     
            var totalCalories = activityCalories + stepsCalories;

            var strengthActivities = activities.Where(a => a.Type == "strength").Count();
            var cardioActivities = activities.Where(a => a.Type == "cardio").Count();

            var dayCount = startDate.HasValue && endDate.HasValue
                ? Math.Max(1, (endDate.Value - startDate.Value).Days + 1)
                : Math.Max(1, activities.GroupBy(a => a.CreatedAt.Date).Count());

            return new
            {
                TotalActivities = activities.Count(),
                TotalCalories = totalCalories, 
                StrengthWorkouts = strengthActivities,
                CardioWorkouts = cardioActivities,
                ActivityTypes = activityTypes,
                MostPopularActivity = activityTypes.OrderByDescending(a => a.Count).FirstOrDefault()?.Type ?? "None",
                LastActivity = activities.OrderByDescending(a => a.CreatedAt).FirstOrDefault()?.CreatedAt,
                AverageActivitiesPerDay = Math.Round((double)activities.Count() / dayCount, 2),
                AverageCaloriesPerDay = Math.Round((double)totalCalories / dayCount, 2), 
                DaysActive = activities.GroupBy(a => a.CreatedAt.Date).Count()
            };
        }
    }
}