using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartElderlyCare.Data;
using SmartElderlyCare.Models;

namespace SmartElderlyCare.Services;

public class AlertService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IVitalEvaluator _vitalEvaluator;
    private readonly ILogger<AlertService> _logger;

    public AlertService(ApplicationDbContext dbContext, IVitalEvaluator vitalEvaluator, ILogger<AlertService> logger)
    {
        _dbContext = dbContext;
        _vitalEvaluator = vitalEvaluator;
        _logger = logger;
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
            if (!value.HasValue)
            {
                continue;
            }

            // Gap 11 fix: check CriticalLow / CriticalHigh first.
            // A critical breach generates its own Critical-severity alert regardless
            // of the normal min/max range, ensuring dangerous values are never missed.
            bool isCriticalBreach = IsCriticalBreach(value.Value, threshold);
            bool isOutOfRange = !IsWithinRange(value.Value, threshold);

            if (!isCriticalBreach && !isOutOfRange)
            {
                continue;
            }

            // Gap 3 & 4 fix: use the async evaluator and catch evaluation errors so
            // one unresolvable metric never prevents other alerts from being saved.
            VitalStatus vitalStatus;
            try
            {
                vitalStatus = await _vitalEvaluator.EvaluateAsync(
                    threshold.Metric.ToString(), value.Value, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "VitalEvaluator could not classify metric '{Metric}' for patient {PatientId}. Defaulting to Warning.",
                    threshold.Metric, reading.PatientId);
                vitalStatus = VitalStatus.Warning;
            }

            alerts.Add(new Alert
            {
                PatientId   = reading.PatientId,
                ThresholdId = threshold.Id,
                Type        = AlertType.VitalSigns,
                // Gap 11 fix: promote to Critical severity when the value crosses
                // the CriticalLow or CriticalHigh boundary.
                Severity    = isCriticalBreach ? AlertSeverity.Critical : threshold.Severity,
                VitalStatus = vitalStatus,
                Metric      = threshold.Metric.ToString(),
                Value       = value.Value,
                Message     = $"{threshold.Name}: recorded value {value.Value:0.##}{FormatUnit(threshold.Unit)} is outside the configured range.",
                CreatedAt   = reading.RecordedAt
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
            ThresholdMetric.TemperatureCelsius     => reading.TemperatureCelsius,
            ThresholdMetric.SystolicBloodPressure  => reading.SystolicBloodPressure,
            ThresholdMetric.DiastolicBloodPressure => reading.DiastolicBloodPressure,
            ThresholdMetric.PulseRate              => reading.PulseRate,
            ThresholdMetric.RespiratoryRate        => reading.RespiratoryRate,
            ThresholdMetric.OxygenSaturation       => reading.OxygenSaturation,
            ThresholdMetric.WeightKilograms        => reading.WeightKilograms,
            ThresholdMetric.BloodGlucoseMgDl       => reading.BloodGlucoseMgDl,
            _                                      => null
        };
    }

    private static bool IsWithinRange(decimal value, Threshold threshold)
    {
        return (!threshold.MinimumValue.HasValue || value >= threshold.MinimumValue.Value)
            && (!threshold.MaximumValue.HasValue || value <= threshold.MaximumValue.Value);
    }

    // Gap 11 fix: dedicated check for the critical boundary.
    private static bool IsCriticalBreach(decimal value, Threshold threshold)
    {
        return (threshold.CriticalLow.HasValue  && value < threshold.CriticalLow.Value)
            || (threshold.CriticalHigh.HasValue && value > threshold.CriticalHigh.Value);
    }

    private static string FormatUnit(string? unit)
    {
        return string.IsNullOrWhiteSpace(unit) ? string.Empty : $" {unit}";
    }
}

