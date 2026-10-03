using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartElderlyCare.Models;

namespace SmartElderlyCare.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(returnUrl);
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
        // Self-service resets are intentionally disabled. Passwords protect
        // clinical records, so recovery is handled by an administrator.
        return View();
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
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "The email or password is incorrect.");
            return View(model);
        }

        // Always delegate to SignInManager so failed attempts count towards
        // lockout. Deactivated accounts are rejected after the credential check
        // and immediately signed out, so they cannot be used to probe passwords.
        var result = await _signInManager.PasswordSignInAsync(
            user,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (!result.Succeeded || !user.IsActive)
        {
            if (result.Succeeded)
            {
                await _signInManager.SignOutAsync();
            }

            ModelState.AddModelError(string.Empty, !result.Succeeded && result.IsLockedOut
                ? "This account is temporarily locked. Contact an administrator."
                : "The email or password is incorrect, or the account is inactive.");
            return View(model);
        }

        return RedirectToLocal(model.ReturnUrl);
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

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction("Index", "Workspace");
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
