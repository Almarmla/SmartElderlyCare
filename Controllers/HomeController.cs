using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartElderlyCare.Data;
using Microsoft.AspNetCore.Mvc;
using SmartElderlyCare.Models;

namespace SmartElderlyCare.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _dbContext;

    public HomeController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IActionResult Index()
    {
        return RedirectToAction(nameof(Login));
    }

    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        return RedirectToAction("Login", "Account", new { returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public IActionResult Login(string? email, string? password)
    {
        return RedirectToAction("Login", "Account");
    }

    [Microsoft.AspNetCore.Authorization.Authorize(AuthenticationSchemes = "AdminCookie,Identity.Application")]
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
        ViewData["DisplayName"] = User.FindFirstValue("DisplayName")
            ?? User.Identity?.Name
            ?? "Care team member";

        return View();
    }

    [HttpGet]
    [Microsoft.AspNetCore.Authorization.Authorize(AuthenticationSchemes = "AdminCookie,Identity.Application")]
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
    [Microsoft.AspNetCore.Authorization.Authorize(AuthenticationSchemes = "AdminCookie,Identity.Application")]
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
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("AdminCookie");
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return RedirectToAction("Login", "Account");
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

}
