using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartElderlyCare.Data;
using SmartElderlyCare.Models;

namespace SmartElderlyCare.Pages.Vhw;

[Authorize(Roles = "VHW,Nurse,FacilityNurse,DhioAdmin,Administrator")]
public class RegisterModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;

    public RegisterModel(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IReadOnlyList<PatientRegisterRow> Patients { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Patients = await _dbContext.Patients
            .AsNoTracking()
            .Where(patient => patient.IsActive)
            .OrderBy(patient => patient.LastName)
            .ThenBy(patient => patient.FirstName)
            .Select(patient => new PatientRegisterRow
            {
                PatientId = patient.Id,
                PatientNumber = patient.PatientNumber,
                FullName = patient.FirstName + " " + patient.LastName,
                MedicalCondition = patient.MedicalCondition,
                MostRecentCheck = patient.VhwWelfareChecks
                    .OrderByDescending(check => check.ObservedAt)
                    .Select(check => new WelfareCheckSummary
                    {
                        ObservedAt = check.ObservedAt,
                        Status = check.Status
                    })
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);
    }

    public static string StatusBadgeClass(VhwWelfareStatus status) =>
        status switch
        {
            VhwWelfareStatus.Stable => "text-bg-success",
            VhwWelfareStatus.NeedsAttention => "text-bg-warning",
            VhwWelfareStatus.Urgent => "text-bg-danger",
            _ => "text-bg-secondary"
        };

    public static string StatusLabel(VhwWelfareStatus status) =>
        status switch
        {
            VhwWelfareStatus.NeedsAttention => "Needs Attention",
            _ => status.ToString()
        };

    public sealed class PatientRegisterRow
    {
        public int PatientId { get; init; }

        public string PatientNumber { get; init; } = string.Empty;

        public string FullName { get; init; } = string.Empty;

        public string? MedicalCondition { get; init; }

        public WelfareCheckSummary? MostRecentCheck { get; init; }
    }

    public sealed class WelfareCheckSummary
    {
        public DateTimeOffset ObservedAt { get; init; }

        public VhwWelfareStatus Status { get; init; }
    }
}
