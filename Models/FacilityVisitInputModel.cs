using System.ComponentModel.DataAnnotations;

namespace SmartElderlyCare.Models;

public class FacilityVisitInputModel
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Enter a valid patient ID.")]
    [Display(Name = "Patient ID")]
    public int PatientId { get; set; }

    [Required]
    [Range(50, 250, ErrorMessage = "Systolic pressure must be between 50 and 250 mmHg.")]
    [Display(Name = "Systolic blood pressure")]
    public int SystolicBloodPressure { get; set; }

    [Required]
    [Range(30, 150, ErrorMessage = "Diastolic pressure must be between 30 and 150 mmHg.")]
    [Display(Name = "Diastolic blood pressure")]
    public int DiastolicBloodPressure { get; set; }

    [Required]
    [Range(25, 240, ErrorMessage = "Pulse must be between 25 and 240 bpm.")]
    [Display(Name = "Pulse rate")]
    public int PulseRate { get; set; }

    [Required]
    [Range(30, 45, ErrorMessage = "Temperature must be between 30 and 45 °C.")]
    [Display(Name = "Temperature (°C)")]
    public decimal TemperatureCelsius { get; set; }

    [Range(20, 1000, ErrorMessage = "Blood glucose must be between 20 and 1000 mg/dL.")]
    [Display(Name = "Blood glucose (mg/dL)")]
    public decimal? BloodGlucoseMgDl { get; set; }

    [Range(5, 80, ErrorMessage = "Respiratory rate must be between 5 and 80 breaths per minute.")]
    [Display(Name = "Respiratory rate (breaths/min)")]
    public int? RespiratoryRate { get; set; }

    [Range(50, 100, ErrorMessage = "Oxygen saturation must be between 50 and 100%.")]
    [Display(Name = "Oxygen saturation (%)")]
    public decimal? OxygenSaturation { get; set; }

    [Range(1, 500, ErrorMessage = "Weight must be between 1 and 500 kg.")]
    [Display(Name = "Weight (kg)")]
    public decimal? WeightKilograms { get; set; }

    [Required]
    [StringLength(2000, MinimumLength = 2)]
    public string Diagnosis { get; set; } = string.Empty;

    [Required]
    [StringLength(4000, MinimumLength = 2)]
    [Display(Name = "Treatment given")]
    public string Treatment { get; set; } = string.Empty;

    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string FacilityName { get; set; } = string.Empty;

    [StringLength(4000)]
    [Display(Name = "Clinical notes")]
    public string? ClinicalNotes { get; set; }
}
