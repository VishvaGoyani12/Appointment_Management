using System.ComponentModel.DataAnnotations;

namespace Appointment_Management.Models.ViewModels
{
    public class DoctorViewModel
    {
        public string? ApplicationUserId { get; set; }

        public string FullName { get; set; }
        public string Gender { get; set; }

        [Required]
        [EmailAddress]
        [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Please enter a valid email address.")]
        public string Email { get; set; }

        public string? Password { get; set; }

        public string SpecialistIn { get; set; }
        public bool Status { get; set; }
    }
}
