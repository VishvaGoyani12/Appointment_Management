using Microsoft.AspNetCore.Identity;
using System.Numerics;

namespace Appointment_Management.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } 
        public string Gender { get; set; }   
        public DateTime CreatedAt { get; set; }
        public Patient? Patient { get; set; } 
        public Doctor? Doctor { get; set; }  
    }
}
