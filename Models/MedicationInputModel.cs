using System.ComponentModel.DataAnnotations;

namespace SmartElderlyCare.Models;

public class MedicationInputModel
{
    [Required(ErrorMessage = "Please choose a patient.")]
    [Range(1, int.MaxValue)]
    [Display(Name = "Patient")]
    public int PatientId { get; set; }

    [Required(ErrorMessage = "Please enter the medication name.")]
    [StringLength(200, MinimumLength = 2)]
    [Display(Name = "Medication name")]
    public string MedicationName { get; set; } = string.Empty;

    [StringLength(100)]
    [Display(Name = "Dosage")]
    public string? Dosage { get; set; }

    [StringLength(100)]
    [Display(Name = "Frequency")]
    public string? Frequency { get; set; }

    [StringLength(50)]
    [Display(Name = "Route")]
    public string? Route { get; set; }

    [Display(Name = "Still active")]
    public bool IsActive { get; set; } = true;

    [StringLength(1000)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }
}