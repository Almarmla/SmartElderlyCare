using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartElderlyCare.Models;

namespace SmartElderlyCare.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _configuration = configuration;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Workspace");
        }

        return View(new LoginInputModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Workspace");
        }

        return View(new RegisterInputModel());
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordInputModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordInputModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email.Trim());
        var contactMatches = user is not null
            && !string.IsNullOrWhiteSpace(user.PhoneNumber)
            && NormalizeContact(user.PhoneNumber) == NormalizeContact(model.PhoneNumber);

        if (user is null || !contactMatches || !user.IsActive)
        {
            ModelState.AddModelError(string.Empty,
                "We could not verify those details. Please contact the Super Administrator to reset your password.");
            return View(model);
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(nameof(model.NewPassword), error.Description);
            }

            return View(model);
        }

        TempData["SuccessMessage"] = "Your password has been reset. You can now sign in.";
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterInputModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email.Trim(),
            Email = model.Email.Trim(),
            DisplayName = model.DisplayName.Trim(),
            PhoneNumber = model.PhoneNumber.Trim(),
            IsActive = true
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(
                    error.Code == "DuplicateUserName" || error.Code == "DuplicateEmail"
                        ? nameof(model.Email)
                        : string.Empty,
                    error.Description);
            }

            return View(model);
        }

        var roleResult = await _userManager.AddToRoleAsync(user, ApplicationRoles.Family);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            var errors = string.Join(" ", roleResult.Errors.Select(error => error.Description));
            ModelState.AddModelError(string.Empty, $"The account could not be completed: {errors}");
            return View(model);
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        return RedirectToAction("Index", "Workspace");
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginInputModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is null || !user.IsActive)
        {
            if (await SignInConfiguredAdministratorAsync(model.Email, model.Password))
            {
                return RedirectToLocal(model.ReturnUrl, isAdministrator: true);
            }

            ModelState.AddModelError(string.Empty, "The email or password is incorrect, or the account is inactive.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            user,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.IsLockedOut
                ? "This account is temporarily locked. Contact an administrator."
                : "The email or password is incorrect.");
            return View(model);
        }

        var isAdministrator = await _userManager.IsInRoleAsync(user, ApplicationRoles.Administrator)
            || await _userManager.IsInRoleAsync(user, ApplicationRoles.DhioAdmin);
        return RedirectToLocal(model.ReturnUrl, isAdministrator);
    }

    [Authorize]
    [HttpGet]
    public IActionResult ChangePassword()
    {
        return View(new ChangePasswordInputModel());
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordInputModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Your account could not be found.");
            return View(model);
        }

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(nameof(model.CurrentPassword), error.Description);
            }

            return View(model);
        }

        await _signInManager.RefreshSignInAsync(user);
        TempData["SuccessMessage"] = "Your password has been changed.";
        return RedirectToAction("Index", "Workspace");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private IActionResult RedirectToLocal(string? returnUrl, bool isAdministrator = false)
    {
        return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : isAdministrator
                ? RedirectToAction("Dashboard", "Home")
                : RedirectToAction("Index", "Workspace");
    }

    private static string NormalizeContact(string value)
    {
        return new string(value.Where(char.IsDigit).ToArray());
    }

    private async Task<bool> SignInConfiguredAdministratorAsync(string email, string password)
    {
        var admin = _configuration.GetSection("AdminUser");
        var configuredEmail = admin["Email"];
        var configuredSalt = admin["PasswordSalt"];
        var configuredHash = admin["PasswordHash"];
        var identityPasswordHash = admin["IdentityPasswordHash"];
        var iterations = admin.GetValue<int>("PasswordIterations");

        if (!string.Equals(email.Trim(), configuredEmail, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(configuredSalt)
            || string.IsNullOrWhiteSpace(configuredHash)
            || string.IsNullOrWhiteSpace(identityPasswordHash)
            || iterations <= 0
            || !VerifyPassword(password, configuredSalt, configuredHash, iterations))
        {
            return false;
        }

        var user = await _userManager.FindByEmailAsync(configuredEmail!);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = configuredEmail!,
                Email = configuredEmail!,
                EmailConfirmed = true,
                DisplayName = admin["Name"] ?? configuredEmail!,
                IsActive = true
            };

            user.PasswordHash = identityPasswordHash;
            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                return false;
            }
        }
        else
        {
            user.Email = configuredEmail;
            user.UserName = configuredEmail;
            user.EmailConfirmed = true;
            user.IsActive = true;
            user.PasswordHash = identityPasswordHash;
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                return false;
            }
        }

        var roles = new[]
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
        var existingRoles = await _userManager.GetRolesAsync(user);
        foreach (var role in roles.Except(existingRoles, StringComparer.Ordinal))
        {
            var roleResult = await _userManager.AddToRoleAsync(user, role);
            if (!roleResult.Succeeded)
            {
                return false;
            }
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        return true;
    }

    private static bool VerifyPassword(string password, string saltBase64, string hashBase64, int iterations)
    {
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

    public class LoginInputModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }

        public string? ReturnUrl { get; set; }
    }

    public class RegisterInputModel
    {
        [Required]
        [StringLength(200, MinimumLength = 2)]
        [Display(Name = "Full name")]
        public string DisplayName { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Phone]
        [Display(Name = "Contact phone number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 8)]
        public string Password { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class ForgotPasswordInputModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, Phone]
        [Display(Name = "Contact phone number used when registering")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 8)]
        [Display(Name = "New password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
        [Display(Name = "Confirm new password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class ChangePasswordInputModel
    {
        [Required, DataType(DataType.Password)]
        [Display(Name = "Current password")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 8)]
        [Display(Name = "New password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
        [Display(Name = "Confirm new password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
