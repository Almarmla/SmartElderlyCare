namespace SmartElderlyCare.Models;

public class Dhis2IndicatorSubmission
{
    public long Id { get; set; }

    public long MonthlyReturnId { get; set; }

    public MonthlyReturn MonthlyReturn { get; set; } = null!;

    public string CapturedByUserId { get; set; } = string.Empty;

    public ApplicationUser CapturedByUser { get; set; } = null!;

    public string IndicatorUid { get; set; } = string.Empty;

    public string IndicatorName { get; set; } = string.Empty;

    public decimal Value { get; set; }

    public string OrganisationUnitUid { get; set; } = string.Empty;

    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;

    public Dhis2SubmissionStatus Status { get; set; } = Dhis2SubmissionStatus.Pending;

    public DateTimeOffset? SubmittedAt { get; set; }

    public string? Dhis2ResponseId { get; set; }

    public string? ErrorMessage { get; set; }
}

public enum Dhis2SubmissionStatus
{
    Pending,
    Submitted,
    Failed
}
