using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartElderlyCare.Data;
using SmartElderlyCare.Models;

namespace SmartElderlyCare.Areas.Admin.Pages.Thresholds;

[Authorize(AuthenticationSchemes = "AdminCookie,Identity.Application", Roles = ApplicationRoles.DhioAdmin)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;

    public EditModel(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int? id, CancellationToken cancellationToken)
    {
        if (id is null)
        {
            return Page();
        }

        var threshold = await _dbContext.Thresholds.FindAsync([id.Value], cancellationToken);
        if (threshold is null)
        {
            return NotFound();
        }

        Input = new InputModel
        {
            Id = threshold.Id,
            Name = threshold.Name,
            MedicalCondition = threshold.MedicalCondition,
            Metric = threshold.Metric,
            MinimumValue = threshold.MinimumValue,
            MaximumValue = threshold.MaximumValue,
            Unit = threshold.Unit,
            Severity = threshold.Severity,
            IsActive = threshold.IsActive
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (Input.MinimumValue.HasValue && Input.MaximumValue.HasValue && Input.MinimumValue > Input.MaximumValue)
        {
            ModelState.AddModelError(nameof(Input.MaximumValue), "Maximum must be greater than or equal to minimum.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        Threshold threshold;
        var action = Input.Id.HasValue ? AdministrationAction.ThresholdUpdated : AdministrationAction.ThresholdCreated;
        if (Input.Id.HasValue)
        {
            threshold = await _dbContext.Thresholds.FindAsync([Input.Id.Value], cancellationToken)
                ?? throw new InvalidOperationException("The threshold could not be found.");
        }
        else
        {
            threshold = new Threshold();
            _dbContext.Thresholds.Add(threshold);
        }

        threshold.Name = Input.Name;
        threshold.MedicalCondition = Input.MedicalCondition;
        threshold.Metric = Input.Metric;
        threshold.MinimumValue = Input.MinimumValue;
        threshold.MaximumValue = Input.MaximumValue;
        threshold.Unit = Input.Unit;
        threshold.Severity = Input.Severity;
        threshold.IsActive = Input.IsActive;

        var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrWhiteSpace(adminId))
        {
            _dbContext.AdministrationAuditLogs.Add(new AdministrationAuditLog
            {
                Action = action,
                EntityName = nameof(Threshold),
                EntityId = threshold.Id.ToString(),
                Threshold = threshold,
                PerformedByUserId = adminId,
                Details = $"{action} for {threshold.MedicalCondition ?? "all conditions"}."
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToPage("/Index", new { area = "Admin" });
    }

    public class InputModel
    {
        public int? Id { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        [Display(Name = "Medical condition")]
        public string? MedicalCondition { get; set; }

        [Required]
        public ThresholdMetric Metric { get; set; }

        [Display(Name = "Minimum value")]
        public decimal? MinimumValue { get; set; }

        [Display(Name = "Maximum value")]
        public decimal? MaximumValue { get; set; }

        [StringLength(30)]
        public string? Unit { get; set; }

        public AlertSeverity Severity { get; set; } = AlertSeverity.Medium;

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;
    }
}
