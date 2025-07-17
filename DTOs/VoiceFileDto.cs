namespace FitnessTracker.API.DTOs
{
    public class VoiceFileDto
    {
        public string FileId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public string VoiceType { get; set; } = string.Empty; // "workout", "food"

        public string UserId { get; set; } = string.Empty;

        public long SizeBytes { get; set; }
        public double SizeMB { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Extension { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;

        public string? TranscribedText { get; set; }
        public string? AnalysisResult { get; set; }
        public bool IsAnalyzed { get; set; }
    }

    public class VoiceFilesStatsDto
    {
        public int TotalFiles { get; set; }
        public int WorkoutFiles { get; set; }
        public int FoodFiles { get; set; }
        public double TotalSizeMB { get; set; }
        public double AverageFileSizeMB { get; set; }
        public DateTime? OldestFileDate { get; set; }
        public DateTime? NewestFileDate { get; set; }
        public int FilesThisMonth { get; set; }
        public int FilesToday { get; set; }
    }

    public class VoiceFilesSearchRequest
    {
        public string? VoiceType { get; set; } // "workout", "food"
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string SortBy { get; set; } = "created_desc"; // "created_desc", "size_desc", "name_asc"
    }

    public class VoiceWorkoutResponseWithAudio
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? TranscribedText { get; set; }
        public WorkoutDataResponse? WorkoutData { get; set; }

        public string? AudioUrl { get; set; }
        public string? AudioFileId { get; set; }
        public bool AudioSaved { get; set; }
        public double AudioSizeMB { get; set; }
    }

    public class VoiceFoodResponseWithAudio
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? TranscribedText { get; set; }
        public List<FoodItemResponse>? FoodItems { get; set; }
        public int EstimatedTotalCalories { get; set; }

        public string? AudioUrl { get; set; }
        public string? AudioFileId { get; set; }
        public bool AudioSaved { get; set; }
        public double AudioSizeMB { get; set; }
    }
}