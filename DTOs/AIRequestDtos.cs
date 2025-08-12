public class TextWorkoutRequest
{
    public string WorkoutDescription { get; set; } = string.Empty;
    public string? WorkoutType { get; set; }
    public bool SaveResults { get; set; } = false;
}

public class TextWorkoutResponse
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string ProcessedText { get; set; } = string.Empty;
    public ActivityDto? WorkoutData { get; set; } 
}

public class VoiceWorkoutResponse
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? TranscribedText { get; set; }
    public ActivityDto? WorkoutData { get; set; } 
}

public class VoiceWorkoutResponseWithAudio : VoiceWorkoutResponse
{
    public string? AudioUrl { get; set; }
    public string? AudioFileId { get; set; }
    public bool AudioSaved { get; set; }
    public double AudioSizeMB { get; set; }
}

public class VoiceFoodResponseWithAudio : VoiceFoodResponse
{
    public string? AudioUrl { get; set; }
    public string? AudioFileId { get; set; }
    public bool AudioSaved { get; set; }
    public double AudioSizeMB { get; set; }
}