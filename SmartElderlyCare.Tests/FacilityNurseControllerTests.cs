using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartElderlyCare.Controllers;
using SmartElderlyCare.Data;
using SmartElderlyCare.Models;
using SmartElderlyCare.Services;
using Xunit;

namespace SmartElderlyCare.Tests;

public sealed class FacilityNurseControllerTests
{
    [Fact]
    public async Task DiscontinueMedication_SetsIsActiveToFalse_AndAppendsDiscontinuationNote()
    {
        using var context = CreateContext();
        var patient = new Patient
        {
            PatientNumber = "MED-001",
            FirstName = "Samuel",
            LastName = "Chirwa",
            DateOfBirth = new DateOnly(1946, 11, 4)
        };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();
        var medication = new PatientMedication
        {
            Patient = patient,
            PatientId = patient.Id,
            MedicationName = "Amlodipine",
            Dosage = "5 mg",
            Frequency = "Once daily",
            IsActive = true,
            Notes = "Take with food."
        };
        context.PatientMedications.Add(medication);
        await context.SaveChangesAsync();
        var httpContext = CreateHttpContext();
        var controller = new FacilityNurseController(
            context,
            new AlertService(context, new VitalEvaluator(context), NullLogger<AlertService>.Instance))
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
        };

        var result = await controller.DiscontinueMedication(
            medication.Id,
            patient.Id,
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(FacilityNurseController.Medications), redirect.ActionName);
        Assert.Equal(patient.Id, redirect.RouteValues!["patientId"]);

        context.ChangeTracker.Clear();
        var savedMedication = await context.PatientMedications
            .AsNoTracking()
            .SingleAsync(item => item.Id == medication.Id);
        Assert.False(savedMedication.IsActive);
        Assert.NotNull(savedMedication.Notes);
        Assert.StartsWith("Take with food. Discontinued on ", savedMedication.Notes);
        Assert.EndsWith(" by Test Nurse.", savedMedication.Notes);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "facility-nurse-001"),
                new Claim(ClaimTypes.Name, "facility.nurse@example.com"),
                new Claim("DisplayName", "Test Nurse"),
                new Claim(ClaimTypes.Role, ApplicationRoles.FacilityNurse)
            },
            "TestAuthentication"));
        return new DefaultHttpContext { User = principal };
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context)
        {
            return new Dictionary<string, object>();
        }

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
