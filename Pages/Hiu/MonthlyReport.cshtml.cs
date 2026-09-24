using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartElderlyCare.Data;
using SmartElderlyCare.Models;
using SmartElderlyCare.Services;

namespace SmartElderlyCare.Pages.Hiu;

[Authorize(Roles = "HiuClerk,RecordsStaff,DhioAdmin,Dmo")]
public class MonthlyReportModel : PageModel
{
    private readonly Dhis2MonthlyExportService _exportService;
    private readonly ApplicationDbContext _dbContext;

    public MonthlyReportModel(Dhis2MonthlyExportService exportService, ApplicationDbContext dbContext)
    {
        _exportService = exportService;
        _dbContext = dbContext;
    }

    [BindProperty(SupportsGet = true)]
    public int Year { get; set; } = DateTime.UtcNow.Year;

    [BindProperty(SupportsGet = true)]
    public int Month { get; set; } = DateTime.UtcNow.Month;

    [BindProperty(SupportsGet = true)]
    public string OrganisationUnit { get; set; } = "SMART-ELDERLY-CARE";

    public IReadOnlyList<SubmissionHistoryEntry> Submissions { get; private set; } = [];

    public string PeriodLabel =>
        $"{CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(Month)} {Year}";

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (Month is < 1 or > 12)
        {
            ModelState.AddModelError(nameof(Month), "Month must be between 1 and 12.");
        }

        await LoadHistoryAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostDownloadAsync(CancellationToken cancellationToken)
    {
        if (Month is < 1 or > 12 || Year is < 2000 or > 2100 || string.IsNullOrWhiteSpace(OrganisationUnit))
        {
            ModelState.AddModelError(string.Empty, "Enter a valid year, month, and organisation unit.");
            await LoadHistoryAsync(cancellationToken);
            return Page();
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var organisationUnit = OrganisationUnit.Trim();
        try
        {
            var submissions = await _exportService.RecordSubmissionBatchAsync(
                Year,
                Month,
                organisationUnit,
                userId,
                cancellationToken);
            TempData["SubmissionSuccess"] =
                $"Recorded {submissions.Count} DHIS2 indicator submissions for {PeriodLabel}.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["SubmissionWarning"] = $"{exception.Message} The CSV was still generated.";
        }

        var csv = await _exportService.ExportMonthAsCsvAsync(Year, Month, organisationUnit, cancellationToken);
        return File(csv, "text/csv", $"dhis2-monthly-return-{Year:0000}-{Month:00}.csv");
    }

    private async Task LoadHistoryAsync(CancellationToken cancellationToken)
    {
        Submissions = await _dbContext.Dhis2IndicatorSubmissions
            .AsNoTracking()
            .Where(submission => submission.SubmittedAt != null)
            .GroupBy(submission => new
            {
                submission.MonthlyReturn.Year,
                submission.MonthlyReturn.Month,
                submission.OrganisationUnitUid,
                SubmittedAt = submission.SubmittedAt!.Value
            })
            .OrderByDescending(group => group.Key.SubmittedAt)
            .Take(10)
            .Select(group => new SubmissionHistoryEntry
            {
                Year = group.Key.Year,
                Month = group.Key.Month,
                OrganisationUnit = group.Key.OrganisationUnitUid,
                At = group.Key.SubmittedAt,
                IndicatorCount = group.Count(),
                Status = group.Any(submission => submission.Status == Dhis2SubmissionStatus.Failed)
                    ? Dhis2SubmissionStatus.Failed
                    : group.All(submission => submission.Status == Dhis2SubmissionStatus.Submitted)
                        ? Dhis2SubmissionStatus.Submitted
                        : Dhis2SubmissionStatus.Pending
            })
            .ToListAsync(cancellationToken);
    }

    public sealed class SubmissionHistoryEntry
    {
        public DateTimeOffset At { get; init; }

        public int Year { get; init; }

        public int Month { get; init; }

        public string OrganisationUnit { get; init; } = string.Empty;

        public int IndicatorCount { get; init; }

        public Dhis2SubmissionStatus Status { get; init; }

        public string MonthName => CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(Month);
    }
}