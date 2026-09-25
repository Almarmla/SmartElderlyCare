using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartElderlyCare.Data;
using SmartElderlyCare.Models;

namespace SmartElderlyCare.Controllers;

[Authorize]
public class WorkspaceController : Controller
{
    private readonly ApplicationDbContext _dbContext;

    public WorkspaceController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var role = ResolveRole();
        var viewModel = new RoleWorkspaceViewModel
        {
            DisplayName = User.Identity?.Name ?? "Care team member",
            RoleName = role,
            Actions = ActionsFor(role)
        };

        if (role == ApplicationRoles.Family)
        {
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var linkedPatients = await _dbContext.PatientFamilyMembers
                .AsNoTracking()
                .Include(link => link.Patient)
                .Where(link => link.FamilyMemberUserId == currentUserId && link.IsActive)
                .OrderBy(link => link.Patient.LastName)
                .ThenBy(link => link.Patient.FirstName)
                .ToListAsync(cancellationToken);

            viewModel.LinkedPatients = linkedPatients
                .Select(link => new LinkedFamilyPatient(
                    link.PatientId,
                    $"{link.Patient.LastName}, {link.Patient.FirstName}",
                    link.Patient.PatientNumber,
                    link.Patient.MedicalCondition ?? "—",
                    link.AccessLevel,
                    link.RelationshipToPatient))
                .ToList();
        }

        return View(viewModel);
    }

    private string ResolveRole()
    {
        if (User.IsInRole(ApplicationRoles.Administrator)
            || User.IsInRole(ApplicationRoles.DhioAdmin)
            || User.HasClaim(ClaimTypes.Role, "Administrator"))
        {
            return ApplicationRoles.DhioAdmin;
        }

        foreach (var role in new[]
        {
            ApplicationRoles.FacilityNurse,
            ApplicationRoles.Nurse,
            ApplicationRoles.VHW,
            ApplicationRoles.HiuClerk,
            ApplicationRoles.RecordsStaff,
            ApplicationRoles.Family,
            ApplicationRoles.Dmo
        })
        {
            if (User.IsInRole(role))
            {
                return role;
            }
        }

        return "Care team";
    }

    private static IReadOnlyList<RoleWorkspaceAction> ActionsFor(string role) =>
        role switch
        {
            ApplicationRoles.DhioAdmin =>
            [
                new("Manage user accounts", "Create, edit, assign roles, deactivate, or reactivate users.", "", Page: "/Users/Index", Area: "Admin"),
                new("Configure thresholds", "Set safe ranges for each vital-sign metric and condition.", "", Page: "/Thresholds/Edit", Area: "Admin", Style: "purple"),
                new("Review audit log", "Inspect the trail of account and threshold changes.", "", Page: "/AuditLogs/Index", Area: "Admin", Style: "purple"),
                new("Link family members", "Grant registered family accounts access to a patient's history.", "Patients", "LinkFamilyMember", Style: "purple"),
                new("Review patient history", "Search registered patients and inspect their full capture history.", "Patients", "Index", Style: "blue")
            ],
            ApplicationRoles.FacilityNurse =>
            [
                new("Record facility visit", "Capture vital signs, diagnosis, and treatment for a patient.", "FacilityNurse", "RecordVisit"),
                new("Patient Medications", "Log prescribed medications and review active regimens.", "FacilityNurse", "Medications", Style: "purple"),
                new("Monthly Facility Returns", "Compile completeness and diagnosis totals for the selected reporting month.", "", Page: "/Returns/MonthlySummary", Style: "purple"),
                new("Find a patient", "Search a patient and review their previous readings and care captures.", "Patients", "Index", Style: "blue"),
                new("Register patient", "Add an elderly patient to the chronic care programme.", "Patients", "Register", Style: "purple"),
                new("Link family members", "Grant registered family accounts access to a patient's history.", "Patients", "LinkFamilyMember")
            ],
            ApplicationRoles.Nurse =>
            [
                new("Record facility visit", "Capture vital signs, diagnosis, and treatment for a patient.", "FacilityNurse", "RecordVisit"),
                new("Patient Medications", "Log prescribed medications and review active regimens.", "FacilityNurse", "Medications", Style: "purple"),
                new("Find a patient", "Search a patient and review their previous readings and care captures.", "Patients", "Index", Style: "blue"),
                new("Register patient", "Add an elderly patient to the chronic care programme.", "Patients", "Register", Style: "purple"),
                new("Link family members", "Grant registered family accounts access to a patient's history.", "Patients", "LinkFamilyMember")
            ],
            ApplicationRoles.VHW =>
            [
                new("Record welfare check", "Use the simple form to capture mobility, medication, condition, and notes.", "", Page: "/Vhw/WelfareCheck"),
                new("VHW Register", "Review active elderly patients, recent welfare status, and overdue visits.", "", Page: "/Vhw/Register", Style: "purple"),
                new("Find a patient", "Search a patient and review their previous welfare captures.", "Patients", "Index", Style: "blue")
            ],
            ApplicationRoles.HiuClerk =>
            [
                new("Open HIU dashboard", "Review recent readings and work through alerts by severity.", "", Page: "/Hiu/Dashboard"),
                new("Monthly DHIS2 report", "Export the selected month’s indicators as a DHIS2 CSV.", "", Page: "/Hiu/MonthlyReport", Style: "purple"),
                new("Find a patient", "Retrieve a patient’s complete capture history.", "Patients", "Index", Style: "blue")
            ],
            ApplicationRoles.RecordsStaff =>
            [
                new("Open HIU dashboard", "Review recent readings and work through alerts by severity.", "", Page: "/Hiu/Dashboard"),
                new("Monthly DHIS2 report", "Export the selected month’s indicators as a DHIS2 CSV.", "", Page: "/Hiu/MonthlyReport", Style: "purple"),
                new("Monthly Facility Returns", "Compile completeness and diagnosis totals for the selected reporting month.", "", Page: "/Returns/MonthlySummary", Style: "purple"),
                new("Find a patient", "Retrieve a patient’s complete capture history.", "Patients", "Index", Style: "blue")
            ],
            ApplicationRoles.Family =>
            [
                new("My linked relatives", "Review the care history your family account has been granted access to.", "Patients", "Index", Style: "blue")
            ],
            ApplicationRoles.Dmo =>
            [
                new("District Aggregate Dashboard", "Review high-level care indicators, facility volume, and morbidity.", "", Page: "/Reports/DistrictSummary", Style: "blue"),
                new("Monthly DHIS2 report", "Review the monthly indicator export prepared for district reporting.", "", Page: "/Hiu/MonthlyReport", Style: "purple")
            ],
            _ =>
            [
                new("Find a patient", "Search registered patients and inspect available care history.", "Patients", "Index")
            ]
        };
}
