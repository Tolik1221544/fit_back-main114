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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User entity configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.ReferralCode).IsUnique();
                entity.Property(e => e.Weight).HasPrecision(5, 2);
                entity.Property(e => e.Height).HasPrecision(5, 2);
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

                entity.Property(e => e.StrengthDataJson).HasColumnName("StrengthData");
                entity.Property(e => e.CardioDataJson).HasColumnName("CardioData");

                entity.Ignore(e => e.StrengthData);
                entity.Ignore(e => e.CardioData);

                entity.Property(e => e.StartTime).IsRequired();
                entity.Property(e => e.EndTime).IsRequired(false);
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

            // LwCoinTransaction entity configuration
            modelBuilder.Entity<LwCoinTransaction>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.User)
                    .WithMany(u => u.LwCoinTransactions)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
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

            // Subscription entity configuration
            modelBuilder.Entity<Subscription>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.User)
                    .WithMany(u => u.Subscriptions)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.Property(e => e.Price).HasPrecision(10, 2);
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
                }
            );

            modelBuilder.Entity<Skin>().HasData(
                new Skin
                {
                    Id = "skin_athlete",
                    Name = "Атлет",
                    Cost = 200,
                    ImageUrl = "https://example.com/skins/athlete.png",
                    Description = "Скин для настоящих спортсменов"
                },
                new Skin
                {
                    Id = "skin_machine",
                    Name = "Машина",
                    Cost = 500,
                    ImageUrl = "https://example.com/skins/machine.png",
                    Description = "Скин для тех, кто работает как машина"
                },
                new Skin
                {
                    Id = "skin_superhuman",
                    Name = "Сверхчеловек",
                    Cost = 2000,
                    ImageUrl = "https://example.com/skins/superhuman.png",
                    Description = "Скин для сверхлюдей"
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
                }
            );
        }
    }
}