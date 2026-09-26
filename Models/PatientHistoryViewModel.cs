namespace SmartElderlyCare.Models;

public class PatientHistoryViewModel
{
    public Patient Patient { get; set; } = null!;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public IReadOnlyList<VitalSignsReading> Readings { get; set; } = [];

    public IReadOnlyList<VhwWelfareCheck> WelfareChecks { get; set; } = [];

    public IReadOnlyList<FacilityVisit> FacilityVisits { get; set; } = [];

    public IReadOnlyList<Alert> Alerts { get; set; } = [];

    public IReadOnlyList<PatientMedication> Medications { get; set; } = [];

    public IReadOnlyList<PatientFamilyMember> FamilyMembers { get; set; } = [];

    public IReadOnlyList<PatientActivity> Activities { get; set; } = [];

    public bool ShowAllTime { get; set; }
}

public enum PatientActivityType
{
    FacilityVisit,
    VhwWelfareCheck,
    Alert
}

public sealed class PatientActivity
{
    public DateTimeOffset OccurredAt { get; set; }

    public PatientActivityType Type { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}
