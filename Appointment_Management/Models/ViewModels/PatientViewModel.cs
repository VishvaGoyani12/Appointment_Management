using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Appointment_Management.Models.ViewModels
{
    public class PatientViewModel
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Gender { get; set; }
        public DateTime JoinDate { get; set; }
        public bool Status { get; set; }
    }

}
