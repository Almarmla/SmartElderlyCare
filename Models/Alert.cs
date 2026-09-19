namespace SmartElderlyCare.Models;

public class Alert
{
    public long Id { get; set; }

    public int PatientId { get; set; }

    public Patient Patient { get; set; } = null!;

    public int? ThresholdId { get; set; }

    public Threshold? Threshold { get; set; }

    public string? AssignedToUserId { get; set; }

    public ApplicationUser? AssignedToUser { get; set; }

    public AlertType Type { get; set; }

    public AlertSeverity Severity { get; set; } = AlertSeverity.Medium;

    public AlertStatus Status { get; set; } = AlertStatus.Open;

    public bool Reviewed { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public string? ReviewedByUserId { get; set; }

    public string Message { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? AcknowledgedAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }
}

public enum AlertType
{
    VitalSigns,
    Welfare,
    MissedCheckIn,
    Medication,
    Other
}

public enum AlertSeverity
{
    Low,
    Medium,
    High,
    Critical
}

public enum AlertStatus
{
    Open,
    Acknowledged,
    Resolved
}
