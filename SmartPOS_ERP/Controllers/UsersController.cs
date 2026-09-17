using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Data;
using SmartPOS_ERP.Models;
using SmartPOS_ERP.Security;
using System.Security.Claims;

namespace SmartPOS_ERP.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UsersController> _logger;

        public UsersController(ApplicationDbContext context, ILogger<UsersController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var users = await OwnerAccount.Visible(_context.Users).OrderBy(u => u.DisplayName).ToListAsync();
            return View(users);
        }

        public IActionResult Create() => View(new StaffFormViewModel { Role = "Cashier", IsActive = true });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StaffFormViewModel form)
        {
            form.IsEdit = false;
            if (!await ValidateStaffFormAsync(form, 0))
            {
                return View(form);
            }

            var user = new User { Username = form.Username.Trim() };
            ApplyForm(user, form);
            user.Password = BCrypt.Net.BCrypt.HashPassword(form.Password);
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            _logger.LogInformation("User {Username} created with role {Role}", user.Username, user.Role);
            TempData["Message"] = "تم إضافة الموظف.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null || OwnerAccount.Matches(user))
            {
                return NotFound();
            }

            return View(ToForm(user));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, StaffFormViewModel form)
        {
            if (id != form.Id)
            {
                return NotFound();
            }

            form.IsEdit = true;
            var user = await _context.Users.FindAsync(id);
            if (user == null || OwnerAccount.Matches(user))
            {
                return NotFound();
            }

            form.Username = user.Username;
            if (!await ValidateStaffFormAsync(form, user.Id))
            {
                return View(form);
            }

            var currentUserId = CurrentUserId();
            var activeAdmins = await ActiveAdminCountAsync();
            if (user.IsActive && !form.IsActive && !StaffRules.CanDeactivate(user, currentUserId, activeAdmins))
            {
                ModelState.AddModelError(string.Empty, DeactivateBlockMessage(user, currentUserId));
                return View(form);
            }

            if (user.Role == "Admin" && form.Role == "Cashier" && !StaffRules.CanChangeRoleToCashier(user, activeAdmins))
            {
                ModelState.AddModelError(nameof(form.Role), "لا يمكن تحويل آخر مدير مفعّل إلى كاشير.");
                return View(form);
            }

            ApplyForm(user, form);
            if (!string.IsNullOrWhiteSpace(form.Password))
            {
                user.Password = BCrypt.Net.BCrypt.HashPassword(form.Password);
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("User {Username} updated", user.Username);
            TempData["Message"] = "تم حفظ بيانات الموظف.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null || OwnerAccount.Matches(user))
            {
                return NotFound();
            }

            var currentUserId = CurrentUserId();
            if (user.IsActive)
            {
                var activeAdmins = await ActiveAdminCountAsync();
                if (!StaffRules.CanDeactivate(user, currentUserId, activeAdmins))
                {
                    TempData["Error"] = DeactivateBlockMessage(user, currentUserId);
                    return RedirectToAction(nameof(Index));
                }

                user.IsActive = false;
                TempData["Message"] = "تم إيقاف الحساب.";
            }
            else
            {
                user.IsActive = true;
                TempData["Message"] = "تم تفعيل الحساب.";
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private async Task<bool> ValidateStaffFormAsync(StaffFormViewModel form, int userId)
        {
            if (form.Role is not ("Admin" or "Cashier"))
            {
                ModelState.AddModelError(nameof(form.Role), "صلاحية غير صحيحة");
            }

            if (!form.IsEdit)
            {
                if (string.IsNullOrWhiteSpace(form.Username))
                {
                    ModelState.AddModelError(nameof(form.Username), "اسم المستخدم مطلوب");
                }
                else if (OwnerAccount.Matches(form.Username)
                    || await _context.Users.AnyAsync(u => u.Username == form.Username.Trim()))
                {
                    ModelState.AddModelError(nameof(form.Username), "اسم المستخدم مستخدم بالفعل.");
                }

                if (!PasswordRules.TryValidate(form.Password, out var createPasswordError))
                {
                    ModelState.AddModelError(nameof(form.Password), createPasswordError);
                }
            }
            else if (!string.IsNullOrWhiteSpace(form.Password) && !PasswordRules.TryValidate(form.Password, out var editPasswordError))
            {
                ModelState.AddModelError(nameof(form.Password), editPasswordError);
            }

            return ModelState.IsValid;
        }

        private static void ApplyForm(User user, StaffFormViewModel form)
        {
            user.DisplayName = form.DisplayName.Trim();
            user.Phone = string.IsNullOrWhiteSpace(form.Phone) ? null : form.Phone.Trim();
            user.Role = form.Role;
            user.IsActive = form.IsActive;
            var cashier = form.Role == "Cashier";
            user.CanProcessReturn = cashier && form.CanProcessReturn;
            user.CanManageProducts = cashier && form.CanManageProducts;
            user.CanViewAllOrders = cashier && form.CanViewAllOrders;
            user.CanManageExpenses = cashier && form.CanManageExpenses;
            user.CanManagePurchases = cashier && form.CanManagePurchases;
            user.CanViewDashboard = cashier && form.CanViewDashboard;
        }

        private static StaffFormViewModel ToForm(User user) => new()
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Phone = user.Phone,
            Role = user.Role,
            IsActive = user.IsActive,
            CanProcessReturn = user.CanProcessReturn,
            CanManageProducts = user.CanManageProducts,
            CanViewAllOrders = user.CanViewAllOrders,
            CanManageExpenses = user.CanManageExpenses,
            CanManagePurchases = user.CanManagePurchases,
            CanViewDashboard = user.CanViewDashboard,
            IsEdit = true
        };

        private Task<int> ActiveAdminCountAsync()
            => OwnerAccount.Visible(_context.Users).CountAsync(u => u.Role == "Admin" && u.IsActive);

        private int CurrentUserId()
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out var id) ? id : 0;
        }

        private static string DeactivateBlockMessage(User user, int currentUserId)
            => user.Id == currentUserId
                ? "لا يمكنك إيقاف حسابك بنفسك."
                : "لا يمكن إيقاف آخر مدير مفعّل.";
    }
}
