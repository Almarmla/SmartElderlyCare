using SmartElderlyCare.Models;

namespace SmartElderlyCare.Services;

public interface IVitalEvaluator
{
    VitalStatus Evaluate(string metric, decimal value);
}
