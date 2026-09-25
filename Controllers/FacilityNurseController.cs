using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SmartElderlyCare.Data;
using SmartElderlyCare.Models;
using SmartElderlyCare.Services;

namespace SmartElderlyCare.Controllers;

[Authorize(Roles = "Nurse,FacilityNurse")]
public class FacilityNurseController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly AlertService _alertService;

    public FacilityNurseController(ApplicationDbContext dbContext, AlertService alertService)
    {
        _dbContext = dbContext;
        _alertService = alertService;
    }

    [HttpGet]
    public async Task<IActionResult> RecordVisit(CancellationToken cancellationToken)
    {
        await LoadPatientsAsync(cancellationToken);
        return View(new FacilityVisitInputModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordVisit(FacilityVisitInputModel model, CancellationToken cancellationToken)
    {
        if (model.DiastolicBloodPressure >= model.SystolicBloodPressure)
        {
            ModelState.AddModelError(
                nameof(model.DiastolicBloodPressure),
                "Diastolic pressure must be lower than systolic pressure.");
        }

        var patientExists = await _dbContext.Patients
            .AnyAsync(patient => patient.Id == model.PatientId, cancellationToken);

        if (!patientExists)
        {
            ModelState.AddModelError(nameof(model.PatientId), "The selected patient could not be found.");
        }

        if (!ModelState.IsValid)
        {
            await LoadPatientsAsync(cancellationToken);
            return View(model);
        }

        var reading = new VitalSignsReading
        {
            PatientId = model.PatientId,
            RecordedByUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            SystolicBloodPressure = model.SystolicBloodPressure,
            DiastolicBloodPressure = model.DiastolicBloodPressure,
            PulseRate = model.PulseRate,
            TemperatureCelsius = model.TemperatureCelsius,
            RespiratoryRate = model.RespiratoryRate,
            OxygenSaturation = model.OxygenSaturation,
            BloodGlucoseMgDl = model.BloodGlucoseMgDl,
            WeightKilograms = model.WeightKilograms,
            RecordedAt = DateTimeOffset.UtcNow
        };

        var visit = new FacilityVisit
        {
            PatientId = model.PatientId,
            RecordedByUserId = reading.RecordedByUserId ?? string.Empty,
            VitalSignsReading = reading,
            FacilityName = model.FacilityName,
            Diagnosis = model.Diagnosis,
            Treatment = model.Treatment,
            ClinicalNotes = model.ClinicalNotes,
            VisitAt = DateTimeOffset.UtcNow,
            Status = FacilityVisitStatus.Completed
        };

        _dbContext.FacilityVisits.Add(visit);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Gap 5 fix: alert errors are caught and logged as a non-blocking warning
        // so a failure in alerting never crashes the nurse's successfully saved visit.
        try
        {
            await _alertService.CheckThresholds(reading, cancellationToken);
        }
        catch (Exception ex)
        {
            var logger = HttpContext.RequestServices
                .GetRequiredService<Microsoft.Extensions.Logging.ILogger<FacilityNurseController>>();
            logger.LogError(ex, "Alert generation failed for patient {PatientId} after visit was saved.", model.PatientId);
            TempData["WarningMessage"] = "The facility visit was saved, but alert generation encountered an error. Please notify an administrator.";
            return RedirectToAction(nameof(RecordVisit));
        }

        TempData["SuccessMessage"] = "The facility visit was recorded successfully.";
        return RedirectToAction(nameof(RecordVisit));
    }

    [HttpGet]
    public async Task<IActionResult> Medications(int? patientId, CancellationToken cancellationToken)
    {
        await LoadPatientsAsync(cancellationToken);
        ViewData["Medications"] = await LoadMedicationsAsync(patientId, cancellationToken);
        return View(new MedicationInputModel { PatientId = patientId ?? 0 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Medications(MedicationInputModel model, CancellationToken cancellationToken)
    {
        var patientExists = await _dbContext.Patients
            .AnyAsync(patient => patient.Id == model.PatientId && patient.IsActive, cancellationToken);

        if (!patientExists)
        {
            ModelState.AddModelError(nameof(model.PatientId), "The selected patient could not be found.");
        }

        if (!ModelState.IsValid)
        {
            await LoadPatientsAsync(cancellationToken);
            ViewData["Medications"] = await LoadMedicationsAsync(model.PatientId, cancellationToken);
            return View(model);
        }

        _dbContext.PatientMedications.Add(new PatientMedication
        {
            PatientId = model.PatientId,
            RecordedByUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            MedicationName = model.MedicationName.Trim(),
            Dosage = model.Dosage?.Trim(),
            Frequency = model.Frequency?.Trim(),
            Route = model.Route?.Trim(),
            IsActive = model.IsActive,
            Notes = model.Notes?.Trim(),
            PrescribedAt = DateTimeOffset.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "The medication was logged successfully.";
        return RedirectToAction(nameof(Medications), new { patientId = model.PatientId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DiscontinueMedication(long medicationId, int patientId, CancellationToken cancellationToken)
    {
        var medication = await _dbContext.PatientMedications
            .SingleOrDefaultAsync(
                item => item.Id == medicationId && item.PatientId == patientId,
                cancellationToken);

        if (medication is null)
        {
            TempData["SuccessMessage"] = "The medication could not be found.";
            return RedirectToAction(nameof(Medications), new { patientId });
        }

        var userName = User.FindFirst("DisplayName")?.Value
            ?? User.Identity?.Name
            ?? "Unknown user";

        medication.IsActive = false;
        medication.Notes = string.Join(
            " ",
            new[] { medication.Notes?.Trim() }
                .Where(notes => !string.IsNullOrWhiteSpace(notes))
                .Append($"Discontinued on {DateTimeOffset.UtcNow:g} by {userName}."));

        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = $"{medication.MedicationName} was discontinued.";
        return RedirectToAction(nameof(Medications), new { patientId });
    }

    private Task<List<PatientMedication>> LoadMedicationsAsync(int? patientId, CancellationToken cancellationToken)
    {
        if (!patientId.HasValue)
        {
            return Task.FromResult(new List<PatientMedication>());
        }

        return _dbContext.PatientMedications
            .AsNoTracking()
            .Where(medication => medication.PatientId == patientId.Value)
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
                FullName = $"{patient.FirstName} {patient.LastName} ({patient.PatientNumber})"
            })
            .ToListAsync(cancellationToken);

        ViewData["Patients"] = new SelectList(patients, nameof(PatientOption.Id), nameof(PatientOption.FullName));
    }

    private sealed class PatientOption
    {
        public int Id { get; init; }

        public string FullName { get; init; } = string.Empty;
    }
}
