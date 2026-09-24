using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartElderlyCare.Data;
using SmartElderlyCare.Models;

namespace SmartElderlyCare.Pages.Returns;

[Authorize(Roles = "RecordsStaff,FacilityNurse,HiuClerk")]
public class MonthlySummaryModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;

    public MonthlySummaryModel(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [BindProperty(SupportsGet = true)]
    public int Year { get; set; } = DateTime.UtcNow.Year;

    [BindProperty(SupportsGet = true)]
    public int Month { get; set; } = DateTime.UtcNow.Month;

    public int TotalVisitsCompleted { get; private set; }

    public decimal CompletenessScore { get; private set; }

    public int AlertsRaised { get; private set; }

    public MonthlyReturnStatus? CurrentStatus { get; private set; }

    public IReadOnlyList<DiagnosisSummary> Diagnoses { get; private set; } = [];

    public IReadOnlyList<ValidationEntry> ValidationHistory { get; private set; } = [];

    public string SelectedMonthName =>
        CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(Month);

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!ValidatePeriod())
        {
            return Page();
        }

        await LoadSummaryAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostCompileAsync(CancellationToken cancellationToken)
    {
        if (!ValidatePeriod())
        {
            return Page();
        }

        var visits = await LoadVisitsAsync(cancellationToken);
        if (visits.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "There are no facility visits in the selected month to compile.");
            await LoadSummaryAsync(cancellationToken);
            return Page();
        }

        var existingReturns = await _dbContext.MonthlyReturns
            .Where(returnItem => returnItem.Year == Year && returnItem.Month == Month)
            .ToDictionaryAsync(returnItem => returnItem.PatientId, cancellationToken);
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var diagnosisSummary = string.Join(
            "; ",
            BuildDiagnosisSummary(visits)
                .Select(diagnosis => $"{diagnosis.Name}: {diagnosis.VisitCount}"));

        foreach (var patientVisits in visits.GroupBy(visit => visit.PatientId))
        {
            var completedVisits = patientVisits
                .Where(visit => visit.Status == FacilityVisitStatus.Completed)
                .ToList();
            var patientAlerts = await _dbContext.Alerts
                .CountAsync(
                    alert => alert.PatientId == patientVisits.Key &&
                             alert.CreatedAt >= StartOfMonth() &&
                             alert.CreatedAt < StartOfNextMonth(),
                    cancellationToken);

            if (!existingReturns.TryGetValue(patientVisits.Key, out var monthlyReturn))
            {
                monthlyReturn = new MonthlyReturn
                {
                    PatientId = patientVisits.Key,
                    Year = Year,
                    Month = Month
                };
                _dbContext.MonthlyReturns.Add(monthlyReturn);
            }

            monthlyReturn.CheckInsCompleted = 0;
            monthlyReturn.VisitsCompleted = completedVisits.Count;
            monthlyReturn.AlertsRaised = patientAlerts;
            monthlyReturn.Summary = diagnosisSummary;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = $"Monthly return draft compiled for {SelectedMonthName} {Year}.";
        return RedirectToPage(new { Year, Month });
    }

    public async Task<IActionResult> OnPostSubmitAsync(CancellationToken cancellationToken)
    {
        if (!ValidatePeriod())
        {
            return Page();
        }

        var monthlyReturns = await _dbContext.MonthlyReturns
            .Where(returnItem => returnItem.Year == Year && returnItem.Month == Month)
            .ToListAsync(cancellationToken);
        if (monthlyReturns.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Compile and save a draft before submitting it to the HIU.");
            await LoadSummaryAsync(cancellationToken);
            return Page();
        }

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var submittedAt = DateTimeOffset.UtcNow;
        foreach (var monthlyReturn in monthlyReturns.Where(returnItem => returnItem.Status != MonthlyReturnStatus.Approved))
        {
            monthlyReturn.Status = MonthlyReturnStatus.Submitted;
            monthlyReturn.SubmittedByUserId = userId;
            monthlyReturn.SubmittedAt = submittedAt;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = $"Monthly return submitted to the HIU for {SelectedMonthName} {Year}.";
        return RedirectToPage(new { Year, Month });
    }

    public async Task<IActionResult> OnPostApproveAsync(int year, int month, string notes)
        => await ProcessValidationAsync(year, month, notes, MonthlyReturnValidationStatus.Approved);

    public async Task<IActionResult> OnPostRejectAsync(int year, int month, string notes)
        => await ProcessValidationAsync(year, month, notes, MonthlyReturnValidationStatus.Rejected);

    private async Task<IActionResult> ProcessValidationAsync(int year, int month, string notes, MonthlyReturnValidationStatus decision)
    {
        Year = year;
        Month = month;

        if (!ValidatePeriod())
        {
            await LoadSummaryAsync(CancellationToken.None);
            return Page();
        }

        if (string.IsNullOrWhiteSpace(notes))
        {
            ModelState.AddModelError(string.Empty, "Validation notes are required before approving or rejecting a monthly return.");
            await LoadSummaryAsync(CancellationToken.None);
            return Page();
        }

        if (User?.IsInRole(ApplicationRoles.HiuClerk) != true)
        {
            ModelState.AddModelError(string.Empty, "Only HIU clerks can validate monthly returns.");
            await LoadSummaryAsync(CancellationToken.None);
            return Page();
        }

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var monthlyReturns = await _dbContext.MonthlyReturns
            .Where(returnItem => returnItem.Year == Year && returnItem.Month == Month)
            .ToListAsync(CancellationToken.None);
        if (monthlyReturns.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "There is no monthly return for the selected period to validate.");
            await LoadSummaryAsync(CancellationToken.None);
            return Page();
        }

        var targetStatus = decision == MonthlyReturnValidationStatus.Approved
            ? MonthlyReturnStatus.Approved
            : MonthlyReturnStatus.Rejected;
        var validatedAt = DateTimeOffset.UtcNow;

        foreach (var monthlyReturn in monthlyReturns)
        {
            monthlyReturn.Status = targetStatus;
            monthlyReturn.ReviewedByUserId = userId;
            monthlyReturn.ReviewedAt = validatedAt;
            monthlyReturn.ReviewNotes = notes;

            _dbContext.MonthlyReturnValidations.Add(new MonthlyReturnValidation
            {
                MonthlyReturnId = monthlyReturn.Id,
                ValidatedByUserId = userId,
                ValidatedAt = validatedAt,
                Status = decision,
                Notes = notes
            });
        }

        await _dbContext.SaveChangesAsync(CancellationToken.None);

        TempData["SuccessMessage"] = decision == MonthlyReturnValidationStatus.Approved
            ? $"Monthly return approved for {SelectedMonthName} {Year}."
            : $"Monthly return rejected for {SelectedMonthName} {Year}.";
        return RedirectToPage(new { Year, Month });
    }

    private async Task LoadSummaryAsync(CancellationToken cancellationToken)
    {
        var visits = await LoadVisitsAsync(cancellationToken);
        var completedVisits = visits
            .Where(visit => visit.Status == FacilityVisitStatus.Completed)
            .ToList();

        TotalVisitsCompleted = completedVisits.Count;
        var completeVisitCount = completedVisits.Count(IsComplete);
        CompletenessScore = TotalVisitsCompleted == 0
            ? 0
            : Math.Round(completeVisitCount * 100m / TotalVisitsCompleted, 1);
        Diagnoses = BuildDiagnosisSummary(completedVisits)
            .GroupBy(diagnosis => diagnosis.Name)
            .Select(group => new DiagnosisSummary
            {
                Name = group.Key,
                VisitCount = group.Sum(diagnosis => diagnosis.VisitCount)
            })
            .OrderByDescending(diagnosis => diagnosis.VisitCount)
            .ThenBy(diagnosis => diagnosis.Name)
            .ToList();
        AlertsRaised = await _dbContext.Alerts
            .CountAsync(
                alert => alert.CreatedAt >= StartOfMonth() &&
                         alert.CreatedAt < StartOfNextMonth(),
                cancellationToken);

        var statuses = await _dbContext.MonthlyReturns
            .Where(returnItem => returnItem.Year == Year && returnItem.Month == Month)
            .Select(returnItem => returnItem.Status)
            .ToListAsync(cancellationToken);
        CurrentStatus = statuses.Count == 0
            ? null
            : statuses.Contains(MonthlyReturnStatus.Approved)
                ? MonthlyReturnStatus.Approved
                : statuses.Contains(MonthlyReturnStatus.Submitted)
                    ? MonthlyReturnStatus.Submitted
                    : MonthlyReturnStatus.Draft;

        ValidationHistory = await _dbContext.MonthlyReturnValidations
            .AsNoTracking()
            .Where(validation => validation.MonthlyReturn.Year == Year && validation.MonthlyReturn.Month == Month)
            .OrderByDescending(validation => validation.ValidatedAt)
            .Select(validation => new ValidationEntry
            {
                Status = validation.Status,
                ValidatorName = validation.ValidatedByUser.DisplayName,
                ValidatedAt = validation.ValidatedAt,
                Notes = validation.Notes
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<List<FacilityVisit>> LoadVisitsAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.FacilityVisits
            .AsNoTracking()
            .Include(visit => visit.VitalSignsReading)
            .Where(visit => visit.VisitAt >= StartOfMonth() && visit.VisitAt < StartOfNextMonth())
            .ToListAsync(cancellationToken);
    }

    private List<DiagnosisSummary> BuildDiagnosisSummary(IEnumerable<FacilityVisit> visits)
    {
        return visits
            .GroupBy(visit => string.IsNullOrWhiteSpace(visit.Diagnosis) ? "Not recorded" : visit.Diagnosis.Trim())
            .Select(group => new DiagnosisSummary
            {
                Name = group.Key,
                VisitCount = group.Count()
            })
            .ToList();
    }

    private bool ValidatePeriod()
    {
        if (Year is < 2000 or > 2100)
        {
            ModelState.AddModelError(nameof(Year), "Enter a valid year.");
        }

        if (Month is < 1 or > 12)
        {
            ModelState.AddModelError(nameof(Month), "Select a valid month.");
        }

        return ModelState.IsValid;
    }

    private DateTimeOffset StartOfMonth() =>
        new(Year, Month, 1, 0, 0, 0, TimeSpan.Zero);

    private DateTimeOffset StartOfNextMonth() => StartOfMonth().AddMonths(1);

    private static bool IsComplete(FacilityVisit visit) =>
        visit.VitalSignsReading?.SystolicBloodPressure.HasValue == true &&
        visit.VitalSignsReading.DiastolicBloodPressure.HasValue &&
        visit.VitalSignsReading.PulseRate.HasValue &&
        visit.VitalSignsReading.TemperatureCelsius.HasValue &&
        !string.IsNullOrWhiteSpace(visit.Diagnosis);

    public sealed class DiagnosisSummary
    {
        public string Name { get; init; } = string.Empty;

        public int VisitCount { get; init; }
    }

    public sealed class ValidationEntry
    {
        public MonthlyReturnValidationStatus Status { get; init; }

        public string ValidatorName { get; init; } = string.Empty;

        public DateTimeOffset ValidatedAt { get; init; }

        public string? Notes { get; init; }
    }
}
