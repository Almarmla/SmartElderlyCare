using Microsoft.AspNetCore.Identity;

namespace SmartElderlyCare.Models;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;

    public string? FacilityName { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Patient> AssignedPatients { get; } = new List<Patient>();

    public ICollection<PatientFamilyMember> FamilyPatientLinks { get; } = new List<PatientFamilyMember>();

    public ICollection<VitalSignsReading> RecordedVitalSigns { get; } = new List<VitalSignsReading>();

    public ICollection<FacilityVisit> FacilityVisits { get; } = new List<FacilityVisit>();

    public ICollection<WelfareObservation> RecordedWelfareObservations { get; } = new List<WelfareObservation>();

    public ICollection<VhwWelfareCheck> VhwWelfareChecks { get; } = new List<VhwWelfareCheck>();

    public ICollection<Alert> AssignedAlerts { get; } = new List<Alert>();

    public ICollection<MonthlyReturn> SubmittedMonthlyReturns { get; } = new List<MonthlyReturn>();

    public ICollection<MonthlyReturn> ReviewedMonthlyReturns { get; } = new List<MonthlyReturn>();

    public ICollection<Dhis2IndicatorSubmission> Dhis2IndicatorSubmissions { get; } = new List<Dhis2IndicatorSubmission>();

    public ICollection<AdministrationAuditLog> AdministrationAuditLogs { get; } = new List<AdministrationAuditLog>();

    public ICollection<DistrictReport> DistrictReports { get; } = new List<DistrictReport>();
}
