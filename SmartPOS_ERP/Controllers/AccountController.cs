using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Data;
using SmartPOS_ERP.Models;

namespace SmartPOS_ERP.Controllers
{
    public class AccountController : Controller
    {
        // دالة عرض صفحة الدخول (GET)

        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }
        public IActionResult Login() => View();

        // دالة معالجة بيانات الدخول (POST)
        [HttpPost]
        public IActionResult Login(string username, string password)
        {
            // 1. نبحث عن المستخدم بالاسم فقط أولاً
            var user = _context.Users.FirstOrDefault(u => u.Username == username);

            // 2. إذا وجدناه، نتحقق من أن كلمة السر المدخلة تطابق التشفير الموجود في القاعدة
            if (user != null && BCrypt.Net.BCrypt.Verify(password, user.Password))
            {
                HttpContext.Session.SetString("UserName", user.Username);
                HttpContext.Session.SetString("UserRole", user.Role);
                return RedirectToAction("Index", "Products");
            }

            ViewBag.Error = "بيانات الدخول غير صحيحة";
            return View();
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear(); // تدمير "بطاقة التعريف" عند تسجيل الخروج
            return RedirectToAction("Login");
     
        }

        // GET: Account/Profile
        public async Task<IActionResult> Profile()
        {
            // الحصول على اسم المستخدم الحالي من السيشين
            var currentUserName = HttpContext.Session.GetString("UserName");
            if (string.IsNullOrEmpty(currentUserName)) return RedirectToAction("Login");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == currentUserName);
            if (user == null) return NotFound();

            return View(user);
        }

        // POST: Account/Profile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(string newPassword)
        {
            var currentUserName = HttpContext.Session.GetString("UserName");
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == currentUserName);

            if (user != null && !string.IsNullOrEmpty(newPassword))
            {
                // تشفير كلمة السر الجديدة قبل الحفظ
                user.Password = BCrypt.Net.BCrypt.HashPassword(newPassword);
                _context.Update(user);
                await _context.SaveChangesAsync();
                ViewBag.Message = "تم تحديث كلمة المرور بنجاح";
            }
            return View(user);
        }



    }



}