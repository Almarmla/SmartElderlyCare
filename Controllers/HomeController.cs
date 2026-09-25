using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartElderlyCare.Data;
using Microsoft.AspNetCore.Mvc;
using SmartElderlyCare.Models;

namespace SmartElderlyCare.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public HomeController(
        ApplicationDbContext dbContext,
        SignInManager<ApplicationUser> signInManager)
    {
        _dbContext = dbContext;
        _signInManager = signInManager;
    }

    public IActionResult Index()
    {
        return RedirectToAction(nameof(Login));
    }

    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        return RedirectToAction("Login", "Account", new { returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public IActionResult Login(string? email, string? password)
    {
        return RedirectToAction("Login", "Account");
    }

    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
    {
        var catNow = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(2));
        ViewData["CatDate"] = catNow.ToString("dddd, MMMM d, yyyy", CultureInfo.InvariantCulture);
        ViewData["Greeting"] = catNow.Hour < 12
            ? "Good morning"
            : catNow.Hour < 18
                ? "Good afternoon"
                : "Good evening";
        ViewData["UserRole"] = User.FindFirstValue(ClaimTypes.Role) ?? "Administrator";
        ViewData["DisplayName"] = User.FindFirstValue("DisplayName")
            ?? User.Identity?.Name
            ?? "Care team member";

        var todayStart = new DateTimeOffset(catNow.Date, catNow.Offset);
        var tomorrowStart = todayStart.AddDays(1);
        var monthStart = new DateTimeOffset(new DateTime(catNow.Year, catNow.Month, 1), catNow.Offset);
        var nextMonthStart = new DateTimeOffset(
            catNow.Month == 12
                ? new DateTime(catNow.Year + 1, 1, 1)
                : new DateTime(catNow.Year, catNow.Month + 1, 1),
            catNow.Offset);

        var stats = new DashboardStatsViewModel
        {
            ActivePatients = await _dbContext.Patients.CountAsync(patient => patient.IsActive, cancellationToken),
            ActiveAlertsToday = await _dbContext.Alerts.CountAsync(
                alert => alert.Status != AlertStatus.Resolved
                    && alert.CreatedAt >= todayStart
                    && alert.CreatedAt < tomorrowStart,
                cancellationToken),
            CompletedVisitsThisMonth = await _dbContext.FacilityVisits.CountAsync(
                visit => visit.Status == FacilityVisitStatus.Completed
                    && visit.VisitAt >= monthStart
                    && visit.VisitAt < nextMonthStart,
                cancellationToken),
            VhwChecksThisMonth = await _dbContext.VhwWelfareChecks.CountAsync(
                check => check.ObservedAt >= monthStart && check.ObservedAt < nextMonthStart,
                cancellationToken),
            TodayActivity = await LoadTodayActivityAsync(todayStart, tomorrowStart, cancellationToken),
            ConditionBreakdown = await LoadConditionBreakdownAsync(cancellationToken),
            NeedsAttention = await LoadNeedsAttentionAsync(cancellationToken)
        };

        return View(stats);
    }

    private async Task<IReadOnlyList<TodayActivityItem>> LoadTodayActivityAsync(
        DateTimeOffset todayStart,
        DateTimeOffset tomorrowStart,
        CancellationToken cancellationToken)
    {
        var todayVisits = await _dbContext.FacilityVisits
            .AsNoTracking()
            .Where(visit => visit.VisitAt >= todayStart && visit.VisitAt < tomorrowStart)
            .OrderByDescending(visit => visit.VisitAt)
            .Take(5)
            .Select(visit => new TodayActivityItem
            {
                Type = "Facility visit",
                PatientName = visit.Patient.FirstName + " " + visit.Patient.LastName,
                Detail = visit.Diagnosis,
                RecordedAt = visit.VisitAt
            })
            .ToListAsync(cancellationToken);

        var todayChecks = await _dbContext.VhwWelfareChecks
            .AsNoTracking()
            .Where(check => check.ObservedAt >= todayStart && check.ObservedAt < tomorrowStart)
            .OrderByDescending(check => check.ObservedAt)
            .Take(5)
            .Select(check => new TodayActivityItem
            {
                Type = "VHW welfare check",
                PatientName = check.Patient.FirstName + " " + check.Patient.LastName,
                Detail = check.Status.ToString(),
                RecordedAt = check.ObservedAt
            })
            .ToListAsync(cancellationToken);

        return todayVisits
            .Concat(todayChecks)
            .OrderByDescending(item => item.RecordedAt)
            .Take(6)
            .ToList();
    }

    private async Task<IReadOnlyList<ConditionBreakdownItem>> LoadConditionBreakdownAsync(CancellationToken cancellationToken)
    {
        var conditionValues = await _dbContext.Patients
            .AsNoTracking()
            .Where(patient => patient.IsActive && !string.IsNullOrWhiteSpace(patient.MedicalCondition))
            .Select(patient => patient.MedicalCondition!)
            .ToListAsync(cancellationToken);

        return conditionValues
            .SelectMany(value => value.Split(
                new[] { ',', ';', '&', '/' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(condition => condition.Length > 0)
            .GroupBy(condition => condition, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ConditionBreakdownItem { Condition = group.Key, Count = group.Count() })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Condition)
            .Take(6)
            .ToList();
    }

    private async Task<IReadOnlyList<AttentionItem>> LoadNeedsAttentionAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Alerts
            .AsNoTracking()
            .Where(alert => alert.Status == AlertStatus.Open)
            .OrderByDescending(alert => alert.Severity)
            .ThenByDescending(alert => alert.CreatedAt)
            .Take(6)
            .Select(alert => new AttentionItem
            {
                Type = alert.Type.ToString(),
                Severity = alert.Severity.ToString(),
                PatientName = alert.Patient.FirstName + " " + alert.Patient.LastName,
                Message = alert.Message,
                CreatedAt = alert.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    [HttpGet]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> UnreadAlerts(CancellationToken cancellationToken)
    {
        var alerts = await _dbContext.Alerts
            .AsNoTracking()
            .Include(alert => alert.Patient)
            .Where(alert => !alert.IsRead && alert.Status != AlertStatus.Resolved)
            .OrderByDescending(alert => alert.VitalStatus)
            .ThenByDescending(alert => alert.CreatedAt)
            .Select(alert => new
            {
                alert.Id,
                ResidentName = alert.Patient.FirstName + " " + alert.Patient.LastName,
                alert.Metric,
                alert.Value,
                Status = alert.VitalStatus.ToString(),
                alert.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Json(alerts);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> MarkAlertRead(long alertId, CancellationToken cancellationToken)
    {
        var alert = await _dbContext.Alerts
            .SingleOrDefaultAsync(item => item.Id == alertId, cancellationToken);

        if (alert is null)
        {
            return NotFound(new { message = "Alert not found." });
        }

        alert.IsRead = true;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Json(new { success = true, alertId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Login", "Account");
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

}
