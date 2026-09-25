using System.Data.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartElderlyCare.Data;
using SmartElderlyCare.Models;
using SmartElderlyCare.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();
builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, ApplicationUserClaimsPrincipalFactory>();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});
builder.Services.AddAuthorization();
builder.Services.AddScoped<AlertService>();
builder.Services.AddScoped<IVitalEvaluator, VitalEvaluator>();
builder.Services.AddScoped<IPatientRiskEvaluator, PatientRiskEvaluator>();
builder.Services.AddScoped<Dhis2MonthlyExportService>();
builder.Services.AddScoped<IDistrictReportService, DistrictReportService>();

var app = builder.Build();

await InitializeDatabaseAsync(app.Services);

if (args.Contains("--unlock-admin", StringComparer.OrdinalIgnoreCase))
{
    await UnlockConfiguredAdministratorAsync(app.Services, app.Configuration);
    return;
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Login}/{id?}");
app.MapRazorPages();

app.Run();

static async Task InitializeDatabaseAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitialization");

    try
    {
        await dbContext.Database.MigrateAsync();
        logger.LogInformation("SmartElderlyCare database is ready.");

        var applicationRoles = new[]
        {
            ApplicationRoles.Nurse,
            ApplicationRoles.FacilityNurse,
            ApplicationRoles.VHW,
            ApplicationRoles.RecordsStaff,
            ApplicationRoles.Family,
            ApplicationRoles.HiuClerk,
            ApplicationRoles.DhioAdmin,
            ApplicationRoles.Administrator,
            ApplicationRoles.Dmo
        };

        foreach (var role in applicationRoles)
        {
            if (await roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new IdentityRole(role));
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(error => error.Description));
                throw new InvalidOperationException($"Could not seed Identity role '{role}': {errors}");
            }
        }

        logger.LogInformation("Identity roles are ready.");
        await SeedSuperAdminAsync(userManager, applicationRoles, logger, services.GetRequiredService<IConfiguration>());
        await DbSeeder.SeedThresholdsAsync(dbContext);
        logger.LogInformation("Thresholds are ready.");
    }
    catch (Exception exception)
    {
        logger.LogError(
            exception,
            "The SmartElderlyCare database could not be initialized. The application will continue, but data features may be unavailable.");
    }
}

static async Task SeedSuperAdminAsync(
    UserManager<ApplicationUser> userManager,
    IEnumerable<string> applicationRoles,
    ILogger logger,
    IConfiguration configuration)
{
    var adminConfiguration = configuration.GetSection("AdminUser");
    var email = adminConfiguration["Email"];
    var displayName = adminConfiguration["Name"];
    var passwordHash = adminConfiguration["IdentityPasswordHash"];
    if (string.IsNullOrWhiteSpace(email)
        || string.IsNullOrWhiteSpace(displayName)
        || string.IsNullOrWhiteSpace(passwordHash))
    {
        throw new InvalidOperationException("AdminUser must define Name, Email, and IdentityPasswordHash.");
    }

    var user = await userManager.FindByEmailAsync(email);
    if (user is null)
    {
        user = await userManager.FindByEmailAsync("mlamboalmar@gmail.com");
    }

    if (user is null)
    {
        user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = displayName,
            IsActive = true,
            PasswordHash = passwordHash
        };

        var createResult = await userManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"Could not seed the super administrator: {errors}");
        }

        logger.LogInformation("Super administrator account was created.");
    }
    else
    {
        user.DisplayName = displayName;
        user.EmailConfirmed = true;
        user.IsActive = true;

        var emailResult = await userManager.SetEmailAsync(user, email);
        var userNameResult = await userManager.SetUserNameAsync(user, email);
        if (!emailResult.Succeeded || !userNameResult.Succeeded)
        {
            var errors = string.Join(
                "; ",
                emailResult.Errors.Concat(userNameResult.Errors).Select(error => error.Description));
            throw new InvalidOperationException($"Could not update the super administrator email: {errors}");
        }

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            var errors = string.Join("; ", updateResult.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"Could not update the super administrator: {errors}");
        }
    }

    var existingRoles = await userManager.GetRolesAsync(user);
    var missingRoles = applicationRoles.Except(existingRoles, StringComparer.Ordinal);
    foreach (var role in missingRoles)
    {
        var roleResult = await userManager.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
        {
            var errors = string.Join("; ", roleResult.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"Could not assign super administrator role '{role}': {errors}");
        }
    }

    logger.LogInformation("Super administrator roles are ready.");
}

static async Task UnlockConfiguredAdministratorAsync(IServiceProvider services, IConfiguration configuration)
{
    var email = configuration["AdminUser:Email"];
    if (string.IsNullOrWhiteSpace(email))
    {
        throw new InvalidOperationException("AdminUser:Email is not configured.");
    }

    using var scope = services.CreateScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var user = await userManager.FindByEmailAsync(email);
    if (user is null)
    {
        throw new InvalidOperationException($"The configured administrator '{email}' was not found.");
    }

    var resetResult = await userManager.ResetAccessFailedCountAsync(user);
    var lockoutResult = await userManager.SetLockoutEndDateAsync(user, null);
    if (!resetResult.Succeeded || !lockoutResult.Succeeded)
    {
        var errors = string.Join(
            "; ",
            resetResult.Errors.Concat(lockoutResult.Errors).Select(error => error.Description));
        throw new InvalidOperationException($"Could not unlock '{email}': {errors}");
    }

    Console.WriteLine($"Unlocked configured administrator '{email}'.");
}
