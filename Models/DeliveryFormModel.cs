// DeliveryFormModel.cs
// The data model for the New Entry form page — holds exactly what the user types.
//
// This is intentionally separate from DeliveryRecord (the DB entity). The DB entity has
// fields like Id and TimestampUtc that the user should never see or set; this model
// contains only the three fields the user actually fills in.
//
// The [Required] and [Range] attributes are read by Blazor's DataAnnotationsValidator
// and shown as error messages when the user tries to submit an invalid form.

using System.ComponentModel.DataAnnotations;

namespace timezone_manager.Models;

public class DeliveryFormModel
{
    [Required(ErrorMessage = "Delivery Number is required")]
    [Range(1, long.MaxValue, ErrorMessage = "Must be a positive number")]
    public long DeliveryNumber { get; set; }

    [Required(ErrorMessage = "Plant is required")]
    [MaxLength(50)]
    public string Plant { get; set; } = "";

    [Required(ErrorMessage = "Material is required")]
    [MaxLength(100)]
    public string Material { get; set; } = "";
}
