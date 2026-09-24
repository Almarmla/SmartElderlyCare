using System.Globalization;
using System.Text;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using SmartElderlyCare.Data;
using SmartElderlyCare.Models;

namespace SmartElderlyCare.Services;

public class Dhis2MonthlyExportService
{
    private static readonly IReadOnlyDictionary<string, string> IndicatorNames =
        new Dictionary<string, string>
        {
            ["vital_signs_readings"] = "Total vital-sign readings",
            ["elevated_blood_pressure_readings"] = "Elevated blood-pressure readings",
            ["welfare_observations"] = "Total welfare observations",
            ["urgent_welfare_observations"] = "Urgent welfare observations",
            ["welfare_observations_needing_attention"] = "Welfare observations needing attention"
        };

    private readonly ApplicationDbContext _dbContext;

    public Dhis2MonthlyExportService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<byte[]> ExportMonthAsCsvAsync(
        int year,
        int month,
        string organisationUnit,
        CancellationToken cancellationToken = default)
    {
        var indicatorRows = await ComputeIndicatorsAsync(year, month, organisationUnit, cancellationToken);

        await using var stream = new MemoryStream();
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        await csv.WriteRecordsAsync(indicatorRows, cancellationToken);
        await writer.FlushAsync(cancellationToken);
        return stream.ToArray();
    }

    public async Task<IReadOnlyList<Dhis2IndicatorSubmission>> RecordSubmissionBatchAsync(
        int year,
        int month,
        string organisationUnit,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), "Month must be between 1 and 12.");
        }

        if (string.IsNullOrWhiteSpace(organisationUnit))
        {
            throw new ArgumentException("An organisation unit is required.", nameof(organisationUnit));
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("A user is required to record the submission.", nameof(userId));
        }

        var indicators = await ComputeIndicatorsAsync(year, month, organisationUnit, cancellationToken);

        var monthlyReturnId = await _dbContext.MonthlyReturns
            .AsNoTracking()
            .Where(returnItem => returnItem.Year == year && returnItem.Month == month)
            .OrderByDescending(returnItem => returnItem.CreatedAt)
            .ThenByDescending(returnItem => returnItem.Id)
            .Select(returnItem => returnItem.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (monthlyReturnId == 0)
        {
            throw new InvalidOperationException(
                $"No monthly return has been compiled for {CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month)} {year}. " +
                "Compile one on the Monthly Returns page before generating the DHIS2 report.");
        }

        var existing = await _dbContext.Dhis2IndicatorSubmissions
            .Where(submission => submission.MonthlyReturnId == monthlyReturnId
                && submission.OrganisationUnitUid == organisationUnit)
            .ToDictionaryAsync(submission => submission.IndicatorUid, cancellationToken);

        var submittedAt = DateTimeOffset.UtcNow;
        var submissions = new List<Dhis2IndicatorSubmission>(indicators.Count);
        foreach (var indicator in indicators)
        {
            if (existing.TryGetValue(indicator.DataElement, out var submission))
            {
                submission.Value = indicator.Value;
                submission.Status = Dhis2SubmissionStatus.Submitted;
                submission.SubmittedAt = submittedAt;
                submissions.Add(submission);
                continue;
            }

            submission = new Dhis2IndicatorSubmission
            {
                MonthlyReturnId = monthlyReturnId,
                CapturedByUserId = userId,
                IndicatorUid = indicator.DataElement,
                IndicatorName = IndicatorNames.TryGetValue(indicator.DataElement, out var indicatorName)
                    ? indicatorName
                    : indicator.DataElement,
                Value = indicator.Value,
                OrganisationUnitUid = organisationUnit,
                Status = Dhis2SubmissionStatus.Submitted,
                SubmittedAt = submittedAt
            };
            _dbContext.Dhis2IndicatorSubmissions.Add(submission);
            submissions.Add(submission);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return submissions;
    }

    private async Task<IReadOnlyList<Dhis2MonthlyIndicator>> ComputeIndicatorsAsync(
        int year,
        int month,
        string organisationUnit,
        CancellationToken cancellationToken)
    {
        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), "Month must be between 1 and 12.");
        }

        if (string.IsNullOrWhiteSpace(organisationUnit))
        {
            throw new ArgumentException("An organisation unit is required.", nameof(organisationUnit));
        }

        var start = new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero);
        var end = start.AddMonths(1);

        var readings = _dbContext.VitalSignsReadings
            .AsNoTracking()
            .Where(reading => reading.RecordedAt >= start && reading.RecordedAt < end);
        var welfareChecks = _dbContext.VhwWelfareChecks
            .AsNoTracking()
            .Where(check => check.ObservedAt >= start && check.ObservedAt < end);

        return
        [
            CreateIndicator("vital_signs_readings", year, month, organisationUnit, await readings.CountAsync(cancellationToken)),
            CreateIndicator(
                "elevated_blood_pressure_readings",
                year,
                month,
                organisationUnit,
                await readings.CountAsync(reading =>
                    reading.SystolicBloodPressure >= 140 || reading.DiastolicBloodPressure >= 90,
                    cancellationToken)),
            CreateIndicator(
                "welfare_observations",
                year,
                month,
                organisationUnit,
                await welfareChecks.CountAsync(cancellationToken)),
            CreateIndicator(
                "urgent_welfare_observations",
                year,
                month,
                organisationUnit,
                await welfareChecks.CountAsync(
                    check => check.Status == VhwWelfareStatus.Urgent,
                    cancellationToken)),
            CreateIndicator(
                "welfare_observations_needing_attention",
                year,
                month,
                organisationUnit,
                await welfareChecks.CountAsync(
                    check => check.Status == VhwWelfareStatus.NeedsAttention,
                    cancellationToken))
        ];
    }

    private static Dhis2MonthlyIndicator CreateIndicator(
        string dataElement,
        int year,
        int month,
        string organisationUnit,
        int value)
    {
        return new Dhis2MonthlyIndicator
        {
            DataElement = dataElement,
            Period = $"{year:0000}{month:00}",
            OrganisationUnit = organisationUnit,
            Value = value
        };
    }
}