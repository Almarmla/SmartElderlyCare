namespace SmartElderlyCare.Models;

public class VhwWelfareCheck
{
    public long Id { get; set; }

    public int PatientId { get; set; }

    public Patient Patient { get; set; } = null!;

    public string RecordedByUserId { get; set; } = string.Empty;

    public ApplicationUser RecordedByUser { get; set; } = null!;

    public DateTimeOffset ObservedAt { get; set; } = DateTimeOffset.UtcNow;

    public VhwWelfareStatus Status { get; set; } = VhwWelfareStatus.Stable;

    public bool MedicationTaken { get; set; }

    public string? Mood { get; set; }

    public string? Mobility { get; set; }

    public string? Nutrition { get; set; }

    public string? SafetyConcern { get; set; }

    public string? Notes { get; set; }

    public bool FollowUpRequired { get; set; }

    public DateTimeOffset? FollowUpAt { get; set; }
}

public enum VhwWelfareStatus
{
    Stable,
    NeedsAttention,
    Urgent
}
