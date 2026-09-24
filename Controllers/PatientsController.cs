using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartElderlyCare.Data;
using SmartElderlyCare.Models;
using SmartElderlyCare.Services;

namespace SmartElderlyCare.Controllers;

[Authorize(AuthenticationSchemes = "AdminCookie,Identity.Application")]
public class PatientsController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPatientRiskEvaluator _riskEvaluator;

    public PatientsController(ApplicationDbContext dbContext, IPatientRiskEvaluator riskEvaluator)
    {
        _dbContext = dbContext;
        _riskEvaluator = riskEvaluator;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, CancellationToken cancellationToken)
    {
        var query = _dbContext.Patients
            .AsNoTracking()
            .Where(patient => patient.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(patient =>
                patient.PatientNumber.Contains(term)
                || patient.FirstName.Contains(term)
                || patient.LastName.Contains(term)
                || (patient.MedicalCondition != null && patient.MedicalCondition.Contains(term)));
        }

        var patients = await query
            .OrderBy(patient => patient.LastName)
            .ThenBy(patient => patient.FirstName)
            .ToListAsync(cancellationToken);

        ViewData["PatientRiskLevels"] = await _riskEvaluator.EvaluateBatchAsync(
            patients.Select(patient => patient.Id),
            cancellationToken);
        ViewData["Search"] = search;
        return View(patients);
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View(new PatientRegistrationInputModel());
    }

    [HttpGet]
    public async Task<IActionResult> History(
        int id,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken)
    {
        var patient = await _dbContext.Patients
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && item.IsActive, cancellationToken);
        if (patient is null)
        {
            return NotFound();
        }

        var end = (endDate ?? DateTime.Today).Date.AddDays(1);
        var start = (startDate ?? DateTime.Today.AddMonths(-3)).Date;
        if (start >= end)
        {
            ModelState.AddModelError(string.Empty, "The start date must be before the end date.");
            start = end.AddMonths(-3).Date;
        }
        var startOffset = new DateTimeOffset(start, TimeSpan.Zero);
        var endOffset = new DateTimeOffset(end, TimeSpan.Zero);

        var model = new PatientHistoryViewModel
        {
            Patient = patient,
            StartDate = start,
            EndDate = end.AddDays(-1),
            Readings = await _dbContext.VitalSignsReadings
                .AsNoTracking()
                .Where(reading => reading.PatientId == id && reading.RecordedAt >= startOffset && reading.RecordedAt < endOffset)
                .OrderByDescending(reading => reading.RecordedAt)
                .ToListAsync(cancellationToken),
            WelfareChecks = await _dbContext.VhwWelfareChecks
                .AsNoTracking()
                .Where(check => check.PatientId == id && check.ObservedAt >= startOffset && check.ObservedAt < endOffset)
                .OrderByDescending(check => check.ObservedAt)
                .ToListAsync(cancellationToken),
            WelfareObservations = await _dbContext.WelfareObservations
                .AsNoTracking()
                .Where(observation => observation.PatientId == id && observation.ObservedAt >= startOffset && observation.ObservedAt < endOffset)
                .OrderByDescending(observation => observation.ObservedAt)
                .ToListAsync(cancellationToken),
            FacilityVisits = await _dbContext.FacilityVisits
                .AsNoTracking()
                .Where(visit => visit.PatientId == id && visit.VisitAt >= startOffset && visit.VisitAt < endOffset)
                .OrderByDescending(visit => visit.VisitAt)
                .ToListAsync(cancellationToken),
            Alerts = await _dbContext.Alerts
                .AsNoTracking()
                .Where(alert => alert.PatientId == id && alert.CreatedAt >= startOffset && alert.CreatedAt < endOffset)
                .OrderByDescending(alert => alert.CreatedAt)
                .ToListAsync(cancellationToken),
            Medications = await _dbContext.PatientMedications
                .AsNoTracking()
                .Where(medication => medication.PatientId == id && medication.IsActive)
                .OrderByDescending(medication => medication.PrescribedAt)
                .ToListAsync(cancellationToken)
        };

        ViewData["PatientRiskResult"] = await _riskEvaluator.EvaluateAsync(id, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(
        PatientRegistrationInputModel model,
        CancellationToken cancellationToken)
    {
        if (model.DateOfBirth > DateOnly.FromDateTime(DateTime.Today))
        {
            ModelState.AddModelError(nameof(model.DateOfBirth), "Date of birth cannot be in the future.");
        }

        var patientNumberExists = await _dbContext.Patients
            .AnyAsync(patient => patient.PatientNumber == model.PatientNumber, cancellationToken);
        if (patientNumberExists)
        {
            ModelState.AddModelError(nameof(model.PatientNumber), "This patient number is already registered.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var patient = new Patient
        {
            PatientNumber = model.PatientNumber.Trim(),
            FirstName = model.FirstName.Trim(),
            LastName = model.LastName.Trim(),
            DateOfBirth = model.DateOfBirth,
            Sex = model.Sex,
            NationalId = model.NationalId?.Trim(),
            MedicalCondition = model.MedicalCondition.Trim(),
            ChronicCareProgramme = model.ChronicCareProgramme.Trim(),
            PhoneNumber = model.PhoneNumber?.Trim(),
            Address = model.Address?.Trim(),
            EmergencyContactName = model.EmergencyContactName.Trim(),
            EmergencyContactPhone = model.EmergencyContactPhone.Trim()
        };

        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = $"{patient.FirstName} {patient.LastName} was registered successfully.";
        return RedirectToAction(nameof(Index));
    }
}
