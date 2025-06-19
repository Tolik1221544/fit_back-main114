namespace FitnessTracker.API.DTOs
{
    public class AddActivityRequest
    {
        public string Mode { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }

    public class ActivityDto
    {
        public string Id { get; set; } = string.Empty;
        public string Mode { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class UpdateActivityRequest
    {
        public string Mode { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }
}
