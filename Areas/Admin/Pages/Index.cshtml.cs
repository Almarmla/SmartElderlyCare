using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartElderlyCare.Data;
using SmartElderlyCare.Models;

namespace SmartElderlyCare.Areas.Admin.Pages;

[Authorize(AuthenticationSchemes = "AdminCookie,Identity.Application", Roles = ApplicationRoles.DhioAdmin)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public IndexModel(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }

    public int ActiveUserCount { get; private set; }

    public int ThresholdCount { get; private set; }

    public IReadOnlyList<Threshold> Thresholds { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        ActiveUserCount = await _userManager.Users.CountAsync(user => user.IsActive, cancellationToken);
        ThresholdCount = await _dbContext.Thresholds.CountAsync(threshold => threshold.IsActive, cancellationToken);
        Thresholds = await _dbContext.Thresholds
            .AsNoTracking()
            .OrderBy(threshold => threshold.MedicalCondition)
            .ThenBy(threshold => threshold.Metric)
            .ToListAsync(cancellationToken);
    }
}
