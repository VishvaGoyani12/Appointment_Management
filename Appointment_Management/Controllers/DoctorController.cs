using Appointment_Management.Data;
using Appointment_Management.Models;
using Appointment_Management.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Dynamic.Core;

namespace Appointment_Management.Controllers
{
    [Authorize(Roles = "Admin")]
    public class DoctorController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public DoctorController(ApplicationDbContext context,
                                UserManager<ApplicationUser> userManager,
                                RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Create()
        {
            return PartialView("_Create", new DoctorViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Create(DoctorViewModel model)
        {
            if (!ModelState.IsValid)
                return PartialView("_Create", model);

            // Check if email already exists
            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                return Json(new { success = false, message = "A doctor with this email already exists." });
            }

            var user = new ApplicationUser
            {
                FullName = model.FullName,
                Gender = model.Gender,
                Email = model.Email,
                UserName = model.Email,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description).ToList();
                return Json(new { success = false, message = string.Join("<br/>", errors) });
            }

            if (!await _roleManager.RoleExistsAsync("Doctor"))
                await _roleManager.CreateAsync(new IdentityRole("Doctor"));

            await _userManager.AddToRoleAsync(user, "Doctor");

            var doctor = new Doctor
            {
                ApplicationUserId = user.Id,
                SpecialistIn = model.SpecialistIn,
                Status = model.Status
            };

            _context.Doctors.Add(doctor);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }


        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var doctor = await _context.Doctors
                .Include(d => d.ApplicationUser)
                .FirstOrDefaultAsync(d => d.ApplicationUserId == id);

            if (doctor == null)
                return NotFound();

            var model = new DoctorViewModel
            {
                ApplicationUserId = doctor.ApplicationUserId,
                FullName = doctor.ApplicationUser.FullName,
                Gender = doctor.ApplicationUser.Gender,
                Email = doctor.ApplicationUser.Email,
                SpecialistIn = doctor.SpecialistIn,
                Status = doctor.Status
            };

            return PartialView("_Create", model);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(DoctorViewModel model)
        {
            if (!string.IsNullOrEmpty(model.ApplicationUserId))
            {
                ModelState.Remove(nameof(model.Password));
                ModelState.Remove(nameof(model.ConfirmPassword));
                ModelState.Remove(nameof(model.Email));
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                TempData["Error"] = string.Join("<br/>", errors);
                return PartialView("_Create", model);
            }

            var doctor = await _context.Doctors
                .Include(d => d.ApplicationUser)
                .FirstOrDefaultAsync(d => d.ApplicationUserId == model.ApplicationUserId);

            if (doctor == null)
                return NotFound();

            var user = doctor.ApplicationUser!;
            user.FullName = model.FullName;
            user.Gender = model.Gender;

            var updateUserResult = await _userManager.UpdateAsync(user);
            if (!updateUserResult.Succeeded)
            {
                var errors = updateUserResult.Errors.Select(e => e.Description);
                TempData["Error"] = string.Join("<br/>", errors);
                return PartialView("_Create", model);
            }

            if (!string.IsNullOrWhiteSpace(model.Password))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var passwordResult = await _userManager.ResetPasswordAsync(user, token, model.Password);
                if (!passwordResult.Succeeded)
                {
                    var errors = passwordResult.Errors.Select(e => e.Description);
                    TempData["Error"] = string.Join("<br/>", errors);
                    return PartialView("_Create", model);
                }
            }

            doctor.SpecialistIn = model.SpecialistIn;
            doctor.Status = model.Status;

            _context.Doctors.Update(doctor);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }





        [HttpPost]
        public IActionResult GetAll()
        {
            var draw = HttpContext.Request.Form["draw"].FirstOrDefault();
            var start = Request.Form["start"].FirstOrDefault();
            var length = Request.Form["length"].FirstOrDefault();
            var sortColumnIndex = Request.Form["order[0][column]"].FirstOrDefault();
            var sortColumn = Request.Form[$"columns[{sortColumnIndex}][data]"].FirstOrDefault();
            var sortDirection = Request.Form["order[0][dir]"].FirstOrDefault();
            var searchValue = Request.Form["search[value]"].FirstOrDefault();

            var gender = Request.Form["gender"].FirstOrDefault();
            var status = Request.Form["status"].FirstOrDefault();
            var specialistIn = Request.Form["specialistIn"].FirstOrDefault();

            int pageSize = length != null ? Convert.ToInt32(length) : 0;
            int skip = start != null ? Convert.ToInt32(start) : 0;

            var query = _context.Doctors.Include(d => d.ApplicationUser).AsQueryable();

            if (!string.IsNullOrEmpty(gender))
                query = query.Where(d => d.ApplicationUser.Gender == gender);

            if (!string.IsNullOrEmpty(status) && bool.TryParse(status, out var boolStatus))
                query = query.Where(d => d.Status == boolStatus);

            if (!string.IsNullOrEmpty(specialistIn))
                query = query.Where(d => d.SpecialistIn == specialistIn);

            // Search 
            if (!string.IsNullOrEmpty(searchValue))
            {
                query = query.Where(d =>
                    d.ApplicationUser.FullName.Contains(searchValue) ||
                    d.ApplicationUser.Gender.Contains(searchValue) ||
                    d.SpecialistIn.Contains(searchValue));
            }

            var total = query.Count();

            var sortColumnMap = new Dictionary<string, string>
            {
                ["fullName"] = "ApplicationUser.FullName",
                ["gender"] = "ApplicationUser.Gender",
                ["specialistIn"] = "SpecialistIn",
                ["status"] = "Status"
            };

            if (!string.IsNullOrEmpty(sortColumn) && !string.IsNullOrEmpty(sortDirection))
            {
                if (sortColumnMap.TryGetValue(sortColumn, out var mappedColumn))
                {
                    query = query.OrderBy($"{mappedColumn} {sortDirection}");
                }
            }

            var data = query.Skip(skip).Take(pageSize).Select(d => new
            {
                id = d.ApplicationUserId,
                fullName = d.ApplicationUser.FullName,
                gender = d.ApplicationUser.Gender,
                specialistIn = d.SpecialistIn,
                status = d.Status ? "Active" : "Deactive"
            }).ToList();

            return Json(new
            {
                draw,
                recordsTotal = total,
                recordsFiltered = total,
                data
            });
        }

        [HttpGet]
        public IActionResult GetSpecialistList()
        {
            var specialistList = _context.Doctors
                .Where(d => !string.IsNullOrEmpty(d.SpecialistIn))
                .Select(d => d.SpecialistIn)
                .Distinct()
                .ToList();

            return Json(specialistList);
        }


        [HttpPost]
        public async Task<IActionResult> Delete(string id)
        {
            var doctor = await _context.Doctors
                .Include(d => d.ApplicationUser)
                .FirstOrDefaultAsync(d => d.ApplicationUserId == id);

            if (doctor == null)
                return Json(new { success = false, message = "Doctor not found." });

            bool hasAppointments = await _context.Appointments.AnyAsync(a => a.DoctorId == doctor.Id);
            if (hasAppointments)
            {
                return Json(new { success = false, message = "Doctor cannot be deleted because they have appointments booked." });
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                await _userManager.DeleteAsync(user);
            }

            _context.Doctors.Remove(doctor);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

    }
}
