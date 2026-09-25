using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SmartElderlyCare.Data;
using SmartElderlyCare.Models;
using SmartElderlyCare.Pages.Returns;
using Xunit;

namespace SmartElderlyCare.Tests;

public sealed class MonthlySummaryTests
{
    private const int ReturnYear = 2026;
    private const int ReturnMonth = 9;

    [Fact]
    public async Task OnPostApprove_UpdatesMonthlyReturnsToApproved_AndCreatesValidationRecord()
    {
        using var context = CreateContext();
        var (clerk, monthlyReturn) = await SeedSubmittedReturnAsync(context);
        var pageModel = CreatePageModel(context, clerk);
        const string notes = "Figures verified against facility records.";

        var result = await pageModel.OnPostApproveAsync(ReturnYear, ReturnMonth, notes);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Null(redirect.PageName);
        Assert.Equal(ReturnYear, redirect.RouteValues!["Year"]);
        Assert.Equal(ReturnMonth, redirect.RouteValues!["Month"]);

        context.ChangeTracker.Clear();
        var savedReturn = await context.MonthlyReturns
            .AsNoTracking()
            .SingleAsync(item => item.Id == monthlyReturn.Id);
        Assert.Equal(MonthlyReturnStatus.Approved, savedReturn.Status);
        Assert.Equal(clerk.Id, savedReturn.ReviewedByUserId);
        Assert.Equal(notes, savedReturn.ReviewNotes);
        Assert.NotNull(savedReturn.ReviewedAt);

        var validation = await context.MonthlyReturnValidations
            .AsNoTracking()
            .SingleAsync(item => item.MonthlyReturnId == monthlyReturn.Id);
        Assert.Equal(MonthlyReturnValidationStatus.Approved, validation.Status);
        Assert.Equal(clerk.Id, validation.ValidatedByUserId);
        Assert.Equal(notes, validation.Notes);
        Assert.Equal(savedReturn.ReviewedAt, validation.ValidatedAt);
    }

    [Fact]
    public async Task OnPostReject_UpdatesMonthlyReturnsToRejected_AndCreatesValidationRecord()
    {
        using var context = CreateContext();
        var (clerk, monthlyReturn) = await SeedSubmittedReturnAsync(context);
        var pageModel = CreatePageModel(context, clerk);
        const string notes = "Patient totals require correction before approval.";

        var result = await pageModel.OnPostRejectAsync(ReturnYear, ReturnMonth, notes);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Null(redirect.PageName);
        Assert.Equal(ReturnYear, redirect.RouteValues!["Year"]);
        Assert.Equal(ReturnMonth, redirect.RouteValues!["Month"]);

        context.ChangeTracker.Clear();
        var savedReturn = await context.MonthlyReturns
            .AsNoTracking()
            .SingleAsync(item => item.Id == monthlyReturn.Id);
        Assert.Equal(MonthlyReturnStatus.Rejected, savedReturn.Status);
        Assert.Equal(clerk.Id, savedReturn.ReviewedByUserId);
        Assert.Equal(notes, savedReturn.ReviewNotes);
        Assert.NotNull(savedReturn.ReviewedAt);

        var validation = await context.MonthlyReturnValidations
            .AsNoTracking()
            .SingleAsync(item => item.MonthlyReturnId == monthlyReturn.Id);
        Assert.Equal(MonthlyReturnValidationStatus.Rejected, validation.Status);
        Assert.Equal(clerk.Id, validation.ValidatedByUserId);
        Assert.Equal(notes, validation.Notes);
        Assert.Equal(savedReturn.ReviewedAt, validation.ValidatedAt);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<(ApplicationUser Clerk, MonthlyReturn MonthlyReturn)> SeedSubmittedReturnAsync(
        ApplicationDbContext context)
    {
        var clerk = new ApplicationUser
        {
            Id = "hiu-clerk-001",
            UserName = "hiu.clerk@example.com",
            Email = "hiu.clerk@example.com",
            DisplayName = "HIU Clerk",
            EmailConfirmed = true,
            IsActive = true
        };
        var patient = new Patient
        {
            PatientNumber = "RETURN-001",
            FirstName = "Grace",
            LastName = "Phiri",
            DateOfBirth = new DateOnly(1948, 6, 10)
        };
        var monthlyReturn = new MonthlyReturn
        {
            Patient = patient,
            PatientId = patient.Id,
            Year = ReturnYear,
            Month = ReturnMonth,
            Status = MonthlyReturnStatus.Submitted,
            SubmittedAt = DateTimeOffset.UtcNow
        };
        context.Users.Add(clerk);
        context.Patients.Add(patient);
        context.MonthlyReturns.Add(monthlyReturn);
        await context.SaveChangesAsync();
        return (clerk, monthlyReturn);
    }

    private static MonthlySummaryModel CreatePageModel(
        ApplicationDbContext context,
        ApplicationUser clerk)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, clerk.Id),
                new Claim(ClaimTypes.Name, clerk.UserName!),
                new Claim("DisplayName", clerk.DisplayName),
                new Claim(ClaimTypes.Role, ApplicationRoles.HiuClerk)
            },
            "TestAuthentication"));
        var httpContext = new DefaultHttpContext { User = principal };
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new PageActionDescriptor(),
            new ModelStateDictionary());
        var pageModel = new MonthlySummaryModel(context)
        {
            PageContext = new PageContext(actionContext)
        };
        pageModel.TempData = new TempDataDictionary(httpContext, new TestTempDataProvider());
        return pageModel;
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
