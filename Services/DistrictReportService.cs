using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SmartElderlyCare.Data;
using SmartElderlyCare.Models;

namespace SmartElderlyCare.Services;

public class DistrictReportService : IDistrictReportService
{
    private readonly ApplicationDbContext _dbContext;

    public DistrictReportService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DistrictReport> GenerateOrUpdateReportAsync(
        int year,
        int month,
        string districtCode,
        string districtName,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), "Month must be between 1 and 12.");
        }

        if (string.IsNullOrWhiteSpace(districtCode))
        {
            throw new ArgumentException("A district code is required.", nameof(districtCode));
        }

        if (string.IsNullOrWhiteSpace(districtName))
        {
            throw new ArgumentException("A district name is required.", nameof(districtName));
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("A user is required to generate the report.", nameof(userId));
        }

        var start = new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero);
        var end = start.AddMonths(1);

        var totalPatients = await _dbContext.Patients.CountAsync(cancellationToken);
        var activePatients = await _dbContext.Patients.CountAsync(patient => patient.IsActive, cancellationToken);
        var visitsCompleted = await _dbContext.FacilityVisits.CountAsync(
            visit => visit.Status == FacilityVisitStatus.Completed && visit.VisitAt >= start && visit.VisitAt < end,
            cancellationToken);
        var welfareChecks = await _dbContext.VhwWelfareChecks.CountAsync(
            check => check.ObservedAt >= start && check.ObservedAt < end,
            cancellationToken);
        var alertsRaised = await _dbContext.Alerts.CountAsync(
            alert => alert.CreatedAt >= start && alert.CreatedAt < end,
            cancellationToken);
        var alertsResolved = await _dbContext.Alerts.CountAsync(
            alert => alert.Status == AlertStatus.Resolved && alert.ResolvedAt >= start && alert.ResolvedAt < end,
            cancellationToken);
        var morbiditySummary = await BuildMorbiditySummaryAsync(start, end, cancellationToken);

        var report = await _dbContext.DistrictReports
            .FirstOrDefaultAsync(
                reportItem => reportItem.DistrictCode == districtCode
                    && reportItem.ReportingYear == year
                    && reportItem.ReportingMonth == month,
                cancellationToken);

        if (report is null)
        {
            report = new DistrictReport
            {
                DistrictCode = districtCode,
                DistrictName = districtName,
                ReportingYear = year,
                ReportingMonth = month,
                Status = DistrictReportStatus.Published,
                GeneratedByUserId = userId
            };
            _dbContext.DistrictReports.Add(report);
        }
        else
        {
            report.DistrictName = districtName;
            report.GeneratedByUserId = userId;
            report.Status = DistrictReportStatus.Published;
        }

        report.PatientCount = totalPatients;
        report.ActivePatients = activePatients;
        report.VisitsCompleted = visitsCompleted;
        report.WelfareChecksCompleted = welfareChecks;
        report.AlertsRaised = alertsRaised;
        report.AlertsResolved = alertsResolved;
        report.MorbiditySummary = morbiditySummary;
        report.GeneratedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return report;
    }

    private async Task<string> BuildMorbiditySummaryAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken)
    {
        var diagnoses = await _dbContext.FacilityVisits
            .AsNoTracking()
            .Where(visit => visit.Status == FacilityVisitStatus.Completed
                && visit.VisitAt >= start
                && visit.VisitAt < end
                && !string.IsNullOrWhiteSpace(visit.Diagnosis))
            .Select(visit => visit.Diagnosis)
            .ToListAsync(cancellationToken);

        return string.Join(
            "; ",
            diagnoses
                .GroupBy(diagnosis => diagnosis.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => new { Diagnosis = group.Key, Count = group.Count() })
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.Diagnosis, StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .Select(item => $"{item.Diagnosis}: {item.Count}"));
    }
}