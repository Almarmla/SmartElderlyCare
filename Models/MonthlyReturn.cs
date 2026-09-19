namespace SmartElderlyCare.Models;

public class MonthlyReturn
{
    public long Id { get; set; }

    public int PatientId { get; set; }

    public Patient Patient { get; set; } = null!;

    public string? SubmittedByUserId { get; set; }

    public ApplicationUser? SubmittedByUser { get; set; }

    public int Year { get; set; }

    public int Month { get; set; }

    public MonthlyReturnStatus Status { get; set; } = MonthlyReturnStatus.Draft;

    public int CheckInsCompleted { get; set; }

    public int AlertsRaised { get; set; }

    public int VisitsCompleted { get; set; }

    public string? Summary { get; set; }

    public DateTimeOffset? SubmittedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? ReviewedByUserId { get; set; }

    public ApplicationUser? ReviewedByUser { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public string? ReviewNotes { get; set; }

    public ICollection<Dhis2IndicatorSubmission> Dhis2IndicatorSubmissions { get; } = new List<Dhis2IndicatorSubmission>();
}

public enum MonthlyReturnStatus
{
    Draft,
    Submitted,
    Approved,
    Rejected
}
