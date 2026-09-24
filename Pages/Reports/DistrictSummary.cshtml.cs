using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartElderlyCare.Data;
using SmartElderlyCare.Models;
using SmartElderlyCare.Services;

namespace SmartElderlyCare.Pages.Reports;

[Authorize(Roles = "Dmo,DhioAdmin,Administrator")]
public class DistrictSummaryModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDistrictReportService _reportService;

    public DistrictSummaryModel(ApplicationDbContext dbContext, IDistrictReportService reportService)
    {
        _dbContext = dbContext;
        _reportService = reportService;
    }

    [BindProperty(SupportsGet = true)]
    public int Year { get; set; } = DateTime.UtcNow.Year;

    [BindProperty(SupportsGet = true)]
    public int Month { get; set; } = DateTime.UtcNow.Month;

    [BindProperty(SupportsGet = true)]
    public string DistrictCode { get; set; } = "SMART-ELDERLY-CARE";

    [BindProperty(SupportsGet = true)]
    public string DistrictName { get; set; } = "Smart Elderly Care District";

    public DistrictReport? CurrentReport { get; private set; }

    public int LivePatients { get; private set; }

    public int LiveVisitsCompleted { get; private set; }

    public int LiveWelfareChecks { get; private set; }

    public int LiveHighPriorityAlerts { get; private set; }

    public IReadOnlyList<MorbidityEntry> Morbidity { get; private set; } = [];

    public IReadOnlyList<DistrictReport> PastReports { get; private set; } = [];

    public string SelectedPeriodLabel => $"{Month:00}/{Year}";

    public bool CanPublish =>
        User.IsInRole(ApplicationRoles.Dmo) || User.IsInRole(ApplicationRoles.DhioAdmin);

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (Month is < 1 or > 12 || Year is < 2000 or > 2100)
        {
            ModelState.AddModelError(string.Empty, "Select a valid year and month.");
            await LoadPastReportsAsync(cancellationToken);
            return Page();
        }

        CurrentReport = await _dbContext.DistrictReports
            .AsNoTracking()
            .FirstOrDefaultAsync(
                report => report.DistrictCode == DistrictCode
                    && report.ReportingYear == Year
                    && report.ReportingMonth == Month,
                cancellationToken);

        await LoadLiveStatsAsync(cancellationToken);
        await LoadPastReportsAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostPublishAsync(
        int year,
        int month,
        string districtCode,
        string districtName,
        CancellationToken cancellationToken)
    {
        Year = year;
        Month = month;
        DistrictCode = districtCode;
        DistrictName = districtName;

        if (Month is < 1 or > 12
            || Year is < 2000 or > 2100
            || string.IsNullOrWhiteSpace(DistrictCode)
            || string.IsNullOrWhiteSpace(DistrictName))
        {
            ModelState.AddModelError(string.Empty, "Enter a valid year, month, district code, and district name.");
            await LoadLiveStatsAsync(cancellationToken);
            await LoadPastReportsAsync(cancellationToken);
            return Page();
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        await _reportService.GenerateOrUpdateReportAsync(
            Year,
            Month,
            DistrictCode.Trim(),
            DistrictName.Trim(),
            userId,
            cancellationToken);

        TempData["SuccessMessage"] = $"District report for {Month}/{Year} published successfully.";
        return RedirectToPage(new { year = Year, month = Month, districtCode = DistrictCode, districtName = DistrictName });
    }

    private async Task LoadLiveStatsAsync(CancellationToken cancellationToken)
    {
        var start = StartOfMonth();
        var end = start.AddMonths(1);

        LivePatients = await _dbContext.Patients.CountAsync(patient => patient.IsActive, cancellationToken);
        LiveVisitsCompleted = await _dbContext.FacilityVisits.CountAsync(
            visit => visit.Status == FacilityVisitStatus.Completed && visit.VisitAt >= start && visit.VisitAt < end,
            cancellationToken);
        LiveWelfareChecks = await _dbContext.VhwWelfareChecks.CountAsync(
            check => check.ObservedAt >= start && check.ObservedAt < end,
            cancellationToken);
        LiveHighPriorityAlerts = await _dbContext.Alerts.CountAsync(
            alert => (alert.Severity == AlertSeverity.Critical || alert.Severity == AlertSeverity.High)
                && alert.CreatedAt >= start
                && alert.CreatedAt < end,
            cancellationToken);

        var diagnoses = await _dbContext.FacilityVisits
            .AsNoTracking()
            .Where(visit => visit.Status == FacilityVisitStatus.Completed
                && visit.VisitAt >= start
                && visit.VisitAt < end
                && !string.IsNullOrWhiteSpace(visit.Diagnosis))
            .Select(visit => visit.Diagnosis)
            .ToListAsync(cancellationToken);

        Morbidity = diagnoses
            .GroupBy(diagnosis => diagnosis.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => new MorbidityEntry { Diagnosis = group.Key, Cases = group.Count() })
            .OrderByDescending(entry => entry.Cases)
            .ThenBy(entry => entry.Diagnosis, StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
    }

    private async Task LoadPastReportsAsync(CancellationToken cancellationToken)
    {
        PastReports = await _dbContext.DistrictReports
            .AsNoTracking()
            .OrderByDescending(report => report.ReportingYear)
            .ThenByDescending(report => report.ReportingMonth)
            .ThenByDescending(report => report.GeneratedAt)
            .ToListAsync(cancellationToken);
    }

    private DateTimeOffset StartOfMonth() => new(Year, Month, 1, 0, 0, 0, TimeSpan.Zero);

    public sealed class MorbidityEntry
    {
        public string Diagnosis { get; init; } = string.Empty;

        public int Cases { get; init; }
    }
}