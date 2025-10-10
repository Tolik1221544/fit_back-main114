using Microsoft.EntityFrameworkCore;
using FitnessTracker.API.Models;

namespace FitnessTracker.API.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<FoodIntake> FoodIntakes { get; set; }
        public DbSet<Activity> Activities { get; set; }
        public DbSet<Steps> Steps { get; set; }
        public DbSet<LwCoinTransaction> LwCoinTransactions { get; set; }
        public DbSet<ExperienceTransaction> ExperienceTransactions { get; set; }
        public DbSet<BodyScan> BodyScans { get; set; }
        public DbSet<Skin> Skins { get; set; }
        public DbSet<UserSkin> UserSkins { get; set; }
        public DbSet<Mission> Missions { get; set; }
        public DbSet<UserMission> UserMissions { get; set; }
        public DbSet<Achievement> Achievements { get; set; }
        public DbSet<UserAchievement> UserAchievements { get; set; }
        public DbSet<Referral> Referrals { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }
        public DbSet<Goal> Goals { get; set; }
        public DbSet<DailyGoalProgress> DailyGoalProgress { get; set; }
        public DbSet<PendingPayment> PendingPayments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.ReferralCode).IsUnique();
                entity.HasIndex(e => e.TelegramId).IsUnique();

                entity.Property(e => e.Weight).HasPrecision(5, 2);
                entity.Property(e => e.Height).HasPrecision(5, 2);

                entity.Property(e => e.FractionalLwCoins).HasPrecision(10, 2);
            });

            // FoodIntake entity configuration
            modelBuilder.Entity<FoodIntake>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.User)
                    .WithMany(u => u.FoodIntakes)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.OwnsOne(e => e.NutritionPer100g, n =>
                {
                    n.Property(p => p.Calories).HasPrecision(8, 2);
                    n.Property(p => p.Proteins).HasPrecision(8, 2);
                    n.Property(p => p.Fats).HasPrecision(8, 2);
                    n.Property(p => p.Carbs).HasPrecision(8, 2);
                });

                entity.Property(e => e.Weight).HasPrecision(8, 2);
            });

            modelBuilder.Entity<Activity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.User)
                    .WithMany(u => u.Activities)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.ActivityDataJson).HasColumnName("ActivityData");
                entity.Ignore(e => e.ActivityData);

                entity.Property(e => e.StartDate).IsRequired();
                entity.Property(e => e.EndDate).IsRequired(false);

            });

            // Steps entity configuration
            modelBuilder.Entity<Steps>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<LwCoinTransaction>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.User)
                    .WithMany(u => u.LwCoinTransactions)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.FractionalAmount).HasPrecision(10, 2);
                entity.Property(e => e.Price).HasPrecision(10, 2);

                entity.HasIndex(e => new { e.UserId, e.UsageDate });
                entity.HasIndex(e => new { e.FeatureUsed, e.CreatedAt });
                entity.HasIndex(e => e.FractionalAmount);
            });

            // ExperienceTransaction entity configuration
            modelBuilder.Entity<ExperienceTransaction>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // BodyScan entity configuration  
            modelBuilder.Entity<BodyScan>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Skin entity configuration
            modelBuilder.Entity<Skin>(entity =>
            {
                entity.HasKey(e => e.Id);
            });

            // UserSkin entity configuration
            modelBuilder.Entity<UserSkin>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.User)
                    .WithMany(u => u.UserSkins)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Skin)
                    .WithMany(s => s.UserSkins)
                    .HasForeignKey(e => e.SkinId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Mission entity configuration
            modelBuilder.Entity<Mission>(entity =>
            {
                entity.HasKey(e => e.Id);
            });

            // UserMission entity configuration
            modelBuilder.Entity<UserMission>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.User)
                    .WithMany(u => u.UserMissions)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Mission)
                    .WithMany(m => m.UserMissions)
                    .HasForeignKey(e => e.MissionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Achievement entity configuration
            modelBuilder.Entity<Achievement>(entity =>
            {
                entity.HasKey(e => e.Id);
            });

            // UserAchievement entity configuration
            modelBuilder.Entity<UserAchievement>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Achievement)
                    .WithMany(a => a.UserAchievements)
                    .HasForeignKey(e => e.AchievementId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Referral entity configuration
            modelBuilder.Entity<Referral>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Referrer)
                    .WithMany(u => u.Referrals)
                    .HasForeignKey(e => e.ReferrerId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => e.ReferralCode).IsUnique();
            });

            modelBuilder.Entity<Subscription>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.User)
                    .WithMany(u => u.Subscriptions)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.Property(e => e.Price).HasPrecision(10, 2);
            });

            modelBuilder.Entity<Goal>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.TargetWeight).HasPrecision(5, 2);
                entity.Property(e => e.CurrentWeight).HasPrecision(5, 2);
                entity.Property(e => e.ProgressPercentage).HasPrecision(5, 2);

                entity.HasIndex(e => new { e.UserId, e.IsActive });
                entity.HasIndex(e => e.GoalType);
            });

            modelBuilder.Entity<DailyGoalProgress>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Goal)
                    .WithMany(g => g.DailyProgress)
                    .HasForeignKey(e => e.GoalId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.ActualProtein).HasPrecision(8, 2);
                entity.Property(e => e.ActualCarbs).HasPrecision(8, 2);
                entity.Property(e => e.ActualFats).HasPrecision(8, 2);
                entity.Property(e => e.ActualWeight).HasPrecision(5, 2);

                entity.Property(e => e.CaloriesProgress).HasPrecision(5, 2);
                entity.Property(e => e.ProteinProgress).HasPrecision(5, 2);
                entity.Property(e => e.CarbsProgress).HasPrecision(5, 2);
                entity.Property(e => e.FatsProgress).HasPrecision(5, 2);
                entity.Property(e => e.StepsProgress).HasPrecision(5, 2);
                entity.Property(e => e.WorkoutProgress).HasPrecision(5, 2);
                entity.Property(e => e.OverallProgress).HasPrecision(5, 2);

                entity.HasIndex(e => new { e.UserId, e.GoalId, e.Date }).IsUnique();
                entity.HasIndex(e => e.Date);
            });

            modelBuilder.Entity<PendingPayment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.PaymentId).IsUnique();
                entity.HasIndex(e => new { e.Status, e.CreatedAt });
                entity.HasIndex(e => e.TelegramId);
                entity.Property(e => e.Amount).HasPrecision(10, 2);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Mission>().HasData(
                new Mission
                {
                    Id = "mission_breakfast_500",
                    Title = "Съешь 500ккал на завтрак",
                    Icon = "🔥",
                    RewardExperience = 100,
                    Type = "breakfast_calories",
                    TargetValue = 500,
                    Route = null,
                    IsActive = true
                },
                new Mission
                {
                    Id = "mission_walk_5000",
                    Title = "Пройди 5000 шагов",
                    Icon = "🚶‍♂️",
                    RewardExperience = 50,
                    Type = "daily_steps",
                    TargetValue = 5000,
                    Route = null,
                    IsActive = true
                },
                new Mission
                {
                    Id = "mission_body_scan_weekly",
                    Title = "Скан тела каждую неделю",
                    Icon = "💪",
                    RewardExperience = 100,
                    Type = "weekly_body_scan",
                    TargetValue = 1,
                    Route = "/body_analyze",
                    IsActive = true
                },
                new Mission
                {
                    Id = "mission_daily_goal_80",
                    Title = "Выполни дневную цель на 80%",
                    Icon = "🎯",
                    RewardExperience = 75,
                    Type = "daily_goal_progress",
                    TargetValue = 80,
                    Route = "/goals",
                    IsActive = true
                },
                new Mission
                {
                    Id = "mission_weekly_goal_streak",
                    Title = "Неделя выполнения целей",
                    Icon = "🔥",
                    RewardExperience = 200,
                    Type = "weekly_goal_streak",
                    TargetValue = 7,
                    Route = "/goals",
                    IsActive = true
                },
                new Mission
                {
                    Id = "mission_smart_spending",
                    Title = "Умная трата: уложись в дневной лимит",
                    Icon = "💰",
                    RewardExperience = 50,
                    Type = "daily_limit_compliance",
                    TargetValue = 1,
                    Route = "/lw-coin",
                    IsActive = true
                },
                new Mission
                {
                    Id = "mission_photo_master",
                    Title = "Мастер фото: проанализируй 3 фото за день",
                    Icon = "📸",
                    RewardExperience = 75,
                    Type = "daily_photo_scans",
                    TargetValue = 3,
                    Route = "/ai/scan-food",
                    IsActive = true
                }
            );

            modelBuilder.Entity<Skin>().HasData(
                new Skin
                {
                    Id = "skin_minimalist",
                    Name = "Минималист",
                    Cost = 150,
                    ImageUrl = "https://example.com/skins/minimalist.png",
                    Description = "Для пользователей, которые тратят меньше 5 монет в день. Учит ценить каждую монету.",
                    ExperienceBoost = 1.1m, // 10% буст опыта
                    Tier = 1
                },
                new Skin
                {
                    Id = "skin_economist",
                    Name = "Экономист",
                    Cost = 300,
                    ImageUrl = "https://example.com/skins/economist.png",
                    Description = "Для тех, кто умеет экономить монеты и планировать траты разумно",
                    ExperienceBoost = 1.2m, // 20% буст опыта
                    Tier = 1
                },
                new Skin
                {
                    Id = "skin_investor",
                    Name = "Инвестор",
                    Cost = 500,
                    ImageUrl = "https://example.com/skins/investor.png",
                    Description = "Для тех, кто накопил значительную сумму и думает стратегически",
                    ExperienceBoost = 1.3m, // 30% буст опыта
                    Tier = 2
                },
                new Skin
                {
                    Id = "skin_strategist",
                    Name = "Стратег",
                    Cost = 1000,
                    ImageUrl = "https://example.com/skins/strategist.png",
                    Description = "Мастер планирования и долгосрочных целей. Венец экономической мудрости.",
                    ExperienceBoost = 1.5m, // 50% буст опыта
                    Tier = 3
                }
            );

            modelBuilder.Entity<Achievement>().HasData(
                new Achievement
                {
                    Id = "achievement_first_workout",
                    Title = "Первая тренировка",
                    Icon = "⭐",
                    ImageUrl = "https://example.com/achievements/first-workout.png",
                    Type = "activity_count",
                    RequiredValue = 1,
                    RewardExperience = 100
                },
                new Achievement
                {
                    Id = "achievement_workout_week",
                    Title = "Неделя тренировок",
                    Icon = "🏃‍♂️",
                    ImageUrl = "https://example.com/achievements/workout-week.png",
                    Type = "activity_count",
                    RequiredValue = 7,
                    RewardExperience = 200
                },
                new Achievement
                {
                    Id = "achievement_nutrition_master",
                    Title = "Мастер питания",
                    Icon = "🥗",
                    ImageUrl = "https://example.com/achievements/nutrition-master.png",
                    Type = "food_count",
                    RequiredValue = 50,
                    RewardExperience = 300
                },
                new Achievement
                {
                    Id = "achievement_body_analyzer",
                    Title = "Аналитик тела",
                    Icon = "📊",
                    ImageUrl = "https://example.com/achievements/body-analyzer.png",
                    Type = "body_scan_count",
                    RequiredValue = 3,
                    RewardExperience = 250
                },
                new Achievement
                {
                    Id = "achievement_referral_master",
                    Title = "Мастер рефералов",
                    Icon = "👑",
                    ImageUrl = "https://example.com/achievements/referral-master.png",
                    Type = "referral_count",
                    RequiredValue = 10,
                    RewardExperience = 500
                },
                new Achievement
                {
                    Id = "achievement_goal_setter",
                    Title = "Постановщик целей",
                    Icon = "🎯",
                    ImageUrl = "https://example.com/achievements/goal-setter.png",
                    Type = "goal_count",
                    RequiredValue = 1,
                    RewardExperience = 150
                },
                new Achievement
                {
                    Id = "achievement_goal_achiever",
                    Title = "Достигатор целей",
                    Icon = "🏆",
                    ImageUrl = "https://example.com/achievements/goal-achiever.png",
                    Type = "completed_goal_count",
                    RequiredValue = 1,
                    RewardExperience = 300
                },
                new Achievement
                {
                    Id = "achievement_consistency_master",
                    Title = "Мастер постоянства",
                    Icon = "🔥",
                    ImageUrl = "https://example.com/achievements/consistency-master.png",
                    Type = "goal_streak_days",
                    RequiredValue = 30,
                    RewardExperience = 500
                },
                new Achievement
                {
                    Id = "achievement_budget_master",
                    Title = "Мастер бюджета",
                    Icon = "💎",
                    ImageUrl = "https://example.com/achievements/budget-master.png",
                    Type = "daily_limit_streaks",
                    RequiredValue = 7,
                    RewardExperience = 200
                },
                new Achievement
                {
                    Id = "achievement_photo_expert",
                    Title = "Эксперт фотоанализа",
                    Icon = "📷",
                    ImageUrl = "https://example.com/achievements/photo-expert.png",
                    Type = "photo_scan_count",
                    RequiredValue = 100,
                    RewardExperience = 400
                }
            );
        }
    }
}