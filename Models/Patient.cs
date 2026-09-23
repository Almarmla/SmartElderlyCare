namespace SmartElderlyCare.Models;

public class Patient
{
    public int Id { get; set; }

    public string PatientNumber { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public DateOnly DateOfBirth { get; set; }

    public string? Sex { get; set; }

    public string? NationalId { get; set; }

    public string? MedicalCondition { get; set; }

    public string? ChronicCareProgramme { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Address { get; set; }

    public string? EmergencyContactName { get; set; }

    public string? EmergencyContactPhone { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? AssignedUserId { get; set; }

    public ApplicationUser? AssignedUser { get; set; }

    public ICollection<PatientFamilyMember> FamilyMembers { get; } = new List<PatientFamilyMember>();

    public ICollection<VitalSignsReading> VitalSignsReadings { get; } = new List<VitalSignsReading>();

    public ICollection<FacilityVisit> FacilityVisits { get; } = new List<FacilityVisit>();

    public ICollection<PatientMedication> Medications { get; } = new List<PatientMedication>();

    public ICollection<WelfareObservation> WelfareObservations { get; } = new List<WelfareObservation>();

    public ICollection<VhwWelfareCheck> VhwWelfareChecks { get; } = new List<VhwWelfareCheck>();

    public ICollection<Alert> Alerts { get; } = new List<Alert>();

    public ICollection<MonthlyReturn> MonthlyReturns { get; } = new List<MonthlyReturn>();
}
