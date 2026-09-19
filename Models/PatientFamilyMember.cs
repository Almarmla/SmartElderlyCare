namespace SmartElderlyCare.Models;

public class PatientFamilyMember
{
    public int PatientId { get; set; }

    public Patient Patient { get; set; } = null!;

    public string FamilyMemberUserId { get; set; } = string.Empty;

    public ApplicationUser FamilyMemberUser { get; set; } = null!;

    public FamilyAccessLevel AccessLevel { get; set; } = FamilyAccessLevel.Viewer;

    public string? RelationshipToPatient { get; set; }

    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsActive { get; set; } = true;
}

public enum FamilyAccessLevel
{
    Viewer,
    Contributor
}
