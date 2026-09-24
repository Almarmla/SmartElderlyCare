using Microsoft.EntityFrameworkCore;
using SmartElderlyCare.Data;
using SmartElderlyCare.Models;
using SmartElderlyCare.Services;
using Xunit;

namespace SmartElderlyCare.Tests;

public sealed class AlertServiceTests
{
    [Fact]
    public async Task CheckThresholds_saves_one_alert_when_systolic_exceeds_maximum_threshold()
    {
        using var context = CreateContext(
            new Threshold
            {
                Name = "Systolic BP",
                Metric = ThresholdMetric.SystolicBloodPressure,
                MaximumValue = 140m,
                Unit = "mmHg",
                Severity = AlertSeverity.High,
                IsActive = true
            });

        context.Patients.Add(new Patient
        {
            PatientNumber = "P-101",
            FirstName = "John",
            LastName = "Smith",
            DateOfBirth = new DateOnly(1938, 3, 12),
            MedicalCondition = "Hypertension"
        });
        await context.SaveChangesAsync();

        var reading = new VitalSignsReading
        {
            PatientId = context.Patients.Single().Id,
            SystolicBloodPressure = 170,
            RecordedAt = DateTimeOffset.UtcNow
        };

        var service = new AlertService(context, new VitalEvaluator(context));

        var alerts = await service.CheckThresholds(reading);

        Assert.Single(alerts);
        Assert.Equal(AlertType.VitalSigns, alerts[0].Type);
        Assert.Equal(ThresholdMetric.SystolicBloodPressure.ToString(), alerts[0].Metric);
        Assert.Equal(170m, alerts[0].Value);
        Assert.Equal(1, context.Alerts.Count());
    }

    [Fact]
    public async Task CheckThresholds_saves_no_alerts_when_reading_is_within_all_thresholds()
    {
        using var context = CreateContext(
            new Threshold
            {
                Name = "Systolic BP",
                Metric = ThresholdMetric.SystolicBloodPressure,
                MaximumValue = 140m,
                Unit = "mmHg",
                Severity = AlertSeverity.High,
                IsActive = true
            });

        context.Patients.Add(new Patient
        {
            PatientNumber = "P-102",
            FirstName = "John",
            LastName = "Smith",
            DateOfBirth = new DateOnly(1938, 3, 12),
            MedicalCondition = "Hypertension"
        });
        await context.SaveChangesAsync();

        var reading = new VitalSignsReading
        {
            PatientId = context.Patients.Single().Id,
            SystolicBloodPressure = 120,
            RecordedAt = DateTimeOffset.UtcNow
        };

        var service = new AlertService(context, new VitalEvaluator(context));

        var alerts = await service.CheckThresholds(reading);

        Assert.Empty(alerts);
        Assert.Equal(0, context.Alerts.Count());
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