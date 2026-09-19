using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartElderlyCare.Data;
using SmartElderlyCare.Models;

namespace SmartElderlyCare.Pages.Hiu;

[Authorize(Roles = "HiuClerk")]
public class DashboardModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;

    public DashboardModel(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IReadOnlyList<VitalSignsReading> RecentReadings { get; private set; } = [];

    public IReadOnlyList<Alert> ActiveAlerts { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        RecentReadings = await _dbContext.VitalSignsReadings
            .AsNoTracking()
            .Include(reading => reading.Patient)
            .OrderByDescending(reading => reading.RecordedAt)
            .Take(12)
            .ToListAsync(cancellationToken);

        var alerts = await _dbContext.Alerts
            .AsNoTracking()
            .Include(alert => alert.Patient)
            .Where(alert => alert.Status != AlertStatus.Resolved && !alert.Reviewed)
            .ToListAsync(cancellationToken);

        ActiveAlerts = alerts
            .OrderByDescending(alert => alert.Severity)
            .ThenByDescending(alert => alert.CreatedAt)
            .ToList();
    }

    public async Task<IActionResult> OnPostMarkReviewedAsync(
        long alertId,
        CancellationToken cancellationToken)
    {
        var alert = await _dbContext.Alerts
            .SingleOrDefaultAsync(item => item.Id == alertId, cancellationToken);

        if (alert is null)
        {
            return NotFound(new { message = "Alert not found." });
        }

        alert.Reviewed = true;
        alert.ReviewedAt = DateTimeOffset.UtcNow;
        alert.ReviewedByUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new JsonResult(new { success = true, alertId });
    }
}
