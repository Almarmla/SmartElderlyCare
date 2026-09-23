namespace SmartElderlyCare.Services;

public enum RiskLevel
{
    Low,
    Moderate,
    High
}

public sealed record PatientRiskResult(RiskLevel Level, IReadOnlyList<string> Reasons);

public interface IPatientRiskEvaluator
{
    Task<PatientRiskResult> EvaluateAsync(int patientId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<int, PatientRiskResult>> EvaluateBatchAsync(
        IEnumerable<int> patientIds,
        CancellationToken cancellationToken = default);
}