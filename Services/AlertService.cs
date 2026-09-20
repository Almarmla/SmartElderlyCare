using Microsoft.EntityFrameworkCore;
using SmartElderlyCare.Data;
using SmartElderlyCare.Models;

namespace SmartElderlyCare.Services;

public class AlertService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IVitalEvaluator _vitalEvaluator;

    public AlertService(ApplicationDbContext dbContext, IVitalEvaluator vitalEvaluator)
    {
        _dbContext = dbContext;
        _vitalEvaluator = vitalEvaluator;
    }

    public async Task<IReadOnlyList<Alert>> CheckThresholds(
        VitalSignsReading reading,
        CancellationToken cancellationToken = default)
    {
        var patient = await _dbContext.Patients
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == reading.PatientId, cancellationToken);

        if (patient is null)
        {
            throw new InvalidOperationException($"Patient {reading.PatientId} was not found.");
        }

        var thresholds = await _dbContext.Thresholds
            .Where(threshold => threshold.IsActive
                && (threshold.MedicalCondition == null
                    || threshold.MedicalCondition == patient.MedicalCondition))
            .ToListAsync(cancellationToken);

        var alerts = new List<Alert>();
        foreach (var threshold in thresholds)
        {
            var value = GetReadingValue(reading, threshold.Metric);
            if (!value.HasValue || IsWithinRange(value.Value, threshold))
            {
                continue;
            }

            alerts.Add(new Alert
            {
                PatientId = reading.PatientId,
                ThresholdId = threshold.Id,
                Type = AlertType.VitalSigns,
                Severity = threshold.Severity,
                VitalStatus = _vitalEvaluator.Evaluate(threshold.Metric.ToString(), value.Value),
                Metric = threshold.Metric.ToString(),
                Value = value.Value,
                Message = $"{threshold.Name}: recorded value {value.Value:0.##}{FormatUnit(threshold.Unit)} is outside the configured range.",
                CreatedAt = reading.RecordedAt
            });
        }

        if (alerts.Count > 0)
        {
            _dbContext.Alerts.AddRange(alerts);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return alerts;
    }

    private static decimal? GetReadingValue(VitalSignsReading reading, ThresholdMetric metric)
    {
        return metric switch
        {
            ThresholdMetric.TemperatureCelsius => reading.TemperatureCelsius,
            ThresholdMetric.SystolicBloodPressure => reading.SystolicBloodPressure,
            ThresholdMetric.DiastolicBloodPressure => reading.DiastolicBloodPressure,
            ThresholdMetric.PulseRate => reading.PulseRate,
            ThresholdMetric.RespiratoryRate => reading.RespiratoryRate,
            ThresholdMetric.OxygenSaturation => reading.OxygenSaturation,
            ThresholdMetric.WeightKilograms => reading.WeightKilograms,
            ThresholdMetric.BloodGlucoseMgDl => reading.BloodGlucoseMgDl,
            _ => null
        };
    }

    private static bool IsWithinRange(decimal value, Threshold threshold)
    {
        return (!threshold.MinimumValue.HasValue || value >= threshold.MinimumValue.Value)
            && (!threshold.MaximumValue.HasValue || value <= threshold.MaximumValue.Value);
    }

    private static string FormatUnit(string? unit)
    {
        return string.IsNullOrWhiteSpace(unit) ? string.Empty : $" {unit}";
    }
}
