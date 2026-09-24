using System.ComponentModel.DataAnnotations;

namespace SmartElderlyCare.Models;

public class PatientEditInputModel
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Date of birth")]
    public DateOnly DateOfBirth { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddYears(-60));

    [StringLength(30)]
    public string? Sex { get; set; }

    [StringLength(30)]
    [Display(Name = "National ID")]
    public string? NationalId { get; set; }

    [Required, StringLength(200)]
    [Display(Name = "Chronic condition")]
    public string MedicalCondition { get; set; } = string.Empty;

    [Required, StringLength(200)]
    [Display(Name = "Chronic care programme")]
    public string ChronicCareProgramme { get; set; } = string.Empty;

    [Phone, StringLength(40)]
    [Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    [Required, StringLength(150)]
    [Display(Name = "Emergency contact name")]
    public string EmergencyContactName { get; set; } = string.Empty;

    [Required, Phone, StringLength(40)]
    [Display(Name = "Emergency contact phone")]
    public string EmergencyContactPhone { get; set; } = string.Empty;

    [Display(Name = "Active patient record")]
    public bool IsActive { get; set; } = true;
}