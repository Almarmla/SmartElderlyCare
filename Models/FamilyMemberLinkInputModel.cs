using System.ComponentModel.DataAnnotations;

namespace SmartElderlyCare.Models;

public class FamilyMemberLinkInputModel
{
    [Required(ErrorMessage = "Select a patient to link.")]
    [Display(Name = "Patient")]
    public int PatientId { get; set; }

    [Required(ErrorMessage = "Select a registered family member.")]
    [Display(Name = "Family member")]
    public string FamilyMemberUserId { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Access level")]
    public FamilyAccessLevel AccessLevel { get; set; } = FamilyAccessLevel.Viewer;

    [Display(Name = "Relationship to patient")]
    [StringLength(100)]
    public string? RelationshipToPatient { get; set; }
}