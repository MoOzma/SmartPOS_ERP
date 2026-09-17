using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Data;
using SmartPOS_ERP.Models;
using SmartPOS_ERP.Security;

namespace SmartPOS_ERP.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AccountController> _logger;

        public AccountController(ApplicationDbContext context, ILogger<AccountController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [AllowAnonymous]
        public IActionResult Login()
        {
            LoadLoginUsers();
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public IActionResult Login(string username, string password)
        {
            var user = _context.Users.FirstOrDefault(u =>
                u.Username.ToLower() == (username ?? string.Empty).Trim().ToLower());
            var failure = StaffAuth.LoginFailure(user, password);
            if (failure == null && user != null)
            {
                StaffAuth.ApplySession(HttpContext.Session, user);

                if (!PasswordRules.TryValidate(password, out _))
                {
                    HttpContext.Session.SetString("MustChangePassword", "true");
                    return RedirectToAction(nameof(Profile));
                }

                _logger.LogInformation("User {Username} signed in", user.Username);
                if (string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase) || user.CanViewDashboard)
                    return RedirectToAction("Index", "Dashboard");
                return RedirectToAction("Index", "Products");
            }

            _logger.LogWarning("Failed login attempt for username {Username}", username);
            ViewBag.Error = failure ?? StaffAuth.WrongCredentials;
            ViewBag.Username = username;
            LoadLoginUsers();
            return View();
        }

        private void LoadLoginUsers()
        {
            ViewBag.LoginUsers = OwnerAccount.Visible(_context.Users)
                .AsNoTracking()
                .Where(u => u.IsActive)
                .OrderBy(u => u.DisplayName)
                .ThenBy(u => u.Username)
                .Select(u => new LoginUserOption
                {
                    Username = u.Username,
                    DisplayName = u.DisplayName
                })
                .ToList();
        }

        public IActionResult Logout()
        {
            var userName = User.Identity?.Name;
            HttpContext.Session.Clear();
            _logger.LogInformation("User {Username} signed out", userName);
            return RedirectToAction("Login");
        }

        public async Task<IActionResult> Profile()
        {
            var currentUserName = User.Identity?.Name;
            if (string.IsNullOrEmpty(currentUserName)) return RedirectToAction("Login");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == currentUserName);
            if (user == null) return NotFound();

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(string newPassword, string confirmPassword)
        {
            var currentUserName = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == currentUserName);

            if (user != null && !string.IsNullOrEmpty(newPassword))
            {
                if (newPassword != confirmPassword)
                {
                    ViewBag.Error = "الرقم السري وتأكيده غير متطابقين.";
                    return View(user);
                }

                if (!PasswordRules.TryValidate(newPassword, out var error))
                {
                    ViewBag.Error = error;
                    return View(user);
                }

                user.Password = BCrypt.Net.BCrypt.HashPassword(newPassword);
                _context.Update(user);
                await _context.SaveChangesAsync();
                HttpContext.Session.Remove("MustChangePassword");
                _logger.LogInformation("Password updated for user {Username}", currentUserName);
                ViewBag.Message = "تم تحديث الرقم السري بنجاح";
            }
            return View(user);
        }
    }
}
