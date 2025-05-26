using Appointment_Management.Data;
using Appointment_Management.Helper;
using Appointment_Management.Models;
using Appointment_Management.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;

namespace Appointment_Management.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailSender _emailSender;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IEmailSender emailSender, ApplicationDbContext context, IConfiguration configuration)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _context = context;
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult Register() => View(new RegisterViewModel());

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Email", "An account with this email already exists.");
                    return View(model);
                }

                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FullName = model.FullName,
                    Gender = model.Gender,
                    CreatedAt = DateTime.Now
                };

                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "Patient");

                    var patient = new Patient
                    {
                        ApplicationUserId = user.Id,
                        JoinDate = model.JoinDate,
                        Status = true
                    };

                    _context.Patients.Add(patient);
                    await _context.SaveChangesAsync();

                    var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    var confirmationLink = Url.Action("ConfirmEmail", "Account", new
                    {
                        userId = user.Id,
                        token
                    }, protocol: HttpContext.Request.Scheme);

                    await _emailSender.SendEmailAsync(user.Email, "Confirm your email",
                        $"Please confirm your account by <a href='{confirmationLink}'>clicking here</a>.");

                    return View("RegistrationConfirmation");
                }

                var passwordErrors = result.Errors
                    .Where(e => e.Code.Contains("Password"))
                    .Select(e => e.Description)
                    .ToList();

                foreach (var key in ModelState.Keys.Where(k => k.Contains("Password")).ToList())
                {
                    ModelState[key].Errors.Clear();
                }

                foreach (var err in passwordErrors)
                {
                    ModelState.AddModelError(string.Empty, err);
                }

                foreach (var error in result.Errors.Where(e => !e.Code.Contains("Password")))
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View(model);
        }




        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            if (userId == null || token == null)
                return RedirectToAction("Index", "Home");

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (result.Succeeded)
            {
                TempData["Success"] = "Your email has been confirmed. You can now log in.";
                return RedirectToAction("Login", "Account");
            }

            return View("Error");
        }

        private async Task<string> GenerateJwtToken(ApplicationUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);

            var claims = new List<Claim>
{
    new Claim(JwtRegisteredClaimNames.Sub, user.Email),
    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
    new Claim(ClaimTypes.NameIdentifier, user.Id),
    new Claim(ClaimTypes.Email, user.Email), 
    new Claim(ClaimTypes.Name, user.UserName)
};

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var jwtSettings = _configuration.GetSection("JwtSettings");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }


        [HttpGet]
        public IActionResult Login() => View(new LoginViewModel());

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError("", "Invalid login attempt.");
                return View(model);
            }

            if (!user.EmailConfirmed)
            {
                ModelState.AddModelError("", "You need to confirm your email before logging in.");
                return View(model);
            }

            // Check user is inactive
            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.ApplicationUserId == user.Id);
            if (patient != null && !patient.Status)
            {
                TempData["Error"] = "You are not eligible to login. Please contact the administrator.";
                return View(model);
            }

            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.ApplicationUserId == user.Id);
            if (doctor != null && !doctor.Status)
            {
                TempData["Error"] = "You are not eligible to login. Please contact the administrator.";
                return View(model);
            }

            var isPasswordValid = await _userManager.CheckPasswordAsync(user, model.Password);
            if (!isPasswordValid)
            {
                ModelState.AddModelError("", "Invalid login attempt.");
                return View(model);
            }

            var token = await GenerateJwtToken(user);

            Response.Cookies.Append("jwt_token", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                Expires = DateTime.UtcNow.AddHours(1),
                SameSite = SameSiteMode.Strict
            });

            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains("Admin"))
                return RedirectToAction("Index", "Patient");

            if (roles.Contains("Patient"))
                return RedirectToAction("Index", "Appointment");

            if (roles.Contains("Doctor"))
                return RedirectToAction("Index", "DoctorAppointment");

            return RedirectToAction("Index", "Home");

        }



        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return View();
            }

            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                ModelState.AddModelError("", "No user found with this email.");
                return View();
            }

            if (!user.EmailConfirmed)
            {
                ModelState.AddModelError("", "Email is not confirmed. Please confirm your email before resetting password.");
                return View();
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetLink = Url.Action("ResetPassword", "Account", new
            {
                token,
                email
            }, protocol: HttpContext.Request.Scheme);

            await _emailSender.SendEmailAsync(email, "Reset Password",
                $"Reset your password by <a href='{resetLink}'>clicking here</a>.");

            return View("ForgotPasswordConfirmation");
        }



        [HttpGet]
        public IActionResult ResetPassword(string token, string email)
        {
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(email))
                return RedirectToAction("Index", "Home");

            var model = new ResetPasswordViewModel
            {
                Email = email,
                Token = token
            };

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return RedirectToAction("ResetPasswordConfirmation");

            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
            if (result.Succeeded)
            {
                TempData["Success"] = "Your password has been reset successfully. Please log in.";
                return RedirectToAction("Login");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View(model);
        }


        [HttpGet]
        public async Task<IActionResult> Profile()
        {
             var jwtUser = JwtHelper.GetJwtUser(HttpContext);
            var user = await _userManager.FindByIdAsync(jwtUser.UserId);
            if (user == null) return NotFound();

            var model = new UpdateProfileViewModel
            {
                FullName = user.FullName,
                Gender = user.Gender,
                Email = user.Email
            };
            return View(model);
        }


        [HttpPost]
        public async Task<IActionResult> Profile(UpdateProfileViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var jwtUser = JwtHelper.GetJwtUser(HttpContext);
            var user = await _userManager.FindByIdAsync(jwtUser.UserId);
            if (user == null) return NotFound();

            user.FullName = model.FullName;
            user.Gender = model.Gender;
            user.Email = model.Email;
            user.UserName = model.Email;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["Success"] = "Profile updated successfully!";
                return RedirectToAction("Profile");
            }

            TempData["Error"] = "Failed to update profile. Please check the errors.";
            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }




        [HttpGet]
        public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

        [HttpPost]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var jwtUser = JwtHelper.GetJwtUser(HttpContext);
            var user = await _userManager.FindByIdAsync(jwtUser.UserId);
            if (user == null)
                return RedirectToAction("Login", "Account");

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);

            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user);
                TempData["Success"] = "Password changed successfully!";
                return RedirectToAction("ChangePassword");
            }

            TempData["Error"] = "Failed to change password. Please check the errors.";
            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }



        [HttpGet]
        public IActionResult ResendEmailConfirmation() => View(new ForgotPasswordViewModel());

        [HttpPost]
        public async Task<IActionResult> ResendEmailConfirmation(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError("", "No account found with this email.");
                return View(model);
            }

            if (await _userManager.IsEmailConfirmedAsync(user))
            {
                ModelState.AddModelError("", "Your email is already confirmed.");
                return View(model);
            }

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var confirmationLink = Url.Action("ConfirmEmail", "Account", new
            {
                userId = user.Id,
                token
            }, protocol: HttpContext.Request.Scheme);

            await _emailSender.SendEmailAsync(model.Email, "Confirm your email",
                $"Please confirm your account by <a href='{confirmationLink}'>clicking here</a>.");

            TempData["Success"] = "Confirmation email has been resent. Please check your inbox.";
            return RedirectToAction("Login");
        }

        [HttpGet]
        public async Task<IActionResult> ChangeEmail()
        {
            var jwtUser = JwtHelper.GetJwtUser(HttpContext);
            if (!jwtUser.IsAuthenticated)
                return RedirectToAction("Login");

            var user = await _userManager.FindByIdAsync(jwtUser.UserId);
            if (user == null)
                return RedirectToAction("Login");

            var model = new ChangeEmailViewModel
            {
                CurrentEmail = user.Email
            };

            return View(model);
        }



        [HttpPost]
        public async Task<IActionResult> ChangeEmail(ChangeEmailViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var jwtUser = JwtHelper.GetJwtUser(HttpContext);
            if (!jwtUser.IsAuthenticated)
                return RedirectToAction("Login");

            var user = await _userManager.FindByIdAsync(jwtUser.UserId);
            if (user == null)
                return RedirectToAction("Login");

            if (model.NewEmail == user.Email)
            {
                ModelState.AddModelError("", "The new email must be different from the current one.");
                return View(model);
            }

            var token = await _userManager.GenerateChangeEmailTokenAsync(user, model.NewEmail);

            var callbackUrl = Url.Action("ConfirmEmailChange", "Account", new
            {
                userId = user.Id,
                email = model.NewEmail,
                token
            }, protocol: Request.Scheme);

            await _emailSender.SendEmailAsync(model.NewEmail, "Confirm your new email",
                $"Please confirm your new email by <a href='{callbackUrl}'>clicking here</a>.");

            TempData["Success"] = "Confirmation link sent to your new email. Please check your inbox.";
            return RedirectToAction("Profile");
        }


        [HttpGet]
        public async Task<IActionResult> ConfirmEmailChange(string userId, string email, string token)
        {
            if (userId == null || email == null || token == null)
                return RedirectToAction("Index", "Home");

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            var result = await _userManager.ChangeEmailAsync(user, email, token);

            if (result.Succeeded)
            {
                user.UserName = email;
                await _userManager.UpdateAsync(user);

                TempData["Success"] = "Your email has been changed successfully.";
                return RedirectToAction("Login", "Account");
            }

            TempData["Error"] = "Email change failed.";
            return RedirectToAction("Login", "Account");
        }



        [HttpPost]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("jwt_token");
            return RedirectToAction("Login");
        }

        public IActionResult AccessDenied()
        {
            return View();
        }
        

    }
}
