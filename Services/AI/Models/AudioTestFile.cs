public class AudioTestFile
{
    public string Id { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string UploadedBy { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public long FileSize { get; set; }
    public string? ContentType { get; set; }
}

public class TestAudioRequest
{
    public string TestType { get; set; } = "workout"; // "workout" или "food"
    public string? Description { get; set; }
}