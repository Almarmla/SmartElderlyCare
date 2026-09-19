using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartElderlyCare.Services;

namespace SmartElderlyCare.Pages.Hiu;

[Authorize(Roles = "HiuClerk,RecordsStaff,DhioAdmin,Dmo")]
public class MonthlyReportModel : PageModel
{
    private readonly Dhis2MonthlyExportService _exportService;

    public MonthlyReportModel(Dhis2MonthlyExportService exportService)
    {
        _exportService = exportService;
    }

    [BindProperty(SupportsGet = true)]
    public int Year { get; set; } = DateTime.UtcNow.Year;

    [BindProperty(SupportsGet = true)]
    public int Month { get; set; } = DateTime.UtcNow.Month;

    [BindProperty(SupportsGet = true)]
    public string OrganisationUnit { get; set; } = "SMART-ELDERLY-CARE";

    public IActionResult OnGet()
    {
        if (Month is < 1 or > 12)
        {
            ModelState.AddModelError(nameof(Month), "Month must be between 1 and 12.");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostDownloadAsync(CancellationToken cancellationToken)
    {
        if (Month is < 1 or > 12 || Year is < 2000 or > 2100 || string.IsNullOrWhiteSpace(OrganisationUnit))
        {
            ModelState.AddModelError(string.Empty, "Enter a valid year, month, and organisation unit.");
            return Page();
        }

        var csv = await _exportService.ExportMonthAsCsvAsync(Year, Month, OrganisationUnit.Trim(), cancellationToken);
        return File(csv, "text/csv", $"dhis2-monthly-return-{Year:0000}-{Month:00}.csv");
    }
}
