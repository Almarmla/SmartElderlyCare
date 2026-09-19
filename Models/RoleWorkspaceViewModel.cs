namespace SmartElderlyCare.Models;

public class RoleWorkspaceViewModel
{
    public string DisplayName { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    public IReadOnlyList<RoleWorkspaceAction> Actions { get; set; } = [];
}

public record RoleWorkspaceAction(
    string Title,
    string Description,
    string Controller,
    string? Action = null,
    string? Page = null,
    string? Area = null,
    string Style = "green");
