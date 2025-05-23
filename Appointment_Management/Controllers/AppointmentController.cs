﻿using Appointment_Management.Data;
using Appointment_Management.Models;
using Appointment_Management.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Linq.Dynamic.Core;
using System.Linq.Dynamic.Core.Exceptions;

namespace Appointment_Management.Controllers
{
    [Authorize(Roles = "Patient")]
    public class AppointmentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AppointmentController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var patient = await _context.Patients
                .Include(p => p.ApplicationUser)
                .FirstOrDefaultAsync(p => p.ApplicationUserId == user.Id);

            ViewBag.PatientName = patient?.ApplicationUser?.FullName ?? "Unknown";
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GetAll()
        {
            var user = await _userManager.GetUserAsync(User);
            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.ApplicationUserId == user.Id);
            if (patient == null)
            {
                return Json(new { data = Enumerable.Empty<object>(), draw = 0, recordsTotal = 0, recordsFiltered = 0 });
            }

            var draw = Request.Form["draw"].FirstOrDefault();
            var start = Request.Form["start"].FirstOrDefault();
            var length = Request.Form["length"].FirstOrDefault();
            var sortColumnIndex = Convert.ToInt32(Request.Form["order[0][column]"]);
            var sortColumn = Request.Form[$"columns[{sortColumnIndex}][data]"];
            var sortDir = Request.Form["order[0][dir]"];
            var searchValue = Request.Form["search[value]"].FirstOrDefault();
            var status = Request.Form["status"].FirstOrDefault();

            int pageSize = length != null ? Convert.ToInt32(length) : 10;
            int skip = start != null ? Convert.ToInt32(start) : 0;

            var dataQuery = _context.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.ApplicationUser)
                .Include(a => a.Doctor).ThenInclude(d => d.ApplicationUser)
                .Where(a => a.PatientId == patient.Id)
                .Select(a => new
                {
                    a.Id,
                    PatientName = a.Patient.ApplicationUser.FullName,
                    DoctorName = a.Doctor.ApplicationUser.FullName,
                    a.AppointmentDate,
                    a.Description,
                    a.Status
                });

            if (!string.IsNullOrEmpty(status))
                dataQuery = dataQuery.Where(a => a.Status == status);

            if (!string.IsNullOrEmpty(searchValue))
            {
                string lowerSearchValue = searchValue.ToLower();
                dataQuery = dataQuery.Where(a =>
                    a.PatientName.ToLower().Contains(lowerSearchValue) ||
                    a.DoctorName.ToLower().Contains(lowerSearchValue) ||
                    a.Description.ToLower().Contains(lowerSearchValue) ||
                    a.Status.ToLower().Contains(lowerSearchValue));
            }


            int recordsTotal = await dataQuery.CountAsync();

            if (!string.IsNullOrEmpty(sortColumn) && !string.IsNullOrEmpty(sortDir))
            {
                try
                {
                    dataQuery = dataQuery.OrderBy($"{sortColumn} {sortDir}");
                }
                catch
                {
                    dataQuery = dataQuery.OrderBy("AppointmentDate desc");
                }
            }

            var data = await dataQuery
                .Skip(skip)
                .Take(pageSize)
                .Select(a => new
                {
                    a.Id,
                    a.PatientName,
                    a.DoctorName,
                    AppointmentDate = a.AppointmentDate.ToString("yyyy-MM-dd"),
                    a.Description,
                    a.Status
                })
                .ToListAsync();

            return Json(new
            {
                draw,
                recordsFiltered = recordsTotal,
                recordsTotal,
                data
            });
        }

        [HttpGet]
        public IActionResult Create()
        {
            var viewModel = new AppointmentViewModel
            {
                AppointmentDate = DateTime.Now
            };
            PopulateDoctorDropdown(viewModel.AppointmentDate);
            return PartialView("_Create", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Create(AppointmentViewModel vm)
        {
            var user = await _userManager.GetUserAsync(User);
            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.ApplicationUserId == user.Id);
            if (patient == null)
                return Json(new { success = false, message = "Patient profile not found." });

            if (ModelState.IsValid)
            {
                if (vm.AppointmentDate.DayOfWeek == DayOfWeek.Sunday)
                {
                    ModelState.AddModelError("AppointmentDate", "Appointments cannot be booked on Sundays.");
                }
                if (vm.AppointmentDate < DateTime.Now)
                {
                    ModelState.AddModelError("AppointmentDate", "Appointment date cannot be in the past.");
                }

                var appointmentHour = vm.AppointmentDate.Hour;
                if (appointmentHour < 9 || appointmentHour >= 17)
                {
                    ModelState.AddModelError("AppointmentDate", "Appointment must be within doctor's working hours (9 AM - 5 PM).");
                }

                var fifteenDaysAgo = vm.AppointmentDate.Date.AddDays(-5);

                bool hasRecentSameDoctorAppointment = await _context.Appointments.AnyAsync(a =>
                    a.PatientId == patient.Id &&
                    a.DoctorId == vm.DoctorId &&
                    a.AppointmentDate.Date >= fifteenDaysAgo &&
                    a.AppointmentDate.Date < vm.AppointmentDate.Date);

                if (hasRecentSameDoctorAppointment)
                {
                    ModelState.AddModelError("DoctorId", "You cannot book with the same doctor again within 5 days.");
                }

                bool hasOtherDoctorSameDate = await _context.Appointments.AnyAsync(a =>
                    a.PatientId == patient.Id &&
                    a.AppointmentDate.Date == vm.AppointmentDate.Date &&
                    a.DoctorId != vm.DoctorId);

                if (hasOtherDoctorSameDate)
                {
                    ModelState.AddModelError("AppointmentDate", "You already have an appointment with another doctor on this date.");
                }

                int maxAppointmentsPerDay = 10;
                int doctorAppointmentsCount = await _context.Appointments.CountAsync(a =>
                    a.DoctorId == vm.DoctorId &&
                    a.AppointmentDate.Date == vm.AppointmentDate.Date);

                if (doctorAppointmentsCount >= maxAppointmentsPerDay)
                {
                    ModelState.AddModelError("DoctorId", "The doctor is fully booked for this date.");
                }

                bool hasOverlappingAppointment = await _context.Appointments.AnyAsync(a =>
                    a.PatientId == patient.Id &&
                    a.DoctorId == vm.DoctorId &&
                    a.AppointmentDate == vm.AppointmentDate);

                if (hasOverlappingAppointment)
                {
                    ModelState.AddModelError("", "You already have an appointment with this doctor at the selected time.");
                }

                if (!ModelState.IsValid)
                {
                    PopulateDoctorDropdown(vm.AppointmentDate);
                    return PartialView("_Create", vm);
                }

                var appointment = new Appointment
                {
                    PatientId = patient.Id,
                    DoctorId = vm.DoctorId,
                    AppointmentDate = vm.AppointmentDate,
                    Description = vm.Description,
                    Status = "Pending"
                };

                _context.Appointments.Add(appointment);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }

            PopulateDoctorDropdown(vm.AppointmentDate);
            return PartialView("_Create", vm);
        }




        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.ApplicationUserId == user.Id);
            if (appointment.PatientId != patient.Id) return Unauthorized();

            var vm = new AppointmentViewModel
            {
                Id = appointment.Id,
                DoctorId = appointment.DoctorId,
                AppointmentDate = appointment.AppointmentDate,
                Description = appointment.Description,
                Status = appointment.Status
            };

            PopulateDoctorDropdown(vm.AppointmentDate, appointment.DoctorId);
            return PartialView("_Create", vm);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(AppointmentViewModel vm)
        {
            var user = await _userManager.GetUserAsync(User);
            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.ApplicationUserId == user.Id);
            if (patient == null) return Unauthorized();

            if (ModelState.IsValid)
            {
                bool isDuplicate = await _context.Appointments.AnyAsync(a =>
                    a.Id != vm.Id &&
                    a.DoctorId == vm.DoctorId &&
                    a.PatientId == patient.Id &&
                    a.AppointmentDate.Date == vm.AppointmentDate.Date);

                if (isDuplicate)
                {
                    ModelState.AddModelError("AppointmentDate", "This appointment already exists for the selected doctor on the same date.");
                }
                else
                {
                    var appointment = await _context.Appointments.FindAsync(vm.Id);
                    if (appointment == null) return NotFound();
                    if (appointment.PatientId != patient.Id) return Unauthorized();

                    appointment.DoctorId = vm.DoctorId;
                    appointment.AppointmentDate = vm.AppointmentDate;
                    appointment.Description = vm.Description;
                    appointment.Status = vm.Status;

                    _context.Appointments.Update(appointment);
                    await _context.SaveChangesAsync();
                    return Json(new { success = true });
                }
            }

            PopulateDoctorDropdown();
            return PartialView("_Create", vm);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.ApplicationUserId == user.Id);

            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null || appointment.PatientId != patient.Id)
                return Json(new { success = false });

            _context.Appointments.Remove(appointment);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        private void PopulateDoctorDropdown(DateTime? selectedDate = null, int? selectedDoctorId = null)
        {
            var bookedDoctorIds = selectedDate.HasValue
                ? _context.Appointments
                    .Where(a => a.AppointmentDate.Date == selectedDate.Value.Date)
                    .Select(a => a.DoctorId)
                    .ToList()
                : new List<int>();

            ViewBag.Doctors = _context.Doctors
                .Where(d => d.Status && (!selectedDate.HasValue || !bookedDoctorIds.Contains(d.Id) || d.Id == selectedDoctorId))
                .Include(d => d.ApplicationUser)
                .Select(d => new SelectListItem
                {
                    Value = d.Id.ToString(),
                    Text = d.ApplicationUser.FullName + " - " + d.SpecialistIn
                })
                .ToList();
        }

    }
}
