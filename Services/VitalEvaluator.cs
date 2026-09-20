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

    public VitalStatus Evaluate(string metric, decimal value)
    {
        if (!TryParseMetric(metric, out var parsedMetric))
        {
            throw new ArgumentException($"Unknown vital metric '{metric}'.", nameof(metric));
        }

        var threshold = _dbContext.Thresholds
            .Where(item => item.IsActive && item.Metric == parsedMetric)
            .OrderBy(item => item.MedicalCondition == null ? 0 : 1)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"No active threshold exists for metric '{metric}'.");

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
