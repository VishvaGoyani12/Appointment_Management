using Appointment_Management.Data;
using Appointment_Management.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;

namespace Appointment_Management.Controllers
{
    [Authorize(Roles = "Doctor")]
    public class DoctorAppointmentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DoctorAppointmentController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GetMyAppointments()
        {
            var user = await _userManager.GetUserAsync(User);
            var doctor = await _context.Doctors
                .Include(d => d.ApplicationUser)
                .FirstOrDefaultAsync(d => d.ApplicationUserId == user.Id);

            if (doctor == null)
            {
                return Unauthorized();
            }

            var draw = Request.Form["draw"].FirstOrDefault();
            var start = Request.Form["start"].FirstOrDefault();
            var length = Request.Form["length"].FirstOrDefault();
            var status = Request.Form["status"].FirstOrDefault();

            int pageSize = length != null ? Convert.ToInt32(length) : 0;
            int skip = start != null ? Convert.ToInt32(start) : 0;

            var query = _context.Appointments
                .Include(a => a.Patient)
                    .ThenInclude(p => p.ApplicationUser)
                .Where(a => a.DoctorId == doctor.Id);

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(a => a.Status == status);
            }

            var totalRecords = await query.CountAsync();

            var appointments = await query
                .OrderByDescending(a => a.AppointmentDate)
                .Skip(skip)
                .Take(pageSize)
                .Select(a => new
                {
                    id = a.Id,
                    patientName = a.Patient.ApplicationUser.FullName,
                    appointmentDate = a.AppointmentDate.ToString("yyyy-MM-dd HH:mm"),
                    description = a.Description,
                    status = a.Status
                })
                .ToListAsync();

            return Json(new
            {
                draw = draw,
                recordsFiltered = totalRecords,
                recordsTotal = totalRecords,
                data = appointments
            });
        }



        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            var user = await _userManager.GetUserAsync(User);
            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.ApplicationUserId == user.Id);

            if (doctor == null)
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            var appointment = await _context.Appointments.FindAsync(id);

            if (appointment == null || appointment.DoctorId != doctor.Id)
            {
                return Json(new { success = false, message = "Appointment not found" });
            }

            appointment.Status = status;
            _context.Appointments.Update(appointment);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

    }
}
