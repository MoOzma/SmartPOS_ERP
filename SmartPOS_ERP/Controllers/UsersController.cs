using Microsoft.AspNetCore.Mvc;
using SmartPOS_ERP.Models;
using SmartPOS_ERP.Data;

namespace SmartPOS_ERP.Controllers
{
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // عرض قائمة المستخدمين
        public IActionResult Index()
        {
            // فكر برمجياً: منع الدخول لغير المدير
            if (HttpContext.Session.GetString("UserRole") != "Admin")
                return RedirectToAction("Login", "Account");

            var users = _context.Users.ToList();
            return View(users);
        }

        // صفحة إضافة مستخدم جديد
        public IActionResult Create() => View();

        [HttpPost]
        public async Task<IActionResult> Create(User user)
        {
            if (ModelState.IsValid)
            {
                // التفكير البرمجي: تحويل كلمة السر من "نص واضح" إلى "تشفير غير قابل للقراءة"
                user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);

                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(user);
        }

        // دالة الحذف
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null && user.Username != "admin") // منع حذف المدير الأساسي
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}