namespace SmartElderlyCare.Models;

public class AdministrationAuditLog
{
    public long Id { get; set; }

    public string PerformedByUserId { get; set; } = string.Empty;

    public ApplicationUser PerformedByUser { get; set; } = null!;

    public AdministrationAction Action { get; set; }

    public string EntityName { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public int? ThresholdId { get; set; }

    public Threshold? Threshold { get; set; }

    public string? AffectedUserId { get; set; }

    public ApplicationUser? AffectedUser { get; set; }

    public string? Details { get; set; }

    public DateTimeOffset PerformedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum AdministrationAction
{
    UserCreated,
    UserUpdated,
    UserDisabled,
    UserUnlocked,
    PasswordReset,
    RoleChanged,
    ThresholdCreated,
    ThresholdUpdated,
    ThresholdDisabled
}
