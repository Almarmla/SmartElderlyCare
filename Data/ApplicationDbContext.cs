using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SmartElderlyCare.Models;

namespace SmartElderlyCare.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Patient> Patients => Set<Patient>();

    public DbSet<PatientFamilyMember> PatientFamilyMembers => Set<PatientFamilyMember>();

    public DbSet<VitalSignsReading> VitalSignsReadings => Set<VitalSignsReading>();

    public DbSet<FacilityVisit> FacilityVisits => Set<FacilityVisit>();

    public DbSet<PatientMedication> PatientMedications => Set<PatientMedication>();

    public DbSet<WelfareObservation> WelfareObservations => Set<WelfareObservation>();

    public DbSet<VhwWelfareCheck> VhwWelfareChecks => Set<VhwWelfareCheck>();

    public DbSet<Threshold> Thresholds => Set<Threshold>();

    public DbSet<Alert> Alerts => Set<Alert>();

    public DbSet<MonthlyReturn> MonthlyReturns => Set<MonthlyReturn>();

    public DbSet<MonthlyReturnValidation> MonthlyReturnValidations => Set<MonthlyReturnValidation>();

    public DbSet<Dhis2IndicatorSubmission> Dhis2IndicatorSubmissions => Set<Dhis2IndicatorSubmission>();

    public DbSet<AdministrationAuditLog> AdministrationAuditLogs => Set<AdministrationAuditLog>();

    public DbSet<DistrictReport> DistrictReports => Set<DistrictReport>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Patient>(entity =>
        {
            entity.HasIndex(patient => patient.PatientNumber).IsUnique();
            entity.Property(patient => patient.PatientNumber).HasMaxLength(40).IsRequired();
            entity.Property(patient => patient.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(patient => patient.LastName).HasMaxLength(100).IsRequired();
            entity.Property(patient => patient.Sex).HasMaxLength(30);
            entity.Property(patient => patient.MedicalCondition).HasMaxLength(200);
            entity.Property(patient => patient.ChronicCareProgramme).HasMaxLength(200);
            entity.HasOne(patient => patient.AssignedUser)
                .WithMany(user => user.AssignedPatients)
                .HasForeignKey(patient => patient.AssignedUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<PatientFamilyMember>(entity =>
        {
            entity.HasKey(link => new { link.PatientId, link.FamilyMemberUserId });
            entity.Property(link => link.AccessLevel).HasConversion<string>().HasMaxLength(20);
            entity.Property(link => link.RelationshipToPatient).HasMaxLength(100);
            entity.HasOne(link => link.Patient)
                .WithMany(patient => patient.FamilyMembers)
                .HasForeignKey(link => link.PatientId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(link => link.FamilyMemberUser)
                .WithMany(user => user.FamilyPatientLinks)
                .HasForeignKey(link => link.FamilyMemberUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.DisplayName).HasMaxLength(200);
            entity.Property(user => user.FacilityName).HasMaxLength(200);
        });

        builder.Entity<VitalSignsReading>(entity =>
        {
            entity.Property(reading => reading.TemperatureCelsius).HasPrecision(5, 2);
            entity.Property(reading => reading.OxygenSaturation).HasPrecision(5, 2);
            entity.Property(reading => reading.BloodGlucoseMgDl).HasPrecision(6, 2);
            entity.Property(reading => reading.WeightKilograms).HasPrecision(6, 2);
            entity.HasOne(reading => reading.Patient)
                .WithMany(patient => patient.VitalSignsReadings)
                .HasForeignKey(reading => reading.PatientId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(reading => reading.RecordedByUser)
                .WithMany(user => user.RecordedVitalSigns)
                .HasForeignKey(reading => reading.RecordedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<FacilityVisit>(entity =>
        {
            entity.Property(visit => visit.FacilityName).HasMaxLength(200).IsRequired();
            entity.Property(visit => visit.Diagnosis).HasMaxLength(2000).IsRequired();
            entity.Property(visit => visit.Treatment).HasMaxLength(4000).IsRequired();
            entity.Property(visit => visit.ClinicalNotes).HasMaxLength(4000);
            entity.Property(visit => visit.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasOne(visit => visit.Patient)
                .WithMany(patient => patient.FacilityVisits)
                .HasForeignKey(visit => visit.PatientId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(visit => visit.RecordedByUser)
                .WithMany(user => user.FacilityVisits)
                .HasForeignKey(visit => visit.RecordedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(visit => visit.VitalSignsReading)
                .WithMany(reading => reading.FacilityVisits)
                .HasForeignKey(visit => visit.VitalSignsReadingId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<PatientMedication>(entity =>
        {
            entity.Property(medication => medication.MedicationName).HasMaxLength(200).IsRequired();
            entity.Property(medication => medication.Dosage).HasMaxLength(100);
            entity.Property(medication => medication.Frequency).HasMaxLength(100);
            entity.Property(medication => medication.Route).HasMaxLength(50);
            entity.Property(medication => medication.Notes).HasMaxLength(1000);
            entity.HasOne(medication => medication.Patient)
                .WithMany(patient => patient.Medications)
                .HasForeignKey(medication => medication.PatientId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(medication => medication.RecordedByUser)
                .WithMany()
                .HasForeignKey(medication => medication.RecordedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<WelfareObservation>(entity =>
        {
            entity.Property(observation => observation.Status).HasConversion<string>().HasMaxLength(30);
            entity.HasOne(observation => observation.Patient)
                .WithMany(patient => patient.WelfareObservations)
                .HasForeignKey(observation => observation.PatientId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(observation => observation.RecordedByUser)
                .WithMany(user => user.RecordedWelfareObservations)
                .HasForeignKey(observation => observation.RecordedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<VhwWelfareCheck>(entity =>
        {
            entity.Property(check => check.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(check => check.Mood).HasMaxLength(500);
            entity.Property(check => check.Mobility).HasMaxLength(500);
            entity.Property(check => check.Nutrition).HasMaxLength(500);
            entity.Property(check => check.SafetyConcern).HasMaxLength(1000);
            entity.Property(check => check.Notes).HasMaxLength(2000);
            entity.HasOne(check => check.Patient)
                .WithMany(patient => patient.VhwWelfareChecks)
                .HasForeignKey(check => check.PatientId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(check => check.RecordedByUser)
                .WithMany(user => user.VhwWelfareChecks)
                .HasForeignKey(check => check.RecordedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Threshold>(entity =>
        {
            entity.Property(threshold => threshold.Name).HasMaxLength(100).IsRequired();
            entity.Property(threshold => threshold.MedicalCondition).HasMaxLength(200);
            entity.Property(threshold => threshold.Metric).HasConversion<string>().HasMaxLength(40);
            entity.Property(threshold => threshold.Severity).HasConversion<string>().HasMaxLength(20);
            entity.Property(threshold => threshold.MinimumValue).HasPrecision(10, 2);
            entity.Property(threshold => threshold.MaximumValue).HasPrecision(10, 2);
            entity.Property(threshold => threshold.CriticalLow).HasPrecision(10, 2);
            entity.Property(threshold => threshold.CriticalHigh).HasPrecision(10, 2);
        });

        builder.Entity<AdministrationAuditLog>(entity =>
        {
            entity.Property(log => log.Action).HasConversion<string>().HasMaxLength(30);
            entity.Property(log => log.EntityName).HasMaxLength(100).IsRequired();
            entity.Property(log => log.EntityId).HasMaxLength(100).IsRequired();
            entity.Property(log => log.Details).HasMaxLength(4000);
            entity.HasOne(log => log.PerformedByUser)
                .WithMany(user => user.AdministrationAuditLogs)
                .HasForeignKey(log => log.PerformedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(log => log.Threshold)
                .WithMany(threshold => threshold.AdministrationAuditLogs)
                .HasForeignKey(log => log.ThresholdId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(log => log.AffectedUser)
                .WithMany()
                .HasForeignKey(log => log.AffectedUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<DistrictReport>(entity =>
        {
            entity.Property(report => report.DistrictCode).HasMaxLength(50).IsRequired();
            entity.Property(report => report.DistrictName).HasMaxLength(200).IsRequired();
            entity.Property(report => report.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(report => report.MorbiditySummary).HasMaxLength(4000);
            entity.HasIndex(report => new
            {
                report.DistrictCode,
                report.ReportingYear,
                report.ReportingMonth
            }).IsUnique();
            entity.HasOne(report => report.GeneratedByUser)
                .WithMany(user => user.DistrictReports)
                .HasForeignKey(report => report.GeneratedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Alert>(entity =>
        {
            entity.Property(alert => alert.Type).HasConversion<string>().HasMaxLength(30);
            entity.Property(alert => alert.Severity).HasConversion<string>().HasMaxLength(20);
            entity.Property(alert => alert.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(alert => alert.VitalStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(alert => alert.Metric).HasMaxLength(40);
            entity.Property(alert => alert.Value).HasPrecision(10, 2);
            entity.Property(alert => alert.Message).HasMaxLength(1000).IsRequired();
            entity.HasOne(alert => alert.Patient)
                .WithMany(patient => patient.Alerts)
                .HasForeignKey(alert => alert.PatientId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(alert => alert.Threshold)
                .WithMany()
                .HasForeignKey(alert => alert.ThresholdId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(alert => alert.AssignedToUser)
                .WithMany(user => user.AssignedAlerts)
                .HasForeignKey(alert => alert.AssignedToUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<MonthlyReturn>(entity =>
        {
            entity.HasIndex(returnItem => new { returnItem.PatientId, returnItem.Year, returnItem.Month }).IsUnique();
            entity.Property(returnItem => returnItem.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasOne(returnItem => returnItem.Patient)
                .WithMany(patient => patient.MonthlyReturns)
                .HasForeignKey(returnItem => returnItem.PatientId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(returnItem => returnItem.SubmittedByUser)
                .WithMany(user => user.SubmittedMonthlyReturns)
                .HasForeignKey(returnItem => returnItem.SubmittedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(returnItem => returnItem.ReviewedByUser)
                .WithMany(user => user.ReviewedMonthlyReturns)
                .HasForeignKey(returnItem => returnItem.ReviewedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.Property(returnItem => returnItem.ReviewNotes).HasMaxLength(4000);
        });

        builder.Entity<MonthlyReturnValidation>(entity =>
        {
            entity.Property(validation => validation.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(validation => validation.Notes).HasMaxLength(4000);
            entity.HasOne(validation => validation.MonthlyReturn)
                .WithMany()
                .HasForeignKey(validation => validation.MonthlyReturnId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(validation => validation.ValidatedByUser)
                .WithMany()
                .HasForeignKey(validation => validation.ValidatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Dhis2IndicatorSubmission>(entity =>
        {
            entity.Property(submission => submission.IndicatorUid).HasMaxLength(100).IsRequired();
            entity.Property(submission => submission.IndicatorName).HasMaxLength(200).IsRequired();
            entity.Property(submission => submission.OrganisationUnitUid).HasMaxLength(100).IsRequired();
            entity.Property(submission => submission.Value).HasPrecision(18, 2);
            entity.Property(submission => submission.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(submission => submission.Dhis2ResponseId).HasMaxLength(200);
            entity.Property(submission => submission.ErrorMessage).HasMaxLength(4000);
            entity.HasOne(submission => submission.MonthlyReturn)
                .WithMany(returnItem => returnItem.Dhis2IndicatorSubmissions)
                .HasForeignKey(submission => submission.MonthlyReturnId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(submission => submission.CapturedByUser)
                .WithMany(user => user.Dhis2IndicatorSubmissions)
                .HasForeignKey(submission => submission.CapturedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(submission => new
            {
                submission.MonthlyReturnId,
                submission.IndicatorUid,
                submission.OrganisationUnitUid
            }).IsUnique();
        });
    }
}
