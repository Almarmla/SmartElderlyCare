using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartElderlyCare.Controllers;
using SmartElderlyCare.Data;
using SmartElderlyCare.Models;
using SmartElderlyCare.Services;
using Xunit;

namespace SmartElderlyCare.Tests;

public sealed class PatientsControllerTests
{
    [Fact]
    public async Task Index_WhenUserIsFamilyMember_ReturnsOnlyLinkedPatients()
    {
        using var provider = CreateServices().BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var scenario = await SeedFamilyScenarioAsync(context, userManager);
        var controller = CreateController(context, userManager, scenario.FamilyUser);

        var result = await controller.Index(null, false, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var patients = Assert.IsAssignableFrom<IEnumerable<Patient>>(view.Model).ToList();
        var visiblePatient = Assert.Single(patients);
        Assert.Equal(scenario.LinkedPatient.Id, visiblePatient.Id);
        Assert.Equal(scenario.LinkedPatient.PatientNumber, visiblePatient.PatientNumber);
    }

    [Fact]
    public async Task History_WhenUserIsFamilyMember_AndNotLinked_ReturnsForbiddenOrRedirect()
    {
        using var provider = CreateServices().BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var scenario = await SeedFamilyScenarioAsync(context, userManager);
        var controller = CreateController(context, userManager, scenario.FamilyUser);

        var result = await controller.History(
            scenario.UnlinkedPatient.Id,
            startDate: null,
            endDate: null,
            cancellationToken: CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Register_WhenUserIsFamily_ReturnsForbid()
    {
        using var provider = CreateServices().BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var familyUser = await SeedFamilyUserAsync(context, userManager);
        var controller = CreateController(context, userManager, familyUser);

        var result = controller.Register();

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task ToggleStatus_ArchivesThenRestoresThePatient()
    {
        using var provider = CreateServices().BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var staffUser = await SeedStaffUserAsync(context, userManager);
        var patient = new Patient
        {
            PatientNumber = "ARCHIVE-001",
            FirstName = "Grace",
            LastName = "Mumba",
            DateOfBirth = new DateOnly(1948, 5, 4),
            MedicalCondition = "Hypertension",
            IsActive = true
        };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();
        var controller = CreateController(context, userManager, staffUser, ApplicationRoles.Nurse);

        var archiveResult = await controller.ToggleStatus(patient.Id, CancellationToken.None);

        var archived = await context.Patients.AsNoTracking().SingleAsync(item => item.Id == patient.Id);
        Assert.False(archived.IsActive);
        var archiveRedirect = Assert.IsType<RedirectToActionResult>(archiveResult);
        Assert.Equal(nameof(PatientsController.Index), archiveRedirect.ActionName);
        Assert.Equal(true, archiveRedirect.RouteValues!["showArchived"]);

        var restoreResult = await controller.ToggleStatus(patient.Id, CancellationToken.None);

        var restored = await context.Patients.AsNoTracking().SingleAsync(item => item.Id == patient.Id);
        Assert.True(restored.IsActive);
        var restoreRedirect = Assert.IsType<RedirectToActionResult>(restoreResult);
        Assert.Equal(nameof(PatientsController.Index), restoreRedirect.ActionName);
        Assert.Equal(false, restoreRedirect.RouteValues!["showArchived"]);
    }

    [Fact]
    public async Task Index_WithShowArchived_ListsOnlyArchivedPatients()
    {
        using var provider = CreateServices().BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var staffUser = await SeedStaffUserAsync(context, userManager);
        var active = new Patient
        {
            PatientNumber = "TAB-001",
            FirstName = "Active",
            LastName = "Patient",
            DateOfBirth = new DateOnly(1946, 1, 2),
            IsActive = true
        };
        var archived = new Patient
        {
            PatientNumber = "TAB-002",
            FirstName = "Archived",
            LastName = "Patient",
            DateOfBirth = new DateOnly(1947, 2, 3),
            IsActive = false
        };
        context.Patients.AddRange(active, archived);
        await context.SaveChangesAsync();
        var controller = CreateController(context, userManager, staffUser, ApplicationRoles.Nurse);

        var result = await controller.Index(null, showArchived: true, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var patients = Assert.IsAssignableFrom<IEnumerable<Patient>>(view.Model).ToList();
        var onlyPatient = Assert.Single(patients);
        Assert.Equal(archived.Id, onlyPatient.Id);
        Assert.Equal(true, view.ViewData["ShowArchived"]);
    }

    [Fact]
    public async Task Edit_WhenPatientIsArchived_ReturnsFormSoRecordStatusCanRestoreIt()
    {
        using var provider = CreateServices().BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var staffUser = await SeedStaffUserAsync(context, userManager);
        var patient = new Patient
        {
            PatientNumber = "EDIT-ARCHIVED-001",
            FirstName = "Archived",
            LastName = "Record",
            DateOfBirth = new DateOnly(1944, 7, 8),
            IsActive = false
        };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();
        var controller = CreateController(context, userManager, staffUser, ApplicationRoles.Nurse);

        var result = await controller.Edit(patient.Id, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PatientEditInputModel>(view.Model);
        Assert.False(model.IsActive);
        Assert.Equal(true, view.ViewData["IsArchived"]);
    }

    [Fact]
    public void History_IsRoutedToPatientScopedUrl()
    {
        var method = typeof(PatientsController).GetMethod(
            nameof(PatientsController.History),
            new[] { typeof(int), typeof(DateTime?), typeof(DateTime?), typeof(bool), typeof(CancellationToken) });

        Assert.NotNull(method);
        var route = Assert.Single(method!.GetCustomAttributes<RouteAttribute>());
        Assert.Equal("patients/{id:int}/history", route.Template);
    }

    [Fact]
    public async Task History_WithoutPeriod_ListsEveryActivityNewestFirst()
    {
        using var provider = CreateServices().BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var staffUser = await SeedStaffUserAsync(context, userManager);
        var patient = new Patient
        {
            PatientNumber = "HIST-001",
            FirstName = "Timeline",
            LastName = "Patient",
            DateOfBirth = new DateOnly(1943, 9, 9),
            IsActive = true
        };
        var otherPatient = new Patient
        {
            PatientNumber = "HIST-002",
            FirstName = "Other",
            LastName = "Patient",
            DateOfBirth = new DateOnly(1941, 4, 4),
            IsActive = true
        };
        context.Patients.AddRange(patient, otherPatient);

        var now = DateTimeOffset.UtcNow;
        context.FacilityVisits.Add(new FacilityVisit
        {
            PatientId = patient.Id,
            Patient = patient,
            RecordedByUserId = staffUser.Id,
            VisitAt = now.AddDays(-40),
            FacilityName = "Central Clinic",
            Diagnosis = "Hypertension",
            Treatment = "Amlodipine 5mg",
            Status = FacilityVisitStatus.Completed
        });
        context.VhwWelfareChecks.Add(new VhwWelfareCheck
        {
            PatientId = patient.Id,
            Patient = patient,
            RecordedByUserId = staffUser.Id,
            ObservedAt = now.AddDays(-10),
            Status = VhwWelfareStatus.NeedsAttention,
            Mood = "Calm / Neutral",
            Notes = "Reported dizziness"
        });
        context.Alerts.Add(new Alert
        {
            PatientId = patient.Id,
            Patient = patient,
            Type = AlertType.Welfare,
            Severity = AlertSeverity.High,
            Message = "Missed welfare check",
            CreatedAt = now.AddDays(-2)
        });
        // Belongs to a different patient and must never surface on this timeline.
        context.FacilityVisits.Add(new FacilityVisit
        {
            PatientId = otherPatient.Id,
            Patient = otherPatient,
            RecordedByUserId = staffUser.Id,
            VisitAt = now.AddDays(-1),
            FacilityName = "Other Clinic",
            Diagnosis = "Diabetes",
            Status = FacilityVisitStatus.Completed
        });
        await context.SaveChangesAsync();
        var controller = CreateController(context, userManager, staffUser, ApplicationRoles.Nurse);

        var result = await controller.History(
            patient.Id,
            startDate: null,
            endDate: null,
            allTime: true,
            cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PatientHistoryViewModel>(view.Model);
        Assert.Equal(patient.Id, model.Patient.Id);
        Assert.True(model.ShowAllTime);
        Assert.Equal(3, model.Activities.Count);
        Assert.DoesNotContain(model.Activities, activity => activity.Description.Contains("Diabetes", StringComparison.Ordinal));

        // Newest first, regardless of which record type produced the entry.
        Assert.Equal(
            model.Activities.Select(activity => activity.OccurredAt).OrderByDescending(occurredAt => occurredAt),
            model.Activities.Select(activity => activity.OccurredAt));
        Assert.Equal(
            new[] { PatientActivityType.Alert, PatientActivityType.VhwWelfareCheck, PatientActivityType.FacilityVisit },
            model.Activities.Select(activity => activity.Type));

        var visit = model.Activities[2];
        Assert.Contains("Central Clinic", visit.Title, StringComparison.Ordinal);
        Assert.Contains("Hypertension", visit.Description, StringComparison.Ordinal);
        Assert.Contains("Amlodipine 5mg", visit.Description, StringComparison.Ordinal);

        var welfareCheck = model.Activities[1];
        Assert.Contains("NeedsAttention", welfareCheck.Title, StringComparison.Ordinal);
        Assert.Contains("Reported dizziness", welfareCheck.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task History_ByDefault_IncludesActivityOlderThanThreeMonths()
    {
        using var provider = CreateServices().BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var staffUser = await SeedStaffUserAsync(context, userManager);
        var patient = new Patient
        {
            PatientNumber = "HIST-OLD-001",
            FirstName = "Historic",
            LastName = "Patient",
            DateOfBirth = new DateOnly(1939, 1, 1),
            IsActive = true
        };
        context.Patients.Add(patient);
        context.FacilityVisits.Add(new FacilityVisit
        {
            PatientId = patient.Id,
            Patient = patient,
            RecordedByUserId = staffUser.Id,
            VisitAt = DateTimeOffset.UtcNow.AddMonths(-14),
            FacilityName = "Outreach Clinic",
            Diagnosis = "Arthritis",
            Status = FacilityVisitStatus.Completed
        });
        await context.SaveChangesAsync();
        var controller = CreateController(context, userManager, staffUser, ApplicationRoles.Nurse);

        var result = await controller.History(
            patient.Id,
            startDate: null,
            endDate: null,
            allTime: false,
            cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PatientHistoryViewModel>(view.Model);
        Assert.True(model.ShowAllTime);
        Assert.Contains(model.Activities, activity => activity.Description.Contains("Arthritis", StringComparison.Ordinal));
    }

    [Fact]
    public async Task History_WithExplicitPeriod_NarrowsTheActivityTimeline()
    {
        using var provider = CreateServices().BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var staffUser = await SeedStaffUserAsync(context, userManager);
        var patient = new Patient
        {
            PatientNumber = "HIST-PERIOD-001",
            FirstName = "Period",
            LastName = "Patient",
            DateOfBirth = new DateOnly(1940, 2, 2),
            IsActive = true
        };
        context.Patients.Add(patient);
        var now = DateTimeOffset.UtcNow;
        context.Alerts.Add(new Alert
        {
            PatientId = patient.Id,
            Patient = patient,
            Type = AlertType.VitalSigns,
            Severity = AlertSeverity.Medium,
            Message = "Recent fever alert",
            CreatedAt = now.AddDays(-2)
        });
        context.Alerts.Add(new Alert
        {
            PatientId = patient.Id,
            Patient = patient,
            Type = AlertType.VitalSigns,
            Severity = AlertSeverity.Low,
            Message = "Ancient alert",
            CreatedAt = now.AddMonths(-8)
        });
        await context.SaveChangesAsync();
        var controller = CreateController(context, userManager, staffUser, ApplicationRoles.Nurse);

        var result = await controller.History(
            patient.Id,
            startDate: now.AddDays(-30).Date,
            endDate: now.Date,
            allTime: false,
            cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PatientHistoryViewModel>(view.Model);
        Assert.False(model.ShowAllTime);
        var activity = Assert.Single(model.Activities);
        Assert.Contains("Recent fever alert", activity.Description, StringComparison.Ordinal);
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();
        return services;
    }

    private static async Task<(ApplicationUser FamilyUser, Patient LinkedPatient, Patient UnlinkedPatient)>
        SeedFamilyScenarioAsync(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
    {
        var familyUser = await SeedFamilyUserAsync(context, userManager);
        var linkedPatient = new Patient
        {
            PatientNumber = "FAMILY-001",
            FirstName = "Alice",
            LastName = "Mwamba",
            DateOfBirth = new DateOnly(1945, 3, 12),
            MedicalCondition = "Hypertension"
        };
        var unlinkedPatient = new Patient
        {
            PatientNumber = "FAMILY-002",
            FirstName = "Peter",
            LastName = "Banda",
            DateOfBirth = new DateOnly(1942, 8, 21),
            MedicalCondition = "Diabetes"
        };
        context.Patients.AddRange(linkedPatient, unlinkedPatient);
        await context.SaveChangesAsync();
        context.PatientFamilyMembers.Add(new PatientFamilyMember
        {
            Patient = linkedPatient,
            PatientId = linkedPatient.Id,
            FamilyMemberUser = familyUser,
            FamilyMemberUserId = familyUser.Id,
            AccessLevel = FamilyAccessLevel.Viewer,
            RelationshipToPatient = "Daughter",
            IsActive = true
        });
        await context.SaveChangesAsync();
        return (familyUser, linkedPatient, unlinkedPatient);
    }

    private static async Task<ApplicationUser> SeedFamilyUserAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        var familyUser = new ApplicationUser
        {
            Id = "family-user-001",
            UserName = "family.user@example.com",
            NormalizedUserName = "FAMILY.USER@EXAMPLE.COM",
            Email = "family.user@example.com",
            NormalizedEmail = "FAMILY.USER@EXAMPLE.COM",
            DisplayName = "Family User",
            EmailConfirmed = true,
            IsActive = true
        };
        context.Users.Add(familyUser);
        context.Roles.Add(new IdentityRole
        {
            Id = "family-role",
            Name = ApplicationRoles.Family,
            NormalizedName = ApplicationRoles.Family.ToUpperInvariant()
        });
        await context.SaveChangesAsync();
        var roleResult = await userManager.AddToRoleAsync(familyUser, ApplicationRoles.Family);
        Assert.True(roleResult.Succeeded);
        return familyUser;
    }

    private static async Task<ApplicationUser> SeedStaffUserAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        var staffUser = new ApplicationUser
        {
            Id = "staff-user-001",
            UserName = "staff.user@example.com",
            NormalizedUserName = "STAFF.USER@EXAMPLE.COM",
            Email = "staff.user@example.com",
            NormalizedEmail = "STAFF.USER@EXAMPLE.COM",
            DisplayName = "Staff User",
            EmailConfirmed = true,
            IsActive = true
        };
        context.Users.Add(staffUser);
        context.Roles.Add(new IdentityRole
        {
            Id = "nurse-role",
            Name = ApplicationRoles.Nurse,
            NormalizedName = ApplicationRoles.Nurse.ToUpperInvariant()
        });
        await context.SaveChangesAsync();
        var roleResult = await userManager.AddToRoleAsync(staffUser, ApplicationRoles.Nurse);
        Assert.True(roleResult.Succeeded);
        return staffUser;
    }

    private static PatientsController CreateController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ApplicationUser user)
    {
        return CreateController(context, userManager, user, ApplicationRoles.Family);
    }

    private static PatientsController CreateController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ApplicationUser user,
        string role)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName!),
                new Claim("DisplayName", user.DisplayName),
                new Claim(ClaimTypes.Role, role)
            },
            "TestAuthentication"));
        return new PatientsController(context, new PatientRiskEvaluator(context), userManager)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            },
            TempData = new TempDataDictionary(new DefaultHttpContext(), new InMemoryTempDataProvider())
        };
    }

    private sealed class InMemoryTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) =>
            new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
