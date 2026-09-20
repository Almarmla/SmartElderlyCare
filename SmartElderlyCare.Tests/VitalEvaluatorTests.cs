using Microsoft.EntityFrameworkCore;
using SmartElderlyCare.Data;
using SmartElderlyCare.Models;
using SmartElderlyCare.Services;
using Xunit;

namespace SmartElderlyCare.Tests;

public sealed class VitalEvaluatorTests
{
    [Theory]
    [InlineData(36.8, VitalStatus.Normal)]
    [InlineData(38.0, VitalStatus.Warning)]
    [InlineData(34.0, VitalStatus.Critical)]
    [InlineData(40.0, VitalStatus.Critical)]
    public void Evaluate_returns_expected_status_for_temperature(
        decimal value,
        VitalStatus expected)
    {
        using var context = CreateContext(
            new Threshold
            {
                Metric = ThresholdMetric.TemperatureCelsius,
                MinimumValue = 36.0m,
                MaximumValue = 37.5m,
                CriticalLow = 35.0m,
                CriticalHigh = 39.0m,
                IsActive = true
            });

        var evaluator = new VitalEvaluator(context);

        Assert.Equal(expected, evaluator.Evaluate("TemperatureCelsius", value));
    }

    [Fact]
    public void Evaluate_allows_oxygen_saturation_without_critical_high()
    {
        using var context = CreateContext(
            new Threshold
            {
                Metric = ThresholdMetric.OxygenSaturation,
                MinimumValue = 94m,
                MaximumValue = 100m,
                CriticalLow = 90m,
                CriticalHigh = null,
                IsActive = true
            });

        var evaluator = new VitalEvaluator(context);

        Assert.Equal(VitalStatus.Warning, evaluator.Evaluate("OxygenSaturation", 92m));
        Assert.Equal(VitalStatus.Critical, evaluator.Evaluate("OxygenSaturation", 89m));
        Assert.Equal(VitalStatus.Normal, evaluator.Evaluate("OxygenSaturation", 98m));
    }

    private static ApplicationDbContext CreateContext(params Threshold[] thresholds)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new ApplicationDbContext(options);
        context.Thresholds.AddRange(thresholds);
        context.SaveChanges();
        return context;
    }
}
