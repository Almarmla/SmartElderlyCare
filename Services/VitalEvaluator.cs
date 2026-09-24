using Microsoft.EntityFrameworkCore;
using SmartElderlyCare.Data;
using SmartElderlyCare.Models;

namespace SmartElderlyCare.Services;

public sealed class VitalEvaluator : IVitalEvaluator
{
    private readonly ApplicationDbContext _dbContext;

    public VitalEvaluator(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // Gap 3 fix: now async — uses FirstOrDefaultAsync to avoid thread-pool starvation.
    // Gap 4 fix: returns VitalStatus.Warning as a safe default when no active threshold
    //            is configured, instead of throwing and killing the entire alert batch.
    public async Task<VitalStatus> EvaluateAsync(string metric, decimal value, CancellationToken cancellationToken = default)
    {
        if (!TryParseMetric(metric, out var parsedMetric))
        {
            // Unknown metric name — treat as warning so callers can still proceed.
            return VitalStatus.Warning;
        }

        var threshold = await _dbContext.Thresholds
            .Where(item => item.IsActive && item.Metric == parsedMetric)
            .OrderBy(item => item.MedicalCondition == null ? 0 : 1)
            .FirstOrDefaultAsync(cancellationToken);

        // Gap 4 fix: no threshold configured — return Warning instead of throwing.
        if (threshold is null)
        {
            return VitalStatus.Warning;
        }

        if ((threshold.CriticalLow.HasValue && value < threshold.CriticalLow.Value)
            || (threshold.CriticalHigh.HasValue && value > threshold.CriticalHigh.Value))
        {
            return VitalStatus.Critical;
        }

        if ((threshold.MinimumValue.HasValue && value < threshold.MinimumValue.Value)
            || (threshold.MaximumValue.HasValue && value > threshold.MaximumValue.Value))
        {
            return VitalStatus.Warning;
        }

        return VitalStatus.Normal;
    }

    // Keep the synchronous overload for any existing callers during transition.
    public VitalStatus Evaluate(string metric, decimal value)
    {
        return EvaluateAsync(metric, value).GetAwaiter().GetResult();
    }

    private static bool TryParseMetric(string metric, out ThresholdMetric parsedMetric)
    {
        parsedMetric = metric.Trim().ToLowerInvariant() switch
        {
            "temperature" or "temperaturecelsius" => ThresholdMetric.TemperatureCelsius,
            "heartrate" or "pulserate" => ThresholdMetric.PulseRate,
            "respiratoryrate" => ThresholdMetric.RespiratoryRate,
            "systolicbp" or "systolicbloodpressure" => ThresholdMetric.SystolicBloodPressure,
            "diastolicbp" or "diastolicbloodpressure" => ThresholdMetric.DiastolicBloodPressure,
            "oxygensaturation" => ThresholdMetric.OxygenSaturation,
            "bloodglucose" or "bloodglucosemgdl" => ThresholdMetric.BloodGlucoseMgDl,
            "weight" or "weightkilograms" => ThresholdMetric.WeightKilograms,
            _ => default
        };

        return metric.Trim().ToLowerInvariant() is
            "temperature" or "temperaturecelsius" or "heartrate" or "pulserate"
            or "respiratoryrate" or "systolicbp" or "systolicbloodpressure"
            or "diastolicbp" or "diastolicbloodpressure" or "oxygensaturation"
            or "bloodglucose" or "bloodglucosemgdl" or "weight" or "weightkilograms";
    }
}
