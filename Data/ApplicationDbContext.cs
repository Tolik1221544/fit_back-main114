using FitnessTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace FitnessTracker.API.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<FoodIntake> FoodIntakes { get; set; }
        public DbSet<Activity> Activities { get; set; }
        public DbSet<Skin> Skins { get; set; }
        public DbSet<UserSkin> UserSkins { get; set; }
        public DbSet<Mission> Missions { get; set; }
        public DbSet<UserMission> UserMissions { get; set; }
        public DbSet<Achievement> Achievements { get; set; }
        public DbSet<CoinTransaction> CoinTransactions { get; set; }
        public DbSet<Referral> Referrals { get; set; }

        // Новые таблицы для LW Coin системы
        public DbSet<LwCoinTransaction> LwCoinTransactions { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
                entity.Property(e => e.PasswordHash).IsRequired();
                entity.Property(e => e.Gender).HasMaxLength(10);
                entity.Property(e => e.Weight).HasColumnType("decimal(5,2)");
                entity.Property(e => e.Height).HasColumnType("decimal(5,2)");
                entity.HasIndex(e => e.Email).IsUnique();
            });

            // FoodIntake configuration
            modelBuilder.Entity<FoodIntake>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Weight).HasColumnType("decimal(8,2)");
                entity.OwnsOne(e => e.NutritionPer100g, n =>
                {
                    n.Property(p => p.Calories).HasColumnType("decimal(8,2)");
                    n.Property(p => p.Proteins).HasColumnType("decimal(8,2)");
                    n.Property(p => p.Fats).HasColumnType("decimal(8,2)");
                    n.Property(p => p.Carbs).HasColumnType("decimal(8,2)");
                });
                entity.HasOne(e => e.User)
                      .WithMany(u => u.FoodIntakes)
                      .HasForeignKey(e => e.UserId);
            });

            // Activity configuration
            modelBuilder.Entity<Activity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Type).IsRequired().HasMaxLength(20);
                entity.Property(e => e.StrengthDataJson).HasColumnType("TEXT");
                entity.Property(e => e.CardioDataJson).HasColumnType("TEXT");
                entity.HasOne(e => e.User)
                      .WithMany(u => u.Activities)
                      .HasForeignKey(e => e.UserId);
            });

            // Skin configuration
            modelBuilder.Entity<Skin>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
                entity.Property(e => e.ImageUrl).HasMaxLength(500);
                entity.Property(e => e.Description).HasMaxLength(1000);
            });

            // UserSkin configuration
            modelBuilder.Entity<UserSkin>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.User)
                      .WithMany(u => u.UserSkins)
                      .HasForeignKey(e => e.UserId);
                entity.HasOne(e => e.Skin)
                      .WithMany(s => s.UserSkins)
                      .HasForeignKey(e => e.SkinId);
            });

            // Mission configuration
            modelBuilder.Entity<Mission>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.Property(e => e.Type).IsRequired().HasMaxLength(100);
            });

            // UserMission configuration
            modelBuilder.Entity<UserMission>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.User)
                      .WithMany(u => u.UserMissions)
                      .HasForeignKey(e => e.UserId);
                entity.HasOne(e => e.Mission)
                      .WithMany(m => m.UserMissions)
                      .HasForeignKey(e => e.MissionId);
            });

            // CoinTransaction configuration
            modelBuilder.Entity<CoinTransaction>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Type).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.HasOne(e => e.User)
                      .WithMany(u => u.CoinTransactions)
                      .HasForeignKey(e => e.UserId);
            });

            // LwCoinTransaction configuration
            modelBuilder.Entity<LwCoinTransaction>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Type).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.FeatureUsed).HasMaxLength(100);
                entity.HasOne(e => e.User)
                      .WithMany(u => u.LwCoinTransactions)
                      .HasForeignKey(e => e.UserId);
            });

            // Subscription configuration
            modelBuilder.Entity<Subscription>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Type).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Price).HasColumnType("decimal(10,2)");
                entity.Property(e => e.Currency).HasMaxLength(10);
                entity.Property(e => e.PaymentTransactionId).HasMaxLength(255);
                entity.HasOne(e => e.User)
                      .WithMany(u => u.Subscriptions)
                      .HasForeignKey(e => e.UserId);
            });

            // Referral configuration
            modelBuilder.Entity<Referral>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ReferralCode).IsRequired().HasMaxLength(50);
                entity.HasOne(e => e.Referrer)
                      .WithMany(u => u.Referrals)
                      .HasForeignKey(e => e.ReferrerId);
                entity.HasIndex(e => e.ReferralCode).IsUnique();
            });

            // Achievement configuration
            modelBuilder.Entity<Achievement>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.Property(e => e.IconUrl).HasMaxLength(500);
            });

            // Seed data
            SeedData(modelBuilder);
        }

        private void SeedData(ModelBuilder modelBuilder)
        {
            // Seed default skins
            modelBuilder.Entity<Skin>().HasData(
                new Skin { Id = "skin1", Name = "Default Avatar", Cost = 0, ImageUrl = "/images/skins/default.png", Description = "Default character skin" },
                new Skin { Id = "skin2", Name = "Athlete", Cost = 100, ImageUrl = "/images/skins/athlete.png", Description = "Professional athlete skin" },
                new Skin { Id = "skin3", Name = "Ninja", Cost = 200, ImageUrl = "/images/skins/ninja.png", Description = "Stealthy ninja skin" }
            );

            // Seed default missions
            modelBuilder.Entity<Mission>().HasData(
                new Mission { Id = "mission1", Title = "First Steps", Description = "Log your first meal", RewardCoins = 10, Type = "food_intake", TargetValue = 1 },
                new Mission { Id = "mission2", Title = "Daily Nutrition", Description = "Log 3 meals in a day", RewardCoins = 25, Type = "food_intake", TargetValue = 3 },
                new Mission { Id = "mission3", Title = "Workout Warrior", Description = "Complete 5 workouts", RewardCoins = 50, Type = "activity", TargetValue = 5 }
            );
        }
    }
}