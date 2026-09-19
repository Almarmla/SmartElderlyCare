namespace SmartElderlyCare.Models;

public class FacilityVisit
{
    public long Id { get; set; }

    public int PatientId { get; set; }

    public Patient Patient { get; set; } = null!;

    public string RecordedByUserId { get; set; } = string.Empty;

    public ApplicationUser RecordedByUser { get; set; } = null!;

    public long? VitalSignsReadingId { get; set; }

    public VitalSignsReading? VitalSignsReading { get; set; }

    public DateTimeOffset VisitAt { get; set; } = DateTimeOffset.UtcNow;

    public string FacilityName { get; set; } = string.Empty;

    public string Diagnosis { get; set; } = string.Empty;

    public string Treatment { get; set; } = string.Empty;

    public string? ClinicalNotes { get; set; }

    public FacilityVisitStatus Status { get; set; } = FacilityVisitStatus.Completed;
}

public enum FacilityVisitStatus
{
    Scheduled,
    InProgress,
    Completed,
    Cancelled
}
