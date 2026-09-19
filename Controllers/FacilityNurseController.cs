using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    public IActionResult RecordVisit()
    {
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
            BloodGlucoseMgDl = model.BloodGlucoseMgDl,
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
        await _alertService.CheckThresholds(reading, cancellationToken);

        TempData["SuccessMessage"] = "The facility visit was recorded successfully.";
        return RedirectToAction(nameof(RecordVisit));
    }
}
