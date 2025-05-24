using System.ComponentModel.DataAnnotations;

namespace Appointment_Management.Models.ViewModels
{
    public class DoctorViewModel
    {
        public string? ApplicationUserId { get; set; }

        [Required]
        public string FullName { get; set; }

        [Required]
        public string Gender { get; set; }

        [EmailAddress]
        [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Please enter a valid email address.")]
        public string Email { get; set; }

        [DataType(DataType.Password)]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }

        [Required]
        public string SpecialistIn { get; set; }

        public bool Status { get; set; }
    }

}
