using Microsoft.EntityFrameworkCore;
using SmartElderlyCare.Data;
using SmartElderlyCare.Models;

namespace SmartElderlyCare.Services;

public sealed class PatientRiskEvaluator : IPatientRiskEvaluator
{
    private const int RecentWindowDays = 30;

    private readonly ApplicationDbContext _dbContext;

    public PatientRiskEvaluator(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PatientRiskResult> EvaluateAsync(
        int patientId,
        CancellationToken cancellationToken = default)
    {
        var results = await EvaluateBatchAsync(new[] { patientId }, cancellationToken);
        return results[patientId];
    }

    public async Task<IReadOnlyDictionary<int, PatientRiskResult>> EvaluateBatchAsync(
        IEnumerable<int> patientIds,
        CancellationToken cancellationToken = default)
    {
        var ids = patientIds.Distinct().ToArray();
        var results = ids.ToDictionary(
            id => id,
            _ => new PatientRiskResult(RiskLevel.Low, Array.Empty<string>()));
        if (ids.Length == 0)
        {
            return results;
        }

        var windowStart = DateTimeOffset.UtcNow.AddDays(-RecentWindowDays);

        var readings = await _dbContext.VitalSignsReadings
            .AsNoTracking()
            .Where(reading => ids.Contains(reading.PatientId) && reading.RecordedAt >= windowStart)
            .ToListAsync(cancellationToken);

        var alerts = await _dbContext.Alerts
            .AsNoTracking()
            .Where(alert => ids.Contains(alert.PatientId) && alert.Status != AlertStatus.Resolved)
            .ToListAsync(cancellationToken);

        var welfareChecks = await _dbContext.VhwWelfareChecks
            .AsNoTracking()
            .Where(check => ids.Contains(check.PatientId)
                && check.ObservedAt >= windowStart
                && (check.Status == VhwWelfareStatus.Urgent
                    || check.Status == VhwWelfareStatus.NeedsAttention))
            .ToListAsync(cancellationToken);

        foreach (var id in ids)
        {
            var patientReadings = readings.Where(item => item.PatientId == id).ToList();
            var patientAlerts = alerts.Where(item => item.PatientId == id).ToList();
            var patientChecks = welfareChecks.Where(item => item.PatientId == id).ToList();

            results[id] = Evaluate(patientReadings, patientAlerts, patientChecks);
        }

        return results;
    }

    private static PatientRiskResult Evaluate(
        IReadOnlyList<VitalSignsReading> readings,
        IReadOnlyList<Alert> alerts,
        IReadOnlyList<VhwWelfareCheck> welfareChecks)
    {
        var reasons = new List<string>();
        var level = RiskLevel.Low;

        level = Raise(level, EvaluateVitalSigns(readings, reasons));
        level = Raise(level, EvaluateAlerts(alerts, reasons));
        level = Raise(level, EvaluateWelfare(welfareChecks, reasons));

        return new PatientRiskResult(level, reasons);
    }

    private static RiskLevel EvaluateVitalSigns(IReadOnlyList<VitalSignsReading> readings, List<string> reasons)
    {
        var hasSevere = false;
        var hasAbnormal = false;

        foreach (var reading in readings)
        {
            if (IsSevereHypertension(reading)
                || IsSeverePulse(reading)
                || IsSevereBloodGlucose(reading)
                || IsSevereOxygenSaturation(reading))
            {
                hasSevere = true;
            }
            else if (IsElevatedHypertension(reading)
                || IsAbnormalPulse(reading)
                || IsAbnormalBloodGlucose(reading)
                || IsAbnormalOxygenSaturation(reading))
            {
                hasAbnormal = true;
            }
        }

        if (hasSevere)
        {
            reasons.Add("Recent out-of-range vital signs (severe).");
            return RiskLevel.High;
        }

        if (hasAbnormal)
        {
            reasons.Add("Recent out-of-range vital signs.");
            return RiskLevel.Moderate;
        }

        return RiskLevel.Low;
    }

    private static RiskLevel EvaluateAlerts(IReadOnlyList<Alert> alerts, List<string> reasons)
    {
        if (alerts.Count == 0)
        {
            return RiskLevel.Low;
        }

        if (alerts.Any(alert => alert.Severity == AlertSeverity.Critical || alert.Severity == AlertSeverity.High))
        {
            reasons.Add("Unresolved high-severity alert(s).");
        }
        else
        {
            reasons.Add("Unresolved alert(s).");
        }

        return RiskLevel.High;
    }

    private static RiskLevel EvaluateWelfare(IReadOnlyList<VhwWelfareCheck> welfareChecks, List<string> reasons)
    {
        if (welfareChecks.Any(check => check.Status == VhwWelfareStatus.Urgent))
        {
            reasons.Add("Recent urgent welfare check.");
            return RiskLevel.High;
        }

        if (welfareChecks.Count > 0)
        {
            reasons.Add("Recent welfare check needing attention.");
            return RiskLevel.Moderate;
        }

        return RiskLevel.Low;
    }

    private static RiskLevel Raise(RiskLevel current, RiskLevel candidate)
    {
        return (RiskLevel)Math.Max((int)current, (int)candidate);
    }

    private static bool IsSevereHypertension(VitalSignsReading reading)
    {
        return (reading.SystolicBloodPressure.HasValue && reading.SystolicBloodPressure.Value >= 160)
            || (reading.DiastolicBloodPressure.HasValue && reading.DiastolicBloodPressure.Value >= 120);
    }

    private static bool IsElevatedHypertension(VitalSignsReading reading)
    {
        return (reading.SystolicBloodPressure.HasValue && reading.SystolicBloodPressure.Value >= 140)
            || (reading.DiastolicBloodPressure.HasValue && reading.DiastolicBloodPressure.Value >= 90);
    }

    private static bool IsSeverePulse(VitalSignsReading reading)
    {
        return reading.PulseRate.HasValue
            && (reading.PulseRate.Value <= 40 || reading.PulseRate.Value >= 130);
    }

    private static bool IsAbnormalPulse(VitalSignsReading reading)
    {
        return reading.PulseRate.HasValue
            && (reading.PulseRate.Value <= 50 || reading.PulseRate.Value >= 110);
    }

    private static bool IsSevereBloodGlucose(VitalSignsReading reading)
    {
        return reading.BloodGlucoseMgDl.HasValue
            && (reading.BloodGlucoseMgDl.Value < 54m || reading.BloodGlucoseMgDl.Value > 300m);
    }

    private static bool IsAbnormalBloodGlucose(VitalSignsReading reading)
    {
        return reading.BloodGlucoseMgDl.HasValue
            && (reading.BloodGlucoseMgDl.Value < 70m || reading.BloodGlucoseMgDl.Value > 180m);
    }

    private static bool IsSevereOxygenSaturation(VitalSignsReading reading)
    {
        return reading.OxygenSaturation.HasValue && reading.OxygenSaturation.Value < 90m;
    }

    private static bool IsAbnormalOxygenSaturation(VitalSignsReading reading)
    {
        return reading.OxygenSaturation.HasValue && reading.OxygenSaturation.Value < 92m;
    }
}