namespace SmartElderlyCare.Models;

public class RoleWorkspaceViewModel
{
    public string DisplayName { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    public IReadOnlyList<RoleWorkspaceAction> Actions { get; set; } = [];

    public IReadOnlyList<LinkedFamilyPatient> LinkedPatients { get; set; } = [];
}

public record LinkedFamilyPatient(
    int PatientId,
    string PatientName,
    string PatientNumber,
    string MedicalCondition,
    FamilyAccessLevel AccessLevel,
    string? Relationship);

public record RoleWorkspaceAction(
    string Title,
    string Description,
    string Controller,
    string? Action = null,
    string? Page = null,
    string? Area = null,
    string Style = "green");
