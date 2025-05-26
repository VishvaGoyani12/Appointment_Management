using Appointment_Management.Data;
using Appointment_Management.Helper;
using Appointment_Management.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq.Dynamic.Core;


namespace Appointment_Management.Controllers
{
    [Authorize(Roles = "Doctor")]
    public class DoctorAppointmentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DoctorAppointmentController(ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
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
            try
            {
                var jwtUser = JwtHelper.GetJwtUser(HttpContext);

                if (!jwtUser.IsAuthenticated || string.IsNullOrEmpty(jwtUser.Email))
                {
                    return Json(new { draw = Request.Form["draw"].FirstOrDefault(), error = "User not authenticated" });
                }

                var doctor = await _context.Doctors
                    .Include(d => d.ApplicationUser)
                    .FirstOrDefaultAsync(d => d.ApplicationUser.Email == jwtUser.Email);

                if (doctor == null)
                {
                    return Json(new { draw = Request.Form["draw"].FirstOrDefault(), error = "Doctor profile not found" });
                }

                var draw = Request.Form["draw"].FirstOrDefault();
                var start = Request.Form["start"].FirstOrDefault();
                var length = Request.Form["length"].FirstOrDefault();
                var searchValue = Request.Form["search[value]"].FirstOrDefault();

                // Sorting
                var sortColumnIndex = Request.Form["order[0][column]"].FirstOrDefault();
                var sortColumnName = Request.Form[$"columns[{sortColumnIndex}][data]"].FirstOrDefault();
                var sortDirection = Request.Form["order[0][dir]"].FirstOrDefault(); 

                int pageSize = length != null ? Convert.ToInt32(length) : 10;
                int skip = start != null ? Convert.ToInt32(start) : 0;

                var query = _context.Appointments
                    .Include(a => a.Patient)
                        .ThenInclude(p => p.ApplicationUser)
                    .Where(a => a.DoctorId == doctor.Id);

                // Filter by status
                var status = Request.Form["status"].FirstOrDefault();
                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(a => a.Status == status);
                }

                // Global search
                if (!string.IsNullOrEmpty(searchValue))
                {
                    searchValue = searchValue.ToLower();
                    query = query.Where(a =>
                        a.Patient.ApplicationUser.FullName.ToLower().Contains(searchValue) ||
                        a.Description.ToLower().Contains(searchValue) ||
                        a.Status.ToLower().Contains(searchValue) ||
                        a.AppointmentDate.ToString().ToLower().Contains(searchValue));
                }

                var totalRecords = await query.CountAsync();

                if (!string.IsNullOrEmpty(sortColumnName) && !string.IsNullOrEmpty(sortDirection))
                {
                    switch (sortColumnName)
                    {
                        case "patientName":
                            sortColumnName = "Patient.ApplicationUser.FullName";
                            break;
                        case "appointmentDate":
                            sortColumnName = "AppointmentDate";
                            break;
                        case "description":
                            sortColumnName = "Description";
                            break;
                        case "status":
                            sortColumnName = "Status";
                            break;
                        default:
                            sortColumnName = "AppointmentDate";
                            break;
                    }

                    query = query.OrderBy($"{sortColumnName} {sortDirection}");
                }
                else
                {
                    query = query.OrderByDescending(a => a.AppointmentDate); 
                }

                var appointments = await query
                    .Skip(skip)
                    .Take(pageSize)
                    .Select(a => new
                    {
                        id = a.Id,
                        patientName = a.Patient.ApplicationUser.FullName,
                        appointmentDate = a.AppointmentDate.ToString("yyyy-MM-dd"),
                        description = a.Description,
                        status = a.Status
                    })
                    .ToListAsync();

                return Json(new
                {
                    draw,
                    recordsFiltered = totalRecords,
                    recordsTotal = totalRecords,
                    data = appointments
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    draw = Request.Form["draw"].FirstOrDefault(),
                    error = "An error occurred",
                    details = ex.Message
                });
            }
        }



        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            try
            {
                var jwtUser = JwtHelper.GetJwtUser(HttpContext);

                if (!jwtUser.IsAuthenticated || string.IsNullOrEmpty(jwtUser.Email))
                {
                    return Json(new { success = false, message = "User not authenticated" });
                }

                var doctor = await _context.Doctors
                    .FirstOrDefaultAsync(d => d.ApplicationUser.Email == jwtUser.Email);

                if (doctor == null)
                {
                    return Json(new { success = false, message = "Doctor profile not found" });
                }

                var appointment = await _context.Appointments
                    .FirstOrDefaultAsync(a => a.Id == id && a.DoctorId == doctor.Id);

                if (appointment == null)
                {
                    return Json(new { success = false, message = "Appointment not found" });
                }

                appointment.Status = status;
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "An error occurred",
                    details = ex.Message
                });
            }
        }

    }
}
