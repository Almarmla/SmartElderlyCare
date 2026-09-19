namespace SmartElderlyCare.Models;

public class Threshold
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? MedicalCondition { get; set; }

    public ThresholdMetric Metric { get; set; }

    public decimal? MinimumValue { get; set; }

    public decimal? MaximumValue { get; set; }

    public string? Unit { get; set; }

    public AlertSeverity Severity { get; set; } = AlertSeverity.Medium;

    public bool IsActive { get; set; } = true;

    public ICollection<AdministrationAuditLog> AdministrationAuditLogs { get; } = new List<AdministrationAuditLog>();
}

public enum ThresholdMetric
{
    TemperatureCelsius,
    SystolicBloodPressure,
    DiastolicBloodPressure,
    PulseRate,
    RespiratoryRate,
    OxygenSaturation,
    WeightKilograms
    ,
    BloodGlucoseMgDl
}
