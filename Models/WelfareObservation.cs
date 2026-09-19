namespace SmartElderlyCare.Models;

public class WelfareObservation
{
    public long Id { get; set; }

    public int PatientId { get; set; }

    public Patient Patient { get; set; } = null!;

    public string? RecordedByUserId { get; set; }

    public ApplicationUser? RecordedByUser { get; set; }

    public DateTimeOffset ObservedAt { get; set; } = DateTimeOffset.UtcNow;

    public WelfareStatus Status { get; set; } = WelfareStatus.Stable;

    public string? Mood { get; set; }

    public string? Mobility { get; set; }

    public string? NutritionAndHydration { get; set; }

    public string? SocialEngagement { get; set; }

    public string? Notes { get; set; }
}

public enum WelfareStatus
{
    Stable,
    NeedsAttention,
    Urgent
}
