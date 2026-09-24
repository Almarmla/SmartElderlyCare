using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartElderlyCare.Data;
using SmartElderlyCare.Models;
using SmartElderlyCare.Services;

namespace SmartElderlyCare.Controllers;

[Authorize(AuthenticationSchemes = "AdminCookie,Identity.Application")]
public class PatientsController : Controller
{
    private const string FamilyLinkRoles =
        ApplicationRoles.Nurse + "," +
        ApplicationRoles.FacilityNurse + "," +
        ApplicationRoles.DhioAdmin + "," +
        ApplicationRoles.Administrator;

    private readonly ApplicationDbContext _dbContext;
    private readonly IPatientRiskEvaluator _riskEvaluator;
    private readonly UserManager<ApplicationUser> _userManager;

    public PatientsController(
        ApplicationDbContext dbContext,
        IPatientRiskEvaluator riskEvaluator,
        UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _riskEvaluator = riskEvaluator;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, CancellationToken cancellationToken)
    {
        var isFamilyUser = User.IsInRole(ApplicationRoles.Family);
        var query = _dbContext.Patients
            .AsNoTracking()
            .Where(patient => patient.IsActive);

        if (isFamilyUser)
        {
            var currentUserId = _userManager.GetUserId(User);
            query = query.Where(patient =>
                _dbContext.PatientFamilyMembers.Any(link =>
                    link.PatientId == patient.Id
                    && link.FamilyMemberUserId == currentUserId
                    && link.IsActive));
        }

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
        ViewData["IsFamilyView"] = isFamilyUser;
        return View(patients);
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (User.IsInRole(ApplicationRoles.Family))
        {
            return Forbid();
        }

        return View(new PatientRegistrationInputModel());
    }

    [HttpGet]
    public async Task<IActionResult> History(
        int id,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken)
    {
        if (User.IsInRole(ApplicationRoles.Family))
        {
            var currentUserId = _userManager.GetUserId(User);
            var hasAccess = await _dbContext.PatientFamilyMembers
                .AnyAsync(link =>
                    link.PatientId == id
                    && link.FamilyMemberUserId == currentUserId
                    && link.IsActive,
                    cancellationToken);
            if (!hasAccess)
            {
                return Forbid();
            }
        }

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
                .ToListAsync(cancellationToken),
            FamilyMembers = await _dbContext.PatientFamilyMembers
                .AsNoTracking()
                .Include(link => link.FamilyMemberUser)
                .Where(link => link.PatientId == id && link.IsActive)
                .OrderBy(link => link.RelationshipToPatient)
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
        if (User.IsInRole(ApplicationRoles.Family))
        {
            return Forbid();
        }

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

    [Authorize(Roles = FamilyLinkRoles)]
    [HttpGet]
    public async Task<IActionResult> LinkFamilyMember(CancellationToken cancellationToken)
    {
        return View(await BuildFamilyLinkPageAsync(cancellationToken));
    }

    [Authorize(Roles = FamilyLinkRoles)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LinkFamilyMember(
        FamilyMemberLinkInputModel model,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var patient = await _dbContext.Patients
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == model.PatientId && item.IsActive, cancellationToken);
            var familyMember = await _userManager.FindByIdAsync(model.FamilyMemberUserId);
            if (patient is null
                || familyMember is null
                || !await _userManager.IsInRoleAsync(familyMember, ApplicationRoles.Family))
            {
                ModelState.AddModelError(string.Empty, "The selected patient or family member account is not available.");
            }
            else
            {
                var existing = await _dbContext.PatientFamilyMembers
                    .SingleOrDefaultAsync(link =>
                        link.PatientId == model.PatientId
                        && link.FamilyMemberUserId == model.FamilyMemberUserId,
                        cancellationToken);

                if (existing is not null && existing.IsActive)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        $"{familyMember.DisplayName} is already linked to {patient.FirstName} {patient.LastName}.");
                }
                else
                {
                    if (existing is null)
                    {
                        _dbContext.PatientFamilyMembers.Add(new PatientFamilyMember
                        {
                            PatientId = model.PatientId,
                            FamilyMemberUserId = model.FamilyMemberUserId,
                            AccessLevel = model.AccessLevel,
                            RelationshipToPatient = NormalizeRelationship(model.RelationshipToPatient),
                            AddedAt = DateTimeOffset.UtcNow,
                            IsActive = true
                        });
                    }
                    else
                    {
                        existing.IsActive = true;
                        existing.AccessLevel = model.AccessLevel;
                        existing.RelationshipToPatient = NormalizeRelationship(model.RelationshipToPatient);
                        existing.AddedAt = DateTimeOffset.UtcNow;
                    }

                    await _dbContext.SaveChangesAsync(cancellationToken);
                    TempData["FamilyLinkMessage"] =
                        $"{familyMember.DisplayName} was linked to {patient.FirstName} {patient.LastName} with {model.AccessLevel} access.";
                    return RedirectToAction(nameof(LinkFamilyMember));
                }
            }
        }

        return View(await BuildFamilyLinkPageAsync(cancellationToken, model));
    }

    [Authorize(Roles = FamilyLinkRoles)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeFamilyLink(
        int patientId,
        string familyMemberUserId,
        CancellationToken cancellationToken)
    {
        var link = await _dbContext.PatientFamilyMembers
            .SingleOrDefaultAsync(item =>
                item.PatientId == patientId
                && item.FamilyMemberUserId == familyMemberUserId,
                cancellationToken);
        if (link is not null && link.IsActive)
        {
            link.IsActive = false;
            await _dbContext.SaveChangesAsync(cancellationToken);
            TempData["FamilyLinkMessage"] =
                "The family link was revoked. The family member can no longer view this patient.";
        }

        return RedirectToAction(nameof(LinkFamilyMember));
    }

    private async Task<FamilyLinkPageViewModel> BuildFamilyLinkPageAsync(
        CancellationToken cancellationToken,
        FamilyMemberLinkInputModel? input = null)
    {
        return new FamilyLinkPageViewModel
        {
            Input = input ?? new FamilyMemberLinkInputModel(),
            Patients = await _dbContext.Patients
                .AsNoTracking()
                .Where(patient => patient.IsActive)
                .OrderBy(patient => patient.LastName)
                .ThenBy(patient => patient.FirstName)
                .ToListAsync(cancellationToken),
            FamilyMembers = (await _userManager.GetUsersInRoleAsync(ApplicationRoles.Family))
                .Where(user => user.IsActive)
                .OrderBy(user => user.DisplayName)
                .ToList(),
            ExistingLinks = await _dbContext.PatientFamilyMembers
                .AsNoTracking()
                .Include(link => link.Patient)
                .Include(link => link.FamilyMemberUser)
                .OrderByDescending(link => link.AddedAt)
                .ToListAsync(cancellationToken)
        };
    }

    private static string? NormalizeRelationship(string? relationship)
    {
        return string.IsNullOrWhiteSpace(relationship) ? null : relationship.Trim();
    }
}
