namespace SmartElderlyCare.Models;

public class DistrictReport
{
    public long Id { get; set; }

    public string DistrictCode { get; set; } = string.Empty;

    public string DistrictName { get; set; } = string.Empty;

    public int ReportingYear { get; set; }

    public int ReportingMonth { get; set; }

    public int PatientCount { get; set; }

    public int ActivePatients { get; set; }

    public int VisitsCompleted { get; set; }

    public int WelfareChecksCompleted { get; set; }

    public int AlertsRaised { get; set; }

    public int AlertsResolved { get; set; }

    public string? MorbiditySummary { get; set; }

    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    public string GeneratedByUserId { get; set; } = string.Empty;

    public ApplicationUser GeneratedByUser { get; set; } = null!;

    public DistrictReportStatus Status { get; set; } = DistrictReportStatus.Published;
}

public enum DistrictReportStatus
{
    Draft,
    Published,
    Archived
}
