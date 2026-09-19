namespace SmartElderlyCare.Models;

public class MonthlyReturnValidation
{
    public long Id { get; set; }

    public long MonthlyReturnId { get; set; }

    public MonthlyReturn MonthlyReturn { get; set; } = null!;

    public string ValidatedByUserId { get; set; } = string.Empty;

    public ApplicationUser ValidatedByUser { get; set; } = null!;

    public DateTimeOffset ValidatedAt { get; set; } = DateTimeOffset.UtcNow;

    public MonthlyReturnValidationStatus Status { get; set; } = MonthlyReturnValidationStatus.Pending;

    public string? Notes { get; set; }
}

public enum MonthlyReturnValidationStatus
{
    Pending,
    Valid,
    Rejected
}
