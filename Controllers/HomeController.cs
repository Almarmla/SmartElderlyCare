using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using SmartElderlyCare.Data;
using Microsoft.AspNetCore.Mvc;
using SmartElderlyCare.Models;

namespace SmartElderlyCare.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _dbContext;

    public HomeController(
        ILogger<HomeController> logger,
        IConfiguration configuration,
        ApplicationDbContext dbContext)
    {
        _logger = logger;
        _configuration = configuration;
        _dbContext = dbContext;
    }

    public IActionResult Index()
    {
        return RedirectToAction(nameof(Login));
    }

    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction(nameof(Dashboard));
        }

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Login(string email, string password)
    {
        var admin = _configuration.GetSection("AdminUser");
        var configuredEmail = admin["Email"];
        var configuredSalt = admin["PasswordSalt"];
        var configuredHash = admin["PasswordHash"];
        var iterations = admin.GetValue<int>("PasswordIterations");

        if (string.IsNullOrWhiteSpace(configuredEmail)
            || string.IsNullOrWhiteSpace(configuredSalt)
            || string.IsNullOrWhiteSpace(configuredHash)
            || iterations <= 0
            || !string.Equals(email?.Trim(), configuredEmail, StringComparison.OrdinalIgnoreCase)
            || !VerifyPassword(password, configuredSalt, configuredHash, iterations))
        {
            ViewData["LoginError"] = "The email or password is incorrect.";
            return View();
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, admin["Name"] ?? configuredEmail),
            new Claim(ClaimTypes.Email, configuredEmail),
            new Claim(ClaimTypes.Role, "Administrator"),
            new Claim(ClaimTypes.Role, ApplicationRoles.DhioAdmin)
        };
        var identity = new ClaimsIdentity(claims, "AdminCookie");
        var principal = new ClaimsPrincipal(identity);

        HttpContext.SignInAsync("AdminCookie", principal).GetAwaiter().GetResult();
        return RedirectToAction(nameof(Dashboard));
    }

    [Microsoft.AspNetCore.Authorization.Authorize(AuthenticationSchemes = "AdminCookie")]
    public IActionResult Dashboard()
    {
        var catNow = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(2));
        ViewData["CatDate"] = catNow.ToString("dddd, MMMM d, yyyy", CultureInfo.InvariantCulture);
        ViewData["Greeting"] = catNow.Hour < 12
            ? "Good morning"
            : catNow.Hour < 18
                ? "Good afternoon"
                : "Good evening";
        ViewData["UserRole"] = User.FindFirstValue(ClaimTypes.Role) ?? "Administrator";

        return View();
    }

    [HttpGet]
    [Microsoft.AspNetCore.Authorization.Authorize(AuthenticationSchemes = "AdminCookie")]
    public async Task<IActionResult> UnreadAlerts(CancellationToken cancellationToken)
    {
        var alerts = await _dbContext.Alerts
            .AsNoTracking()
            .Include(alert => alert.Patient)
            .Where(alert => !alert.IsRead && alert.Status != AlertStatus.Resolved)
            .OrderByDescending(alert => alert.VitalStatus)
            .ThenByDescending(alert => alert.CreatedAt)
            .Select(alert => new
            {
                alert.Id,
                ResidentName = alert.Patient.FirstName + " " + alert.Patient.LastName,
                alert.Metric,
                alert.Value,
                Status = alert.VitalStatus.ToString(),
                alert.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Json(alerts);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.Authorization.Authorize(AuthenticationSchemes = "AdminCookie")]
    public async Task<IActionResult> MarkAlertRead(long alertId, CancellationToken cancellationToken)
    {
        var alert = await _dbContext.Alerts
            .SingleOrDefaultAsync(item => item.Id == alertId, cancellationToken);

        if (alert is null)
        {
            return NotFound(new { message = "Alert not found." });
        }

        alert.IsRead = true;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Json(new { success = true, alertId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.SignOutAsync("AdminCookie").GetAwaiter().GetResult();
        return RedirectToAction(nameof(Login));
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private static bool VerifyPassword(string? password, string saltBase64, string hashBase64, int iterations)
    {
        if (string.IsNullOrEmpty(password))
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(saltBase64);
            var expectedHash = Convert.FromBase64String(hashBase64);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
