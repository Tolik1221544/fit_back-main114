namespace FitnessTracker.API.DTOs
{
    public class AddBodyScanRequest
    {
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
    }

    public class BodyScanDto
    {
        public string Id { get; set; } = string.Empty;
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
        public DateTime ScanDate { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class UpdateBodyScanRequest
    {
        public string? FrontImageUrl { get; set; }
        public string? SideImageUrl { get; set; }
        public string? BackImageUrl { get; set; }
        public decimal? Weight { get; set; }
        public decimal? BodyFatPercentage { get; set; }
        public decimal? MusclePercentage { get; set; }
        public decimal? WaistCircumference { get; set; }
        public decimal? ChestCircumference { get; set; }
        public decimal? HipCircumference { get; set; }
        public string? Notes { get; set; }
    }

    public class BodyScanComparisonDto
    {
        public BodyScanDto? PreviousScan { get; set; }
        public BodyScanDto CurrentScan { get; set; } = new BodyScanDto();
        public BodyScanProgressDto Progress { get; set; } = new BodyScanProgressDto();
    }

    public class BodyScanProgressDto
    {
        public decimal WeightDifference { get; set; }
        public decimal? BodyFatDifference { get; set; }
        public decimal? MuscleDifference { get; set; }
        public decimal? WaistDifference { get; set; }
        public decimal? ChestDifference { get; set; }
        public decimal? HipDifference { get; set; }
        public int DaysBetweenScans { get; set; }
    }
}