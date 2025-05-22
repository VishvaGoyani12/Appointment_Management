using System.ComponentModel.DataAnnotations;

namespace Appointment_Management.Models.ViewModels
{
    public class AppointmentViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Please select a doctor.")]
        public int DoctorId { get; set; }

        [Required(ErrorMessage = "Please select a patient.")]
        public int PatientId { get; set; }

        [Required(ErrorMessage = "Please select an appointment date.")]
        public DateTime AppointmentDate { get; set; }

        [Required(ErrorMessage = "Description is required.")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Please select status.")]
        public string Status { get; set; }
    }

}
