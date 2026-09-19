using System.Globalization;
using System.Text;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using SmartElderlyCare.Data;
using SmartElderlyCare.Models;

namespace SmartElderlyCare.Services;

public class Dhis2MonthlyExportService
{
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
        var welfareObservations = _dbContext.WelfareObservations
            .AsNoTracking()
            .Where(observation => observation.ObservedAt >= start && observation.ObservedAt < end);

        var indicatorRows = new List<Dhis2MonthlyIndicator>
        {
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
                await welfareObservations.CountAsync(cancellationToken)),
            CreateIndicator(
                "urgent_welfare_observations",
                year,
                month,
                organisationUnit,
                await welfareObservations.CountAsync(
                    observation => observation.Status == WelfareStatus.Urgent,
                    cancellationToken)),
            CreateIndicator(
                "welfare_observations_needing_attention",
                year,
                month,
                organisationUnit,
                await welfareObservations.CountAsync(
                    observation => observation.Status == WelfareStatus.NeedsAttention,
                    cancellationToken))
        };

        await using var stream = new MemoryStream();
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        await csv.WriteRecordsAsync(indicatorRows, cancellationToken);
        await writer.FlushAsync(cancellationToken);
        return stream.ToArray();
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
