using System.ComponentModel.DataAnnotations;

namespace Appointment_Management.Models.ViewModels
{
    public class ChangeEmailViewModel
    {
        public string CurrentEmail { get; set; }

        [Required]
        [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Please enter a valid email address.")]
        [Display(Name = "New Email")]
        public string NewEmail { get; set; }
    }

}
