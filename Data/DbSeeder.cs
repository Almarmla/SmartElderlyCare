using Microsoft.EntityFrameworkCore;
using SmartElderlyCare.Models;

namespace SmartElderlyCare.Data;

public static class DbSeeder
{
    private sealed record ThresholdSeed(
        ThresholdMetric Metric,
        string Name,
        decimal MinimumValue,
        decimal MaximumValue,
        string Unit,
        decimal? CriticalLow,
        decimal? CriticalHigh);

    private static readonly ThresholdSeed[] Thresholds =
    [
        new(ThresholdMetric.TemperatureCelsius, "Temperature", 36.0m, 37.5m, "°C", 35.0m, 39.0m),
        new(ThresholdMetric.PulseRate, "Heart rate", 60m, 100m, "bpm", 40m, 130m),
        new(ThresholdMetric.RespiratoryRate, "Respiratory rate", 12m, 20m, "breaths/min", 8m, 30m),
        new(ThresholdMetric.SystolicBloodPressure, "Systolic blood pressure", 90m, 140m, "mmHg", 80m, 180m),
        new(ThresholdMetric.DiastolicBloodPressure, "Diastolic blood pressure", 60m, 90m, "mmHg", 50m, 120m),
        new(ThresholdMetric.OxygenSaturation, "Oxygen saturation", 94m, 100m, "%", 90m, null),
        new(ThresholdMetric.BloodGlucoseMgDl, "Blood glucose", 70m, 100m, "mg/dL", 54m, 300m)
    ];

    public static async Task SeedThresholdsAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        foreach (var seed in Thresholds)
        {
            if (await dbContext.Thresholds.AnyAsync(
                    threshold => threshold.Metric == seed.Metric
                        && threshold.MedicalCondition == null,
                    cancellationToken))
            {
                continue;
            }

            dbContext.Thresholds.Add(new Threshold
            {
                Name = seed.Name,
                Metric = seed.Metric,
                MinimumValue = seed.MinimumValue,
                MaximumValue = seed.MaximumValue,
                Unit = seed.Unit,
                CriticalLow = seed.CriticalLow,
                CriticalHigh = seed.CriticalHigh,
                Severity = AlertSeverity.Medium,
                IsActive = true
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
