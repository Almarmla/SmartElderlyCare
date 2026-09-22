using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartElderlyCare.Models;

namespace SmartElderlyCare.Controllers;

[Authorize(AuthenticationSchemes = "AdminCookie,Identity.Application")]
public class WorkspaceController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        var role = ResolveRole();
        return View(new RoleWorkspaceViewModel
        {
            DisplayName = User.Identity?.Name ?? "Care team member",
            RoleName = role,
            Actions = ActionsFor(role)
        });
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
                new("Review patient history", "Search registered patients and inspect their full capture history.", "Patients", "Index", Style: "blue")
            ],
            ApplicationRoles.FacilityNurse =>
            [
                new("Record facility visit", "Capture vital signs, diagnosis, and treatment for a patient.", "FacilityNurse", "RecordVisit"),
                new("Monthly Facility Returns", "Compile completeness and diagnosis totals for the selected reporting month.", "", Page: "/Returns/MonthlySummary", Style: "purple"),
                new("Find a patient", "Search a patient and review their previous readings and care captures.", "Patients", "Index", Style: "blue"),
                new("Register patient", "Add an elderly patient to the chronic care programme.", "Patients", "Register", Style: "purple")
            ],
            ApplicationRoles.Nurse =>
            [
                new("Record facility visit", "Capture vital signs, diagnosis, and treatment for a patient.", "FacilityNurse", "RecordVisit"),
                new("Find a patient", "Search a patient and review their previous readings and care captures.", "Patients", "Index", Style: "blue"),
                new("Register patient", "Add an elderly patient to the chronic care programme.", "Patients", "Register", Style: "purple")
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
                new("Find a patient", "Review the care history available to your registered family account.", "Patients", "Index", Style: "blue")
            ],
            ApplicationRoles.Dmo =>
            [
                new("Monthly DHIS2 report", "Review the monthly indicator export prepared for district reporting.", "", Page: "/Hiu/MonthlyReport", Style: "purple")
            ],
            _ =>
            [
                new("Find a patient", "Search registered patients and inspect available care history.", "Patients", "Index")
            ]
        };
}
