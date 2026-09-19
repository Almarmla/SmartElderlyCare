namespace SmartElderlyCare.Models;

public class VitalSignsReading
{
    public long Id { get; set; }

    public int PatientId { get; set; }

    public Patient Patient { get; set; } = null!;

    public string? RecordedByUserId { get; set; }

    public ApplicationUser? RecordedByUser { get; set; }

    public ICollection<FacilityVisit> FacilityVisits { get; } = new List<FacilityVisit>();

    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;

    public decimal? TemperatureCelsius { get; set; }

    public int? SystolicBloodPressure { get; set; }

    public int? DiastolicBloodPressure { get; set; }

    public int? PulseRate { get; set; }

    public int? RespiratoryRate { get; set; }

    public decimal? OxygenSaturation { get; set; }

    public decimal? BloodGlucoseMgDl { get; set; }

    public decimal? WeightKilograms { get; set; }

    public string? Notes { get; set; }
}
