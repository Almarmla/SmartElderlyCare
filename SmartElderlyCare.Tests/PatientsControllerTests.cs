using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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

        var result = await controller.Index(null, CancellationToken.None);

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

    private static PatientsController CreateController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ApplicationUser familyUser)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, familyUser.Id),
                new Claim(ClaimTypes.Name, familyUser.UserName!),
                new Claim("DisplayName", familyUser.DisplayName),
                new Claim(ClaimTypes.Role, ApplicationRoles.Family)
            },
            "TestAuthentication"));
        return new PatientsController(context, new PatientRiskEvaluator(context), userManager)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            }
        };
    }
}
