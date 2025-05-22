using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Appointment_Management.Models
{
    public class Doctor
    {
        public int Id { get; set; }

        [Required]
        public string ApplicationUserId { get; set; }

        [ForeignKey(nameof(ApplicationUserId))]
        public ApplicationUser? ApplicationUser { get; set; }

        public string SpecialistIn { get; set; }
        public bool Status { get; set; }
        public ICollection<Appointment>? Appointments { get; set; }
    }
}
