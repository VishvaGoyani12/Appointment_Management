using Appointment_Management.Data;
using Appointment_Management.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Dynamic.Core;


namespace Appointment_Management.Controllers
{
    [Authorize(Roles = "Admin")]
    public class PatientController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PatientController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index() => View();

        [HttpPost]
        public IActionResult GetAll()
        {
            var draw = Request.Form["draw"].FirstOrDefault();
            int start = int.Parse(Request.Form["start"]);
            int length = int.Parse(Request.Form["length"]);
            var sortColumnIndex = int.Parse(Request.Form["order[0][column]"]);
            var sortColumn = Request.Form[$"columns[{sortColumnIndex}][data]"];
            var sortDir = Request.Form["order[0][dir]"];
            var searchValue = Request.Form["search[value]"];

            var gender = Request.Form["gender"].ToString();
            var status = Request.Form["status"].ToString();
            var joinDate = Request.Form["joinDate"].ToString();

            // Include ApplicationUser to access Name, Gender
            var query = _context.Patients.Include(p => p.ApplicationUser).AsQueryable();

            // Search
            if (!string.IsNullOrEmpty(searchValue))
            {
                query = query.Where(p =>
                    (p.ApplicationUser.FullName != null && p.ApplicationUser.FullName.Contains(searchValue)) ||
                    (p.ApplicationUser.Gender != null && p.ApplicationUser.Gender.Contains(searchValue)) ||
                    p.JoinDate.ToString().Contains(searchValue) ||
                    (p.Status ? "Active" : "Deactive").Contains(searchValue)
                );
            }

            // Filters
            if (!string.IsNullOrEmpty(gender))
                query = query.Where(p => p.ApplicationUser.Gender == gender);

            if (!string.IsNullOrEmpty(status) && bool.TryParse(status, out var boolStatus))
                query = query.Where(p => p.Status == boolStatus);

            if (!string.IsNullOrEmpty(joinDate) && DateTime.TryParse(joinDate, out var parsedDate))
                query = query.Where(p => p.JoinDate.Date == parsedDate.Date);

            int recordsTotal = query.Count();

            // Sorting
            if (!string.IsNullOrEmpty(sortColumn) && !string.IsNullOrEmpty(sortDir))
            {
                // For sorting on FullName or Gender, adjust property name with ApplicationUser
                if (sortColumn == "name")
                    sortColumn = "ApplicationUser.FullName";
                else if (sortColumn == "gender")
                    sortColumn = "ApplicationUser.Gender";

                query = query.OrderBy($"{sortColumn} {sortDir}");
            }

            // Paging and projection
            var data = query.Skip(start).Take(length)
                .Select(p => new
                {
                    p.Id,
                    Name = p.ApplicationUser.FullName,
                    Gender = p.ApplicationUser.Gender,
                    JoinDate = p.JoinDate.ToString("yyyy-MM-dd"),
                    Status = p.Status ? "Active" : "Deactive"
                })
                .ToList();

            return Json(new
            {
                draw,
                recordsFiltered = recordsTotal,
                recordsTotal,
                data
            });
        }


        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var patient = await _context.Patients
                .Include(p => p.ApplicationUser)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (patient == null) return NotFound();

            var viewModel = new PatientViewModel
            {
                Id = patient.Id, 
                FullName = patient.ApplicationUser?.FullName ?? "",
                Gender = patient.ApplicationUser?.Gender ?? "",
                JoinDate = patient.JoinDate,
                Status = patient.Status
            };


            // Pass a ViewData flag to indicate admin edit mode (only Status editable)
            ViewData["IsAdminEdit"] = true;

            return PartialView("_Create", viewModel);
        }



        [HttpPost]
        public async Task<IActionResult> Edit(PatientViewModel model)
        {
            if (ModelState.IsValid)
            {
                var patient = await _context.Patients
                    .Include(p => p.ApplicationUser)
                    .FirstOrDefaultAsync(p => p.ApplicationUser.FullName == model.FullName);

                if (patient == null) return NotFound();

                // Only update Status for admin edit scenario
                patient.Status = model.Status;

                _context.Patients.Update(patient);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }

            // Return partial view with admin edit flag again
            ViewData["IsAdminEdit"] = true;
            return PartialView("_Create", model);
        }

    }
}
