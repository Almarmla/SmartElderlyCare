using System.Data.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SmartElderlyCare.Data;
using SmartElderlyCare.Models;
using SmartElderlyCare.Services;

var builder = WebApplication.CreateBuilder(args);

var isDevelopment = builder.Environment.IsDevelopment();
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "No database connection string is configured. Set ConnectionStrings:DefaultConnection, or the ConnectionStrings__DefaultConnection environment variable.");
}

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
// EnableRetryOnFailure requires the connection string to omit
// MultipleActiveResultSets=True, which is why the development connection string
// in appsettings.Development.json no longer sets it.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        connectionString,
        sql => sql.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null)));
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseConnectivityHealthCheck>("database");
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
    options.Cookie.SecurePolicy = isDevelopment
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
});
builder.Services.AddAuthorization();
builder.Services.AddScoped<AlertService>();
builder.Services.AddScoped<IVitalEvaluator, VitalEvaluator>();
builder.Services.AddScoped<IPatientRiskEvaluator, PatientRiskEvaluator>();
builder.Services.AddScoped<Dhis2MonthlyExportService>();
builder.Services.AddScoped<IDistrictReportService, DistrictReportService>();

var app = builder.Build();

if (args.Contains("--unlock-admin", StringComparer.OrdinalIgnoreCase))
{
    // Recovery tool for a locked-out local administrator. It exits after
    // unlocking, so it must never be reachable from a production start command.
    if (!isDevelopment)
    {
        throw new InvalidOperationException(
            "--unlock-admin is only available in the Development environment.");
    }

    await InitializeDatabaseAsync(app.Services, failOnError: true);
    await UnlockConfiguredAdministratorAsync(app.Services, app.Configuration);
    return;
}

await InitializeDatabaseAsync(app.Services, failOnError: !isDevelopment);

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
app.MapHealthChecks("/healthz");

app.Run();

static async Task InitializeDatabaseAsync(IServiceProvider services, bool failOnError)
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
        // Outside development a failed migration or a missing administrator must
        // fail the deployment. Serving traffic against a half-initialised schema
        // looks healthy until a user hits the broken feature.
        if (failOnError)
        {
            throw new InvalidOperationException(
                "SmartElderlyCare could not initialise its database. Refusing to start; see the inner exception for the underlying cause.",
                exception);
        }

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

    // The AdminUser section only bootstraps the very first administrator. Once
    // that account exists it is expected to be removed from configuration, so a
    // missing section is only an error when there is no administrator at all.
    if (string.IsNullOrWhiteSpace(email))
    {
        var administrators = await userManager.GetUsersInRoleAsync(ApplicationRoles.Administrator);
        if (administrators.Count > 0)
        {
            logger.LogInformation(
                "No AdminUser configuration is present, but an administrator already exists, so there is nothing to seed.");
            return;
        }

        throw new InvalidOperationException(
            "No administrator exists and no AdminUser configuration was found. Set AdminUser:Email, AdminUser:Name and AdminUser:IdentityPasswordHash to bootstrap the first administrator.");
    }

    var user = await userManager.FindByEmailAsync(email);

    if (user is null)
    {
        if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new InvalidOperationException(
                "AdminUser must define Name and IdentityPasswordHash to create the super administrator.");
        }

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
        // Only overwrite the display name when one is actually configured, so a
        // trimmed-down AdminUser section cannot blank out an existing account.
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            user.DisplayName = displayName;
        }

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

// Reports whether the application can actually reach its database, so a broken
// connection fails the host's health probe instead of looking like a healthy
// instance that errors on every data page.
internal sealed class DatabaseConnectivityHealthCheck : IHealthCheck
{
    private readonly ApplicationDbContext _dbContext;

    public DatabaseConnectivityHealthCheck(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("The database is not reachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("The database is not reachable.", exception);
        }
    }
}
