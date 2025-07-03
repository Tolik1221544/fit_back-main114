using FitnessTracker.API.DTOs;
using FitnessTracker.API.Models;
using FitnessTracker.API.Repositories;
using AutoMapper;

namespace FitnessTracker.API.Services
{
    public class GoalService : IGoalService
    {
        private readonly IGoalRepository _goalRepository;
        private readonly IFoodIntakeRepository _foodIntakeRepository;
        private readonly IActivityRepository _activityRepository;
        private readonly IStepsRepository _stepsRepository;
        private readonly IUserRepository _userRepository;
        private readonly IExperienceService _experienceService;
        private readonly IMapper _mapper;
        private readonly ILogger<GoalService> _logger;

        public GoalService(
            IGoalRepository goalRepository,
            IFoodIntakeRepository foodIntakeRepository,
            IActivityRepository activityRepository,
            IStepsRepository stepsRepository,
            IUserRepository userRepository,
            IExperienceService experienceService,
            IMapper mapper,
            ILogger<GoalService> logger)
        {
            _goalRepository = goalRepository;
            _foodIntakeRepository = foodIntakeRepository;
            _activityRepository = activityRepository;
            _stepsRepository = stepsRepository;
            _userRepository = userRepository;
            _experienceService = experienceService;
            _mapper = mapper;
            _logger = logger;
        }

        // Goals
        public async Task<IEnumerable<GoalDto>> GetUserGoalsAsync(string userId)
        {
            var goals = await _goalRepository.GetUserGoalsAsync(userId);
            var goalDtos = new List<GoalDto>();

            foreach (var goal in goals)
            {
                var goalDto = _mapper.Map<GoalDto>(goal);

                // Добавляем прогресс за сегодня
                goalDto.TodayProgress = await GetTodayProgressForGoalAsync(userId, goal.Id);

                // Вычисляем статистику
                var progressStats = await CalculateGoalStatsAsync(goal);
                goalDto.TotalDays = progressStats.TotalDays;
                goalDto.CompletedDays = progressStats.CompletedDays;
                goalDto.AverageProgress = progressStats.AverageProgress;

                goalDtos.Add(goalDto);
            }

            return goalDtos;
        }

        public async Task<GoalDto?> GetActiveUserGoalAsync(string userId)
        {
            var goal = await _goalRepository.GetActiveUserGoalAsync(userId);
            if (goal == null) return null;

            var goalDto = _mapper.Map<GoalDto>(goal);
            goalDto.TodayProgress = await GetTodayProgressForGoalAsync(userId, goal.Id);

            var progressStats = await CalculateGoalStatsAsync(goal);
            goalDto.TotalDays = progressStats.TotalDays;
            goalDto.CompletedDays = progressStats.CompletedDays;
            goalDto.AverageProgress = progressStats.AverageProgress;

            return goalDto;
        }

        public async Task<GoalDto?> GetGoalByIdAsync(string userId, string goalId)
        {
            var goal = await _goalRepository.GetGoalByIdAsync(goalId);
            if (goal == null || goal.UserId != userId) return null;

            var goalDto = _mapper.Map<GoalDto>(goal);
            goalDto.TodayProgress = await GetTodayProgressForGoalAsync(userId, goal.Id);

            var progressStats = await CalculateGoalStatsAsync(goal);
            goalDto.TotalDays = progressStats.TotalDays;
            goalDto.CompletedDays = progressStats.CompletedDays;
            goalDto.AverageProgress = progressStats.AverageProgress;

            return goalDto;
        }

        public async Task<GoalDto> CreateGoalAsync(string userId, CreateGoalRequest request)
        {
            // Деактивируем старые активные цели
            var existingGoals = await _goalRepository.GetUserGoalsAsync(userId);
            foreach (var existingGoal in existingGoals.Where(g => g.IsActive))
            {
                existingGoal.IsActive = false;
                await _goalRepository.UpdateGoalAsync(existingGoal);
            }

            // Получаем шаблон для автозаполнения
            var template = await GetGoalTemplateAsync(request.GoalType);

            var goal = new Goal
            {
                UserId = userId,
                GoalType = request.GoalType,
                Title = !string.IsNullOrEmpty(request.Title) ? request.Title : template?.Title ?? GetDefaultTitle(request.GoalType),
                Description = !string.IsNullOrEmpty(request.Description) ? request.Description : template?.Description ?? "",

                // Целевые показатели
                TargetWeight = request.TargetWeight,
                CurrentWeight = request.CurrentWeight,
                TargetCalories = request.TargetCalories ?? template?.RecommendedCalories,
                TargetProtein = request.TargetProtein ?? template?.RecommendedProtein,
                TargetCarbs = request.TargetCarbs ?? template?.RecommendedCarbs,
                TargetFats = request.TargetFats ?? template?.RecommendedFats,

                // Активность
                TargetWorkoutsPerWeek = request.TargetWorkoutsPerWeek ?? template?.RecommendedWorkoutsPerWeek,
                TargetStepsPerDay = request.TargetStepsPerDay ?? template?.RecommendedStepsPerDay,
                TargetActiveMinutes = request.TargetActiveMinutes ?? template?.RecommendedActiveMinutes,

                EndDate = request.EndDate,
                IsActive = true
            };

            var createdGoal = await _goalRepository.CreateGoalAsync(goal);

            // Создаем прогресс за сегодня
            await CreateTodayProgressAsync(userId, createdGoal.Id);

            // Добавляем опыт за создание цели
            await _experienceService.AddExperienceAsync(userId, 50, "goal_created",
                $"Создана новая цель: {createdGoal.Title}");

            _logger.LogInformation($"Goal created for user {userId}: {createdGoal.GoalType}");

            return _mapper.Map<GoalDto>(createdGoal);
        }

        public async Task<GoalDto> UpdateGoalAsync(string userId, string goalId, UpdateGoalRequest request)
        {
            var goal = await _goalRepository.GetGoalByIdAsync(goalId);
            if (goal == null || goal.UserId != userId)
                throw new ArgumentException("Goal not found");

            // Обновляем поля
            if (!string.IsNullOrEmpty(request.Title))
                goal.Title = request.Title;
            if (!string.IsNullOrEmpty(request.Description))
                goal.Description = request.Description;

            if (request.TargetWeight.HasValue)
                goal.TargetWeight = request.TargetWeight;
            if (request.CurrentWeight.HasValue)
                goal.CurrentWeight = request.CurrentWeight;
            if (request.TargetCalories.HasValue)
                goal.TargetCalories = request.TargetCalories;
            if (request.TargetProtein.HasValue)
                goal.TargetProtein = request.TargetProtein;
            if (request.TargetCarbs.HasValue)
                goal.TargetCarbs = request.TargetCarbs;
            if (request.TargetFats.HasValue)
                goal.TargetFats = request.TargetFats;

            if (request.TargetWorkoutsPerWeek.HasValue)
                goal.TargetWorkoutsPerWeek = request.TargetWorkoutsPerWeek;
            if (request.TargetStepsPerDay.HasValue)
                goal.TargetStepsPerDay = request.TargetStepsPerDay;
            if (request.TargetActiveMinutes.HasValue)
                goal.TargetActiveMinutes = request.TargetActiveMinutes;

            if (request.EndDate.HasValue)
                goal.EndDate = request.EndDate;
            if (!string.IsNullOrEmpty(request.Status))
                goal.Status = request.Status;

            var updatedGoal = await _goalRepository.UpdateGoalAsync(goal);
            return _mapper.Map<GoalDto>(updatedGoal);
        }

        public async Task DeleteGoalAsync(string userId, string goalId)
        {
            var goal = await _goalRepository.GetGoalByIdAsync(goalId);
            if (goal == null || goal.UserId != userId)
                throw new ArgumentException("Goal not found");

            await _goalRepository.DeleteGoalAsync(goalId);
        }

        // Daily Progress
        public async Task<DailyGoalProgressDto?> GetTodayProgressAsync(string userId)
        {
            var activeGoal = await _goalRepository.GetActiveUserGoalAsync(userId);
            if (activeGoal == null) return null;

            return await GetTodayProgressForGoalAsync(userId, activeGoal.Id);
        }

        public async Task<IEnumerable<DailyGoalProgressDto>> GetProgressHistoryAsync(string userId, string goalId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var progressList = await _goalRepository.GetDailyProgressAsync(userId, goalId, startDate, endDate);
            return _mapper.Map<IEnumerable<DailyGoalProgressDto>>(progressList);
        }

        public async Task<DailyGoalProgressDto> UpdateDailyProgressAsync(string userId, UpdateDailyProgressRequest request)
        {
            var activeGoal = await _goalRepository.GetActiveUserGoalAsync(userId);
            if (activeGoal == null)
                throw new InvalidOperationException("No active goal found");

            var date = request.Date.Date;
            var progress = await _goalRepository.GetDailyProgressByDateAsync(userId, activeGoal.Id, date);

            if (progress == null)
            {
                progress = new DailyGoalProgress
                {
                    GoalId = activeGoal.Id,
                    UserId = userId,
                    Date = date
                };
            }

            // Обновляем мануальные значения, если они предоставлены
            if (request.ActualWeight.HasValue)
                progress.ActualWeight = request.ActualWeight;
            if (request.ManualCalories.HasValue)
                progress.ActualCalories = request.ManualCalories.Value;
            if (request.ManualProtein.HasValue)
                progress.ActualProtein = request.ManualProtein.Value;
            if (request.ManualCarbs.HasValue)
                progress.ActualCarbs = request.ManualCarbs.Value;
            if (request.ManualFats.HasValue)
                progress.ActualFats = request.ManualFats.Value;
            if (request.ManualSteps.HasValue)
                progress.ActualSteps = request.ManualSteps.Value;
            if (request.ManualWorkouts.HasValue)
                progress.ActualWorkouts = request.ManualWorkouts.Value;
            if (request.ManualActiveMinutes.HasValue)
                progress.ActualActiveMinutes = request.ManualActiveMinutes.Value;

            // Автоматически рассчитываем остальные значения
            await CalculateAutoValues(progress, userId, date);

            // Рассчитываем прогресс
            CalculateProgressPercentages(progress, activeGoal);

            // Сохраняем
            if (string.IsNullOrEmpty(progress.Id))
                progress = await _goalRepository.CreateDailyProgressAsync(progress);
            else
                progress = await _goalRepository.UpdateDailyProgressAsync(progress);

            return _mapper.Map<DailyGoalProgressDto>(progress);
        }

        public async Task RecalculateDailyProgressAsync(string userId, DateTime date)
        {
            var activeGoal = await _goalRepository.GetActiveUserGoalAsync(userId);
            if (activeGoal == null) return;

            var progress = await _goalRepository.GetDailyProgressByDateAsync(userId, activeGoal.Id, date);
            if (progress == null)
            {
                progress = new DailyGoalProgress
                {
                    GoalId = activeGoal.Id,
                    UserId = userId,
                    Date = date.Date
                };
            }

            // Автоматически рассчитываем все значения
            await CalculateAutoValues(progress, userId, date);
            CalculateProgressPercentages(progress, activeGoal);

            // Сохраняем
            if (string.IsNullOrEmpty(progress.Id))
                await _goalRepository.CreateDailyProgressAsync(progress);
            else
                await _goalRepository.UpdateDailyProgressAsync(progress);
        }

        // Templates
        public async Task<IEnumerable<GoalTemplateDto>> GetGoalTemplatesAsync()
        {
            return await Task.FromResult(new List<GoalTemplateDto>
            {
                new GoalTemplateDto
                {
                    GoalType = "weight_loss",
                    Title = "Похудение",
                    Description = "Снижение веса с помощью дефицита калорий и активности",
                    Icon = "🔥",
                    RecommendedCalories = 1800,
                    RecommendedProtein = 120,
                    RecommendedCarbs = 150,
                    RecommendedFats = 60,
                    RecommendedWorkoutsPerWeek = 4,
                    RecommendedStepsPerDay = 10000,
                    RecommendedActiveMinutes = 30,
                    Tips = new List<string>
                    {
                        "Создайте дефицит калорий 300-500 ккал в день",
                        "Увеличьте потребление белка для сохранения мышечной массы",
                        "Добавьте кардио тренировки 3-4 раза в неделю",
                        "Пейте больше воды и высыпайтесь"
                    }
                },
                new GoalTemplateDto
                {
                    GoalType = "weight_maintain",
                    Title = "Поддержание веса",
                    Description = "Сохранение текущего веса и улучшение композиции тела",
                    Icon = "⚖️",
                    RecommendedCalories = 2200,
                    RecommendedProtein = 100,
                    RecommendedCarbs = 220,
                    RecommendedFats = 80,
                    RecommendedWorkoutsPerWeek = 3,
                    RecommendedStepsPerDay = 8000,
                    RecommendedActiveMinutes = 25,
                    Tips = new List<string>
                    {
                        "Поддерживайте баланс калорий",
                        "Сосредоточьтесь на силовых тренировках",
                        "Следите за качеством пищи",
                        "Регулярно измеряйте композицию тела"
                    }
                },
                new GoalTemplateDto
                {
                    GoalType = "muscle_gain",
                    Title = "Набор мышечной массы",
                    Description = "Увеличение мышечной массы через профицит калорий и силовые тренировки",
                    Icon = "💪",
                    RecommendedCalories = 2800,
                    RecommendedProtein = 160,
                    RecommendedCarbs = 300,
                    RecommendedFats = 100,
                    RecommendedWorkoutsPerWeek = 5,
                    RecommendedStepsPerDay = 6000,
                    RecommendedActiveMinutes = 45,
                    Tips = new List<string>
                    {
                        "Создайте профицит калорий 200-400 ккал в день",
                        "Потребляйте 1.6-2.2г белка на кг веса",
                        "Фокусируйтесь на силовых тренировках",
                        "Обеспечьте достаточный отдых между тренировками"
                    }
                }
            });
        }

        public async Task<GoalTemplateDto?> GetGoalTemplateAsync(string goalType)
        {
            var templates = await GetGoalTemplatesAsync();
            return templates.FirstOrDefault(t => t.GoalType == goalType);
        }

        // Auto-calculation
        public async Task UpdateAllUserProgressAsync(string userId, DateTime date)
        {
            await RecalculateDailyProgressAsync(userId, date);
        }

        // Helper methods
        private async Task<DailyGoalProgressDto?> GetTodayProgressForGoalAsync(string userId, string goalId)
        {
            var today = DateTime.UtcNow.Date;
            var progress = await _goalRepository.GetDailyProgressByDateAsync(userId, goalId, today);

            if (progress == null)
            {
                // Создаем прогресс за сегодня если его нет
                await CreateTodayProgressAsync(userId, goalId);
                progress = await _goalRepository.GetDailyProgressByDateAsync(userId, goalId, today);
            }

            var progressDto = _mapper.Map<DailyGoalProgressDto>(progress);

            // Добавляем целевые значения для отображения
            var goal = await _goalRepository.GetGoalByIdAsync(goalId);
            if (goal != null)
            {
                progressDto.TargetCalories = goal.TargetCalories;
                progressDto.TargetProtein = goal.TargetProtein;
                progressDto.TargetCarbs = goal.TargetCarbs;
                progressDto.TargetFats = goal.TargetFats;
                progressDto.TargetSteps = goal.TargetStepsPerDay;
                progressDto.TargetWorkouts = CalculateTargetWorkoutsForDay(goal.TargetWorkoutsPerWeek);
            }

            return progressDto;
        }

        private async Task CreateTodayProgressAsync(string userId, string goalId)
        {
            var today = DateTime.UtcNow.Date;
            var existingProgress = await _goalRepository.GetDailyProgressByDateAsync(userId, goalId, today);

            if (existingProgress == null)
            {
                var progress = new DailyGoalProgress
                {
                    GoalId = goalId,
                    UserId = userId,
                    Date = today
                };

                await CalculateAutoValues(progress, userId, today);

                var goal = await _goalRepository.GetGoalByIdAsync(goalId);
                if (goal != null)
                {
                    CalculateProgressPercentages(progress, goal);
                }

                await _goalRepository.CreateDailyProgressAsync(progress);
            }
        }

        private async Task CalculateAutoValues(DailyGoalProgress progress, string userId, DateTime date)
        {
            // Рассчитываем калории и макросы из питания
            var foodIntakes = await _foodIntakeRepository.GetByUserIdAndDateAsync(userId, date);

            if (foodIntakes.Any())
            {
                progress.ActualCalories = (int)Math.Round(foodIntakes.Sum(f => (f.NutritionPer100g.Calories * f.Weight) / 100));
                progress.ActualProtein = Math.Round(foodIntakes.Sum(f => (f.NutritionPer100g.Proteins * f.Weight) / 100), 1);
                progress.ActualCarbs = Math.Round(foodIntakes.Sum(f => (f.NutritionPer100g.Carbs * f.Weight) / 100), 1);
                progress.ActualFats = Math.Round(foodIntakes.Sum(f => (f.NutritionPer100g.Fats * f.Weight) / 100), 1);
            }

            // Рассчитываем шаги
            var steps = await _stepsRepository.GetByUserIdAndDateAsync(userId, date);
            if (steps != null)
            {
                progress.ActualSteps = steps.StepsCount;
            }

            // Рассчитываем тренировки
            var activities = await _activityRepository.GetByUserIdAsync(userId);
            var dayActivities = activities.Where(a => a.StartDate.Date == date.Date);
            progress.ActualWorkouts = dayActivities.Count();

            // Рассчитываем активные минуты (приблизительно)
            progress.ActualActiveMinutes = dayActivities.Sum(a =>
            {
                var duration = (a.EndTime ?? a.StartTime.AddMinutes(30)) - a.StartTime;
                return (int)duration.TotalMinutes;
            });
        }

        private void CalculateProgressPercentages(DailyGoalProgress progress, Goal goal)
        {
            // Калории
            if (goal.TargetCalories.HasValue && goal.TargetCalories > 0)
                progress.CaloriesProgress = Math.Min(100, (decimal)progress.ActualCalories / goal.TargetCalories.Value * 100);

            // Белки
            if (goal.TargetProtein.HasValue && goal.TargetProtein > 0)
                progress.ProteinProgress = Math.Min(100, progress.ActualProtein / goal.TargetProtein.Value * 100);

            // Углеводы
            if (goal.TargetCarbs.HasValue && goal.TargetCarbs > 0)
                progress.CarbsProgress = Math.Min(100, progress.ActualCarbs / goal.TargetCarbs.Value * 100);

            // Жиры
            if (goal.TargetFats.HasValue && goal.TargetFats > 0)
                progress.FatsProgress = Math.Min(100, progress.ActualFats / goal.TargetFats.Value * 100);

            // Шаги
            if (goal.TargetStepsPerDay.HasValue && goal.TargetStepsPerDay > 0)
                progress.StepsProgress = Math.Min(100, (decimal)progress.ActualSteps / goal.TargetStepsPerDay.Value * 100);

            // Тренировки (дневная норма = недельная норма / 7)
            var dailyWorkoutTarget = CalculateTargetWorkoutsForDay(goal.TargetWorkoutsPerWeek);
            if (dailyWorkoutTarget > 0)
                progress.WorkoutProgress = Math.Min(100, (decimal)progress.ActualWorkouts / dailyWorkoutTarget * 100);

            // Общий прогресс (среднее арифметическое)
            var progressValues = new List<decimal>();
            if (goal.TargetCalories.HasValue) progressValues.Add(progress.CaloriesProgress);
            if (goal.TargetProtein.HasValue) progressValues.Add(progress.ProteinProgress);
            if (goal.TargetCarbs.HasValue) progressValues.Add(progress.CarbsProgress);
            if (goal.TargetFats.HasValue) progressValues.Add(progress.FatsProgress);
            if (goal.TargetStepsPerDay.HasValue) progressValues.Add(progress.StepsProgress);
            if (goal.TargetWorkoutsPerWeek.HasValue) progressValues.Add(progress.WorkoutProgress);

            progress.OverallProgress = progressValues.Any() ? Math.Round(progressValues.Average(), 1) : 0;
            progress.IsCompleted = progress.OverallProgress >= 80; // 80% считается выполненным
        }

        private async Task<(int TotalDays, int CompletedDays, decimal AverageProgress)> CalculateGoalStatsAsync(Goal goal)
        {
            var totalDays = (DateTime.UtcNow.Date - goal.StartDate.Date).Days + 1;
            var progressList = await _goalRepository.GetDailyProgressAsync(goal.UserId, goal.Id);

            var completedDays = progressList.Count(p => p.IsCompleted);
            var averageProgress = progressList.Any() ? Math.Round(progressList.Average(p => p.OverallProgress), 1) : 0;

            return (totalDays, completedDays, averageProgress);
        }

        private int CalculateTargetWorkoutsForDay(int? targetWorkoutsPerWeek)
        {
            if (!targetWorkoutsPerWeek.HasValue || targetWorkoutsPerWeek <= 0) return 0;

            // Если тренировок меньше 7 в неделю, то не каждый день нужно тренироваться
            return targetWorkoutsPerWeek.Value >= 7 ? 1 : 0;
        }

        private string GetDefaultTitle(string goalType)
        {
            return goalType switch
            {
                "weight_loss" => "Похудение",
                "weight_maintain" => "Поддержание веса",
                "muscle_gain" => "Набор мышечной массы",
                _ => "Моя цель"
            };
        }
    }
}