using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SmartElderlyCare.Data;
using SmartElderlyCare.Models;

namespace SmartElderlyCare.Pages.Vhw;

[Authorize(Roles = "VHW")]
public class WelfareCheckModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;

    public WelfareCheckModel(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public SelectList Patients { get; private set; } = null!;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public async Task OnGetAsync(int? patientId, CancellationToken cancellationToken)
    {
        await LoadPatientsAsync(cancellationToken);

        if (patientId.HasValue && await _dbContext.Patients.AnyAsync(
                patient => patient.Id == patientId.Value && patient.IsActive,
                cancellationToken))
        {
            Input.PatientId = patientId.Value;
        }
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadPatientsAsync(cancellationToken);
            return Page();
        }

        var patientExists = await _dbContext.Patients
            .AnyAsync(patient => patient.Id == Input.PatientId, cancellationToken);
        if (!patientExists)
        {
            ModelState.AddModelError(nameof(Input.PatientId), "Please select a valid patient.");
            await LoadPatientsAsync(cancellationToken);
            return Page();
        }

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        _dbContext.VhwWelfareChecks.Add(new VhwWelfareCheck
        {
            PatientId = Input.PatientId,
            RecordedByUserId = userId,
            Mobility = Input.Mobility,
            MedicationTaken = Input.MedicationTaken,
            Status = Input.GeneralCondition,
            Notes = Input.Notes,
            ObservedAt = DateTimeOffset.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Welfare check saved.";
        return RedirectToPage();
    }

    private async Task LoadPatientsAsync(CancellationToken cancellationToken)
    {
        var patients = await _dbContext.Patients
            .AsNoTracking()
            .Where(patient => patient.IsActive)
            .OrderBy(patient => patient.LastName)
            .ThenBy(patient => patient.FirstName)
            .Select(patient => new
            {
                patient.Id,
                Name = $"{patient.FirstName} {patient.LastName}"
            })
            .ToListAsync(cancellationToken);

        Patients = new SelectList(patients, nameof(InputModel.PatientId), "Name");
    }

    public class InputModel
    {
        [Required(ErrorMessage = "Please choose a patient.")]
        [Display(Name = "Patient")]
        public int PatientId { get; set; }

        [Required(ErrorMessage = "Please choose mobility.")]
        public string Mobility { get; set; } = string.Empty;

        [Display(Name = "Medication taken")]
        public bool MedicationTaken { get; set; }

        [Required(ErrorMessage = "Please choose the general condition.")]
        [Display(Name = "General condition")]
        public VhwWelfareStatus GeneralCondition { get; set; }

        [StringLength(2000)]
        public string? Notes { get; set; }
    }
}
