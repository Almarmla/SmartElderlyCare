using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartElderlyCare.Areas.Admin.Pages.Users;
using SmartElderlyCare.Data;
using SmartElderlyCare.Models;
using Xunit;

namespace SmartElderlyCare.Tests;

public sealed class AdminUserManagementTests
{
    [Fact]
    public async Task Create_AddsUserToSelectedRoleAndWritesAuditRecord()
    {
        using var provider = CreateServices().BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var admin = await CreateAdminAsync(userManager);
        await roleManager.CreateAsync(new IdentityRole(ApplicationRoles.FacilityNurse));
        var page = CreatePageModel(context, userManager, roleManager, scope.ServiceProvider, admin);
        page.Input = new IndexModel.CreateUserInput
        {
            DisplayName = "  New Nurse  ",
            Email = "  nurse@example.org  ",
            PhoneNumber = " +263771234567 ",
            Password = "VeryStrongPassword1",
            Role = ApplicationRoles.FacilityNurse
        };

        var result = await page.OnPostCreateAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(
            "Account successfully created for New Nurse (nurse@example.org) with the FacilityNurse role.",
            page.TempData["SuccessMessage"]);
        var createdUser = await userManager.FindByEmailAsync("nurse@example.org");
        Assert.NotNull(createdUser);
        Assert.Equal("New Nurse", createdUser.DisplayName);
        Assert.Equal("+263771234567", createdUser.PhoneNumber);
        Assert.Contains(ApplicationRoles.FacilityNurse, await userManager.GetRolesAsync(createdUser));
        var audit = await context.AdministrationAuditLogs.SingleAsync();
        Assert.Equal(AdministrationAction.UserCreated, audit.Action);
        Assert.Equal(admin.Id, audit.PerformedByUserId);
        Assert.Equal(createdUser.Id, audit.AffectedUserId);
    }

    [Fact]
    public async Task Create_RejectsRoleThatDoesNotExistWithoutCreatingUser()
    {
        using var provider = CreateServices().BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var admin = await CreateAdminAsync(userManager);
        var page = CreatePageModel(context, userManager, roleManager, scope.ServiceProvider, admin);
        page.Input = new IndexModel.CreateUserInput
        {
            DisplayName = "New Nurse",
            Email = "nurse@example.org",
            PhoneNumber = "+263771234567",
            Password = "VeryStrongPassword1",
            Role = "NotASeededRole"
        };

        var result = await page.OnPostCreateAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(page.ModelState.IsValid);
        Assert.Null(await userManager.FindByEmailAsync("nurse@example.org"));
        Assert.Empty(await context.AdministrationAuditLogs.ToListAsync());
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();
        return services;
    }

    private static async Task<ApplicationUser> CreateAdminAsync(UserManager<ApplicationUser> userManager)
    {
        var admin = new ApplicationUser
        {
            UserName = "admin@example.org",
            Email = "admin@example.org",
            DisplayName = "Admin",
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(admin, "VeryStrongPassword1");
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));
        return admin;
    }

    private static IndexModel CreatePageModel(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IServiceProvider services,
        ApplicationUser admin)
    {
        var page = new IndexModel(
            context,
            userManager,
            roleManager,
            services.GetRequiredService<ILogger<IndexModel>>());
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, admin.Id)],
                authenticationType: "Test"))
        };
        page.PageContext = new PageContext
        {
            HttpContext = httpContext
        };
        page.TempData = new TempDataDictionary(httpContext, new TestTempDataProvider());
        return page;
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) =>
            new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
