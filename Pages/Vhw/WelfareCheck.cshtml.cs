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

    public IReadOnlyList<PatientMedication> Medications { get; private set; } = new List<PatientMedication>();

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
            await LoadMedicationsAsync(patientId.Value, cancellationToken);
        }
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadPatientsAsync(cancellationToken);
            return Page();
        }

        var patient = await _dbContext.Patients
            .FirstOrDefaultAsync(patient => patient.Id == Input.PatientId, cancellationToken);
        if (patient is null)
        {
            ModelState.AddModelError(nameof(Input.PatientId), "Please select a valid patient.");
            await LoadPatientsAsync(cancellationToken);
            return Page();
        }

        if (Input.FollowUpRequired && Input.FollowUpAt.HasValue && Input.FollowUpAt.Value <= DateTimeOffset.UtcNow)
        {
            ModelState.AddModelError(nameof(Input.FollowUpAt), "The follow-up date must be in the future.");
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
            Mood = Input.Mood,
            Nutrition = Input.Nutrition,
            SafetyConcern = Input.SafetyConcern,
            FollowUpRequired = Input.FollowUpRequired,
            FollowUpAt = Input.FollowUpAt,
            Notes = Input.Notes,
            ObservedAt = DateTimeOffset.UtcNow
        });

        if (Input.GeneralCondition == VhwWelfareStatus.Urgent || Input.FollowUpRequired)
        {
            _dbContext.Alerts.Add(new Alert
            {
                Type = AlertType.Welfare,
                Severity = Input.GeneralCondition == VhwWelfareStatus.Urgent ? AlertSeverity.Critical : AlertSeverity.High,
                Message = $"VHW Welfare Check Urgent: {patient.FirstName} {patient.LastName} reported urgent welfare concerns ({Input.Notes}).",
                PatientId = Input.PatientId,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Welfare check saved.";
        return RedirectToPage(new { patientId = Input.PatientId });
    }

    private async Task LoadMedicationsAsync(int patientId, CancellationToken cancellationToken)
    {
        Medications = await _dbContext.PatientMedications
            .AsNoTracking()
            .Where(medication => medication.PatientId == patientId)
            .OrderBy(medication => medication.IsActive ? 0 : 1)
            .ThenByDescending(medication => medication.PrescribedAt)
            .ToListAsync(cancellationToken);
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

        [Display(Name = "Mood")]
        public string? Mood { get; set; }

        [Display(Name = "Nutrition")]
        public string? Nutrition { get; set; }

        [Display(Name = "Safety concern")]
        public string? SafetyConcern { get; set; }

        [Display(Name = "Follow-up required")]
        public bool FollowUpRequired { get; set; }

        [Display(Name = "Follow-up date")]
        [DataType(DataType.DateTime)]
        public DateTimeOffset? FollowUpAt { get; set; }

        [StringLength(2000)]
        public string? Notes { get; set; }
    }
}
