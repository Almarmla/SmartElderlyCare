using Microsoft.EntityFrameworkCore;
using SmartElderlyCare.Data;
using SmartElderlyCare.Models;
using SmartElderlyCare.Services;
using Xunit;

namespace SmartElderlyCare.Tests;

public sealed class PatientRiskEvaluatorTests
{
    [Fact]
    public async Task Evaluate_returns_high_risk_for_systolic_bp_of_160_or_higher()
    {
        using var context = CreateContext();

        context.Patients.Add(new Patient
        {
            PatientNumber = "P-001",
            FirstName = "Jane",
            LastName = "Doe",
            DateOfBirth = new DateOnly(1940, 1, 1),
            MedicalCondition = "Hypertension"
        });
        await context.SaveChangesAsync();
        var patientId = context.Patients.Single().Id;

        context.VitalSignsReadings.Add(new VitalSignsReading
        {
            PatientId = patientId,
            SystolicBloodPressure = 160,
            RecordedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var evaluator = new PatientRiskEvaluator(context);

        var result = await evaluator.EvaluateAsync(patientId);

        Assert.Equal(RiskLevel.High, result.Level);
    }

    [Fact]
    public async Task Evaluate_returns_low_risk_for_no_alerts_and_stable_vhw_check()
    {
        using var context = CreateContext();

        context.Patients.Add(new Patient
        {
            PatientNumber = "P-002",
            FirstName = "Jane",
            LastName = "Doe",
            DateOfBirth = new DateOnly(1940, 1, 1),
            MedicalCondition = "Hypertension"
        });
        await context.SaveChangesAsync();
        var patientId = context.Patients.Single().Id;

        context.VhwWelfareChecks.Add(new VhwWelfareCheck
        {
            PatientId = patientId,
            RecordedByUserId = "user-1",
            ObservedAt = DateTimeOffset.UtcNow,
            Status = VhwWelfareStatus.Stable
        });
        await context.SaveChangesAsync();

        var evaluator = new PatientRiskEvaluator(context);

        var result = await evaluator.EvaluateAsync(patientId);

        Assert.Equal(RiskLevel.Low, result.Level);
    }

    [Fact]
    public async Task Evaluate_returns_high_risk_for_three_unresolved_alerts()
    {
        using var context = CreateContext();

        context.Patients.Add(new Patient
        {
            PatientNumber = "P-003",
            FirstName = "Jane",
            LastName = "Doe",
            DateOfBirth = new DateOnly(1940, 1, 1),
            MedicalCondition = "Hypertension"
        });
        await context.SaveChangesAsync();
        var patientId = context.Patients.Single().Id;

        context.Alerts.AddRange(
            new Alert { PatientId = patientId, Message = "Alert 1", Severity = AlertSeverity.Medium, Status = AlertStatus.Open, Type = AlertType.VitalSigns },
            new Alert { PatientId = patientId, Message = "Alert 2", Severity = AlertSeverity.Low, Status = AlertStatus.Acknowledged, Type = AlertType.VitalSigns },
            new Alert { PatientId = patientId, Message = "Alert 3", Severity = AlertSeverity.Medium, Status = AlertStatus.Open, Type = AlertType.VitalSigns });
        await context.SaveChangesAsync();

        var evaluator = new PatientRiskEvaluator(context);

        var result = await evaluator.EvaluateAsync(patientId);

        Assert.Equal(RiskLevel.High, result.Level);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}