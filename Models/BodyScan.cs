namespace FitnessTracker.API.Models
{
    public class BodyScan
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public string FrontImageUrl { get; set; } = string.Empty;
        public string SideImageUrl { get; set; } = string.Empty;
        public string? BackImageUrl { get; set; }
        public decimal Weight { get; set; }
        public decimal? BodyFatPercentage { get; set; }
        public decimal? MusclePercentage { get; set; }
        public decimal? WaistCircumference { get; set; }
        public decimal? ChestCircumference { get; set; }
        public decimal? HipCircumference { get; set; }
        public string? Notes { get; set; }
        public DateTime ScanDate { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int? BasalMetabolicRate { get; set; } 
        public string? MetabolicRateCategory { get; set; } 

        // Navigation property
        public User User { get; set; } = null!;
    }
}