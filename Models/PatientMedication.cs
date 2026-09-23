namespace SmartElderlyCare.Models;

public class PatientMedication
{
    public long Id { get; set; }

    public int PatientId { get; set; }

    public Patient Patient { get; set; } = null!;

    public string? RecordedByUserId { get; set; }

    public ApplicationUser? RecordedByUser { get; set; }

    public string MedicationName { get; set; } = string.Empty;

    public string? Dosage { get; set; }

    public string? Frequency { get; set; }

    public string? Route { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset PrescribedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? Notes { get; set; }
}