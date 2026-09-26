using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartElderlyCare.Data;
using SmartElderlyCare.Models;
using SmartElderlyCare.Services;

namespace SmartElderlyCare.Controllers;

[Authorize]
public class PatientsController : Controller
{
    private const string FamilyLinkRoles =
        ApplicationRoles.Nurse + "," +
        ApplicationRoles.FacilityNurse + "," +
        ApplicationRoles.DhioAdmin + "," +
        ApplicationRoles.Administrator;

    private const string AllowedStaffRoles =
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
    public async Task<IActionResult> Index(
        string? search,
        bool showArchived = false,
        CancellationToken cancellationToken = default)
    {
        var isFamilyUser = User.IsInRole(ApplicationRoles.Family);
        var query = _dbContext.Patients
            .AsNoTracking()
            .Where(patient => patient.IsActive == !showArchived);

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
        ViewData["ShowArchived"] = showArchived;
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
    [Route("patients/{id:int}/history")]
    public async Task<IActionResult> History(
        int id,
        DateTime? startDate,
        DateTime? endDate,
        bool allTime = false,
        CancellationToken cancellationToken = default)
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

        // Archived records stay readable so clinical staff and linked family
        // members can still review history; the view flags the archived state.
        var patient = await _dbContext.Patients
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (patient is null)
        {
            return NotFound();
        }

        ViewData["IsArchived"] = !patient.IsActive;

        // With no explicit period the whole record is shown, so the timeline lists
        // every activity ever captured for this patient.
        var showAllTime = allTime || (!startDate.HasValue && !endDate.HasValue);
        DateTimeOffset startOffset;
        DateTimeOffset endOffset;
        var periodStart = (startDate ?? DateTime.Today.AddMonths(-3)).Date;
        var periodEnd = (endDate ?? DateTime.Today).Date;

        if (showAllTime)
        {
            startOffset = DateTimeOffset.MinValue;
            endOffset = DateTimeOffset.MaxValue;
        }
        else if (periodStart > periodEnd)
        {
            ModelState.AddModelError(string.Empty, "The start date must be on or before the end date.");
            showAllTime = true;
            startOffset = DateTimeOffset.MinValue;
            endOffset = DateTimeOffset.MaxValue;
        }
        else
        {
            // The date inputs are inclusive of the end day, so step one past it.
            startOffset = new DateTimeOffset(periodStart, TimeSpan.Zero);
            endOffset = new DateTimeOffset(periodEnd.AddDays(1), TimeSpan.Zero);
        }

        var facilityVisits = await _dbContext.FacilityVisits
            .AsNoTracking()
            .Where(visit => visit.PatientId == id && visit.VisitAt >= startOffset && visit.VisitAt < endOffset)
            .OrderByDescending(visit => visit.VisitAt)
            .ToListAsync(cancellationToken);
        var welfareChecks = await _dbContext.VhwWelfareChecks
            .AsNoTracking()
            .Where(check => check.PatientId == id && check.ObservedAt >= startOffset && check.ObservedAt < endOffset)
            .OrderByDescending(check => check.ObservedAt)
            .ToListAsync(cancellationToken);
        var alerts = await _dbContext.Alerts
            .AsNoTracking()
            .Where(alert => alert.PatientId == id && alert.CreatedAt >= startOffset && alert.CreatedAt < endOffset)
            .OrderByDescending(alert => alert.CreatedAt)
            .ToListAsync(cancellationToken);

        var model = new PatientHistoryViewModel
        {
            Patient = patient,
            StartDate = periodStart,
            EndDate = periodEnd,
            ShowAllTime = showAllTime,
            Readings = await _dbContext.VitalSignsReadings
                .AsNoTracking()
                .Where(reading => reading.PatientId == id && reading.RecordedAt >= startOffset && reading.RecordedAt < endOffset)
                .OrderByDescending(reading => reading.RecordedAt)
                .ToListAsync(cancellationToken),
            WelfareChecks = welfareChecks,
            FacilityVisits = facilityVisits,
            Alerts = alerts,
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
                .ToListAsync(cancellationToken),
            Activities = BuildActivityTimeline(facilityVisits, welfareChecks, alerts)
        };

        ViewData["PatientRiskResult"] = await _riskEvaluator.EvaluateAsync(id, cancellationToken);
        return View(model);
    }

    private static List<PatientActivity> BuildActivityTimeline(
        IReadOnlyList<FacilityVisit> facilityVisits,
        IReadOnlyList<VhwWelfareCheck> welfareChecks,
        IReadOnlyList<Alert> alerts)
    {
        var activities = new List<PatientActivity>(
            facilityVisits.Count + welfareChecks.Count + alerts.Count);

        activities.AddRange(facilityVisits.Select(visit => new PatientActivity
        {
            OccurredAt = visit.VisitAt,
            Type = PatientActivityType.FacilityVisit,
            Title = string.IsNullOrWhiteSpace(visit.FacilityName)
                ? "Facility visit"
                : $"Facility visit · {visit.FacilityName}",
            Description = string.Join(
                " — ",
                new[] { visit.Diagnosis, visit.Treatment, visit.ClinicalNotes }
                    .Where(part => !string.IsNullOrWhiteSpace(part)))
        }));

        activities.AddRange(welfareChecks.Select(check => new PatientActivity
        {
            OccurredAt = check.ObservedAt,
            Type = PatientActivityType.VhwWelfareCheck,
            Title = $"VHW welfare check · {check.Status}",
            Description = string.Join(
                " — ",
                new[]
                {
                    check.Mobility,
                    check.Mood,
                    check.Nutrition,
                    check.SafetyConcern,
                    check.MedicationTaken ? "Medication taken" : "Medication not taken",
                    check.Notes
                }
                    .Where(part => !string.IsNullOrWhiteSpace(part)))
        }));

        activities.AddRange(alerts.Select(alert => new PatientActivity
        {
            OccurredAt = alert.CreatedAt,
            Type = PatientActivityType.Alert,
            Title = $"Alert · {alert.Type} ({alert.Severity})",
            Description = alert.Message
        }));

        return activities
            .OrderByDescending(activity => activity.OccurredAt)
            .ToList();
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

    [Authorize(Roles = AllowedStaffRoles)]
    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        // Archived records stay editable so the record status switch can also be
        // used to restore a patient; the view flags the archived state.
        var patient = await _dbContext.Patients
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (patient is null)
        {
            return NotFound();
        }

        ViewData["IsArchived"] = !patient.IsActive;

        var model = new PatientEditInputModel
        {
            Id = patient.Id,
            FirstName = patient.FirstName,
            LastName = patient.LastName,
            DateOfBirth = patient.DateOfBirth,
            Sex = patient.Sex,
            NationalId = patient.NationalId,
            MedicalCondition = patient.MedicalCondition ?? string.Empty,
            ChronicCareProgramme = patient.ChronicCareProgramme ?? string.Empty,
            PhoneNumber = patient.PhoneNumber,
            Address = patient.Address,
            EmergencyContactName = patient.EmergencyContactName ?? string.Empty,
            EmergencyContactPhone = patient.EmergencyContactPhone ?? string.Empty,
            IsActive = patient.IsActive
        };

        return View(model);
    }

    [Authorize(Roles = AllowedStaffRoles)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PatientEditInputModel model, CancellationToken cancellationToken)
    {
        if (model.DateOfBirth > DateOnly.FromDateTime(DateTime.Today))
        {
            ModelState.AddModelError(nameof(model.DateOfBirth), "Date of birth cannot be in the future.");
        }

        var patient = await _dbContext.Patients
            .SingleOrDefaultAsync(item => item.Id == model.Id, cancellationToken);
        if (patient is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var wasArchived = !patient.IsActive;
        patient.FirstName = model.FirstName.Trim();
        patient.LastName = model.LastName.Trim();
        patient.DateOfBirth = model.DateOfBirth;
        patient.Sex = string.IsNullOrWhiteSpace(model.Sex) ? null : model.Sex.Trim();
        patient.NationalId = model.NationalId?.Trim();
        patient.MedicalCondition = model.MedicalCondition.Trim();
        patient.ChronicCareProgramme = model.ChronicCareProgramme.Trim();
        patient.PhoneNumber = model.PhoneNumber?.Trim();
        patient.Address = model.Address?.Trim();
        patient.EmergencyContactName = model.EmergencyContactName.Trim();
        patient.EmergencyContactPhone = model.EmergencyContactPhone.Trim();
        patient.IsActive = model.IsActive;

        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = wasArchived && patient.IsActive
            ? $"{patient.FirstName} {patient.LastName} was unarchived / restored."
            : "Patient details updated successfully.";
        return RedirectToAction(nameof(History), new { id = patient.Id });
    }

    [Authorize(Roles = AllowedStaffRoles)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id, CancellationToken cancellationToken)
    {
        var patient = await _dbContext.Patients
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (patient is null)
        {
            return NotFound();
        }

        patient.IsActive = !patient.IsActive;
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = patient.IsActive
            ? $"{patient.FirstName} {patient.LastName} was unarchived / restored."
            : $"{patient.FirstName} {patient.LastName} was archived.";

        // Land on the tab the patient now belongs to.
        return RedirectToAction(nameof(Index), new { showArchived = !patient.IsActive });
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
        [Bind(Prefix = "Input")] FamilyMemberLinkInputModel model,
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
