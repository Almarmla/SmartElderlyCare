namespace SmartElderlyCare.Models;

public class FamilyLinkPageViewModel
{
    public FamilyMemberLinkInputModel Input { get; set; } = new();

    public IReadOnlyList<Patient> Patients { get; set; } = [];

    public IReadOnlyList<ApplicationUser> FamilyMembers { get; set; } = [];

    public IReadOnlyList<PatientFamilyMember> ExistingLinks { get; set; } = [];
}