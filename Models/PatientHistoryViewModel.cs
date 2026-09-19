namespace SmartElderlyCare.Models;

public class PatientHistoryViewModel
{
    public Patient Patient { get; set; } = null!;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public IReadOnlyList<VitalSignsReading> Readings { get; set; } = [];

    public IReadOnlyList<VhwWelfareCheck> WelfareChecks { get; set; } = [];

    public IReadOnlyList<WelfareObservation> WelfareObservations { get; set; } = [];

    public IReadOnlyList<FacilityVisit> FacilityVisits { get; set; } = [];

    public IReadOnlyList<Alert> Alerts { get; set; } = [];
}
