using SmartElderlyCare.Models;

namespace SmartElderlyCare.Services;

public interface IVitalEvaluator
{
    // Async version — preferred; avoids thread-pool starvation (Gap 3 fix).
    Task<VitalStatus> EvaluateAsync(string metric, decimal value, CancellationToken cancellationToken = default);

    // Synchronous overload kept for backward compatibility.
    VitalStatus Evaluate(string metric, decimal value);
}
