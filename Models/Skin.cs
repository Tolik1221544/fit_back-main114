namespace FitnessTracker.API.Models
{
    public class Skin
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public int Cost { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        
        public decimal ExperienceBoost { get; set; } = 1.0m; // Множитель опыта (1.0 = без бонуса)
        public int Tier { get; set; } = 1; // Уровень скина (1, 2, 3)

        // Navigation properties
        public ICollection<UserSkin> UserSkins { get; set; } = new List<UserSkin>();
    }

    public class UserSkin
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public string SkinId { get; set; } = string.Empty;
        public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = false;

        // Navigation properties
        public User User { get; set; } = null!;
        public Skin Skin { get; set; } = null!;
    }
}