using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartElderlyCare.Models;

namespace SmartElderlyCare.Areas.Admin.Pages.Roles;

[Authorize(Roles = ApplicationRoles.DhioAdmin + "," + ApplicationRoles.Administrator)]
public class IndexModel : PageModel
{
    private readonly RoleManager<IdentityRole> _roleManager;

    public IndexModel(RoleManager<IdentityRole> roleManager)
    {
        _roleManager = roleManager;
    }

    public IReadOnlyList<RoleSummary> Roles { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Roles = await _roleManager.Roles
            .AsNoTracking()
            .OrderBy(role => role.Name)
            .Select(role => new RoleSummary(role.Name!, role.Id))
            .ToListAsync(cancellationToken);
    }

    public sealed record RoleSummary(string Name, string Id);
}
