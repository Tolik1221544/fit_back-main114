using Microsoft.EntityFrameworkCore;

namespace FitnessTracker.API.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<FoodIntake> FoodIntakes { get; set; }
        public DbSet<Activity> Activities { get; set; }
        public DbSet<Steps> Steps { get; set; } 
        public DbSet<CoinTransaction> CoinTransactions { get; set; }
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

            // Activity entity configuration
            modelBuilder.Entity<Activity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.User)
                    .WithMany(u => u.Activities)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Ignore(e => e.StrengthData);
                entity.Ignore(e => e.CardioData);
            });

            // CoinTransaction entity configuration
            modelBuilder.Entity<CoinTransaction>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.User)
                    .WithMany(u => u.CoinTransactions)
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

            // Seed default missions
            modelBuilder.Entity<Mission>().HasData(
                new Mission
                {
                    Id = "mission1",
                    Title = "Первые шаги",
                    Icon = "🏃‍♂️",
                    RewardExperience = 50,
                    Type = "activity",
                    TargetValue = 1
                },
                new Mission
                {
                    Id = "mission2",
                    Title = "Записать питание",
                    Icon = "🍎",
                    RewardExperience = 30,
                    Type = "food_intake",
                    TargetValue = 5
                },
                new Mission
                {
                    Id = "mission3",
                    Title = "Недельный воин",
                    Icon = "💪",
                    RewardExperience = 100,
                    Type = "activity",
                    TargetValue = 7
                }
            );

            // Seed default achievements
            modelBuilder.Entity<Achievement>().HasData(
                new Achievement
                {
                    Id = "achievement1",
                    Title = "Первые шаги",
                    Icon = "⭐",
                    ImageUrl = "https://example.com/achievements/first-steps.png",
                    Type = "activity_count",
                    RequiredValue = 1,
                    RewardExperience = 100
                },
                new Achievement
                {
                    Id = "achievement2",
                    Title = "Активный новичок",
                    Icon = "🏃‍♂️",
                    ImageUrl = "https://example.com/achievements/active-beginner.png",
                    Type = "activity_count",
                    RequiredValue = 10,
                    RewardExperience = 200
                },
                new Achievement
                {
                    Id = "achievement3",
                    Title = "Мастер питания",
                    Icon = "🥗",
                    ImageUrl = "https://example.com/achievements/nutrition-master.png",
                    Type = "food_count",
                    RequiredValue = 50,
                    RewardExperience = 300
                },
                new Achievement
                {
                    Id = "achievement4",
                    Title = "Опытный атлет",
                    Icon = "💪",
                    ImageUrl = "https://example.com/achievements/experienced-athlete.png",
                    Type = "activity_count",
                    RequiredValue = 100,
                    RewardExperience = 500
                },
                new Achievement
                {
                    Id = "achievement5",
                    Title = "Достиг 5-го уровня",
                    Icon = "🎖️",
                    ImageUrl = "https://example.com/achievements/level-5.png",
                    Type = "level",
                    RequiredValue = 5,
                    RewardExperience = 250
                },
                new Achievement
                {
                    Id = "achievement6",
                    Title = "Амбассадор",
                    Icon = "👥",
                    ImageUrl = "https://example.com/achievements/ambassador.png",
                    Type = "referral_count",
                    RequiredValue = 5,
                    RewardExperience = 400
                }
            );

            // Seed default skins
            modelBuilder.Entity<Skin>().HasData(
                new Skin
                {
                    Id = "skin1",
                    Name = "Classic Blue",
                    Cost = 50,
                    ImageUrl = "https://example.com/skins/classic-blue.png",
                    Description = "Classic blue theme for your tracker"
                },
                new Skin
                {
                    Id = "skin2",
                    Name = "Forest Green",
                    Cost = 75,
                    ImageUrl = "https://example.com/skins/forest-green.png",
                    Description = "Nature-inspired green theme"
                },
                new Skin
                {
                    Id = "skin3",
                    Name = "Premium Gold",
                    Cost = 150,
                    ImageUrl = "https://example.com/skins/premium-gold.png",
                    Description = "Luxury gold theme for premium users"
                }
            );
        }
    }
}