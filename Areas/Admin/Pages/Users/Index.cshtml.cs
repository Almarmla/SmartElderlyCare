using System.Data.Common;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartElderlyCare.Data;
using SmartElderlyCare.Models;

namespace SmartElderlyCare.Areas.Admin.Pages.Users;

[Authorize(Roles = ApplicationRoles.Administrator)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ILogger<IndexModel> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    public IReadOnlyList<ApplicationUser> Users { get; private set; } = [];

    public IReadOnlyList<string> Roles { get; private set; } = [];

    public IReadOnlyDictionary<string, string> UserRoles { get; private set; } = new Dictionary<string, string>();

    [BindProperty(SupportsGet = true)]
    public string SearchTerm { get; set; } = string.Empty;

    [BindProperty]
    public CreateUserInput Input { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadUsersAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
    {
        Input.DisplayName = Input.DisplayName?.Trim() ?? string.Empty;
        Input.Email = Input.Email?.Trim() ?? string.Empty;
        Input.PhoneNumber = Input.PhoneNumber?.Trim() ?? string.Empty;
        Input.Role = Input.Role?.Trim() ?? string.Empty;

        if (!ModelState.IsValid)
        {
            await LoadUsersAsync(cancellationToken);
            return Page();
        }

        ApplicationUser? createdUser = null;
        var userCreated = false;
        try
        {
            if (!await _roleManager.RoleExistsAsync(Input.Role))
            {
                ModelState.AddModelError(nameof(Input.Role), "Select a role that has been seeded.");
                await LoadUsersAsync(cancellationToken);
                return Page();
            }

            createdUser = new ApplicationUser
            {
                UserName = Input.Email,
                Email = Input.Email,
                DisplayName = Input.DisplayName,
                PhoneNumber = Input.PhoneNumber,
                EmailConfirmed = true,
                IsActive = true
            };

            var result = await _userManager.CreateAsync(createdUser, Input.Password);
            if (!result.Succeeded)
            {
                AddIdentityErrors(result);
                await LoadUsersAsync(cancellationToken);
                return Page();
            }

            userCreated = true;
            var roleResult = await _userManager.AddToRoleAsync(createdUser, Input.Role);
            if (!roleResult.Succeeded)
            {
                AddIdentityErrors(roleResult);
                var cleanupResult = await _userManager.DeleteAsync(createdUser);
                if (!cleanupResult.Succeeded)
                {
                    _logger.LogError(
                        "Could not remove newly created user {UserId} after role assignment failed: {Errors}",
                        createdUser.Id,
                        string.Join("; ", cleanupResult.Errors.Select(error => error.Description)));
                    ModelState.AddModelError(
                        string.Empty,
                        "The account was created but could not be assigned its role or removed automatically. Review this account before it is used.");
                }

                await LoadUsersAsync(cancellationToken);
                return Page();
            }
        }
        catch (Exception exception) when (exception is DbUpdateException or DbException)
        {
            if (userCreated && createdUser is not null)
            {
                try
                {
                    var cleanupResult = await _userManager.DeleteAsync(createdUser);
                    if (!cleanupResult.Succeeded)
                    {
                        _logger.LogError(
                            "Could not clean up user {UserId} after a database error: {Errors}",
                            createdUser.Id,
                            string.Join("; ", cleanupResult.Errors.Select(error => error.Description)));
                    }
                }
                catch (Exception cleanupException) when (cleanupException is DbUpdateException or DbException)
                {
                    _logger.LogError(
                        cleanupException,
                        "Could not clean up user {UserId} after a database error.",
                        createdUser.Id);
                }
            }

            _logger.LogError(exception, "A database error prevented user account creation.");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                "The account could not be created because the database is temporarily unavailable. Please try again later.");
        }

        await AddAuditAsync(
            AdministrationAction.UserCreated,
            createdUser!.Id,
            createdUser.Id,
            $"Created user with role {Input.Role}.");
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            TempData["SuccessMessage"] = $"Account successfully created for {Input.DisplayName} ({Input.Email}) with the {Input.Role} role.";
        }
        catch (Exception exception) when (exception is DbUpdateException or DbException)
        {
            _logger.LogError(
                exception,
                "User {UserId} was created with role {Role}, but its audit record could not be saved.",
                createdUser.Id,
                Input.Role);
            TempData["SuccessMessage"] = $"Account successfully created for {Input.DisplayName} ({Input.Email}) with the {Input.Role} role.";
            TempData["ErrorMessage"] = "The account was created, but its audit record could not be saved. Notify the system administrator.";
        }

        return RedirectToPage("./Index");
    }

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
    }

    public async Task<IActionResult> OnPostResetPasswordAsync(string id, string newPassword, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
        {
            TempData["UserError"] = "The new password must contain at least 8 characters.";
            return RedirectToPage();
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            TempData["UserError"] = string.Join("; ", result.Errors.Select(error => error.Description));
            return RedirectToPage();
        }

        await AddAuditAsync(AdministrationAction.PasswordReset, user.Id, user.Id, "Reset user password.");
        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData["UserSuccess"] = $"Password reset for {user.DisplayName}.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeactivateAsync(string id, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        user.IsActive = false;
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        await _userManager.UpdateAsync(user);
        await AddAuditAsync(AdministrationAction.UserDisabled, user.Id, user.Id, "Deactivated user account.");
        await _dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateAsync(
        string id,
        string displayName,
        string email,
        string role,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(displayName) || !new EmailAddressAttribute().IsValid(email) || !await _roleManager.RoleExistsAsync(role))
        {
            TempData["UserError"] = "Enter a valid display name, email, and seeded role.";
            return RedirectToPage();
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        user.DisplayName = displayName.Trim();
        user.Email = email.Trim();
        user.UserName = email.Trim();
        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            TempData["UserError"] = string.Join("; ", updateResult.Errors.Select(error => error.Description));
            return RedirectToPage();
        }

        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        await _userManager.AddToRoleAsync(user, role);
        await AddAuditAsync(AdministrationAction.UserUpdated, user.Id, user.Id, $"Updated account and assigned role {role}.");
        await _dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostReactivateAsync(string id, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        user.IsActive = true;
        user.LockoutEnd = null;
        await _userManager.UpdateAsync(user);
        await AddAuditAsync(AdministrationAction.UserUpdated, user.Id, user.Id, "Reactivated user account.");
        await _dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUnlockAsync(string id, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        var resetResult = await _userManager.ResetAccessFailedCountAsync(user);
        var lockoutResult = await _userManager.SetLockoutEndDateAsync(user, null);
        if (!resetResult.Succeeded || !lockoutResult.Succeeded)
        {
            TempData["UserError"] = string.Join(
                "; ",
                resetResult.Errors.Concat(lockoutResult.Errors).Select(error => error.Description));
            return RedirectToPage();
        }

        await AddAuditAsync(
            AdministrationAction.UserUnlocked,
            user.Id,
            user.Id,
            "Cleared failed login attempts and lockout end date.");
        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData["UserSuccess"] = $"Unlocked {user.DisplayName}.";
        return RedirectToPage();
    }

    private async Task LoadUsersAsync(CancellationToken cancellationToken)
    {
        var usersQuery = _userManager.Users.AsNoTracking();
        SearchTerm = SearchTerm.Trim();
        var searchTerm = SearchTerm;
        if (searchTerm.Length > 0)
        {
            usersQuery = usersQuery.Where(user =>
                user.DisplayName.Contains(searchTerm) ||
                user.Email!.Contains(searchTerm) ||
                user.UserName!.Contains(searchTerm));
        }

        Users = await usersQuery.OrderBy(user => user.DisplayName).ToListAsync(cancellationToken);
        Roles = await _roleManager.Roles.OrderBy(role => role.Name).Select(role => role.Name!).ToListAsync(cancellationToken);
        var userRoles = new Dictionary<string, string>();
        foreach (var user in Users)
        {
            userRoles[user.Id] = (await _userManager.GetRolesAsync(user)).FirstOrDefault() ?? ApplicationRoles.Nurse;
        }
        UserRoles = userRoles;
    }

    private async Task AddAuditAsync(AdministrationAction action, string entityId, string? affectedUserId, string details)
    {
        var adminId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(adminId))
        {
            return;
        }

        _dbContext.AdministrationAuditLogs.Add(new AdministrationAuditLog
        {
            Action = action,
            EntityName = nameof(ApplicationUser),
            EntityId = entityId,
            AffectedUserId = affectedUserId,
            PerformedByUserId = adminId,
            Details = details
        });
        await Task.CompletedTask;
    }

    public class CreateUserInput
    {
        [Required, StringLength(200)]
        [Display(Name = "Display name")]
        public string DisplayName { get; set; } = string.Empty;

        [Required, EmailAddress, StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required, Phone]
        [Display(Name = "Contact phone")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required, MinLength(8)]
        public string Password { get; set; } = string.Empty;

        [Required, StringLength(50)]
        public string Role { get; set; } = ApplicationRoles.Nurse;
    }
}
