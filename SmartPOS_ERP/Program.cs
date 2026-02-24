using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Data;
using SmartPOS_ERP.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. إعداد قاعدة البيانات
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// 2. إعداد الهوية (Identity) والكنترولرز
builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddControllersWithViews();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(12); // تنتهي الجلسة بعد 12 ساعات
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// 4. إعدادات بيئة العمل (Development vs Production)
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // تحميل الصور والتنسيقات أولاً

app.UseRouting();

// تفعيل الجلسات قبل الحماية وقبل التوجيه
app.UseSession();

// تفعيل حارس البوابة لفحص تسجيل الدخول
app.UseMiddleware<LoginCheckMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

//  تحديد المسار الافتراضي (يبدأ بصفحة اللوجن)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.MapRazorPages();

//  كود الـ  لإنشاء مستخدم المدير   
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (!context.Users.Any())
    {
        context.Users.Add(new User
        {
            Username = "admin",
            // تأكد من استخدام مكتبة BCrypt لتشفير كلمة المرور الافتراضية
            Password = BCrypt.Net.BCrypt.HashPassword("123"),
            Role = "Admin"
        });
        context.SaveChanges();
    }
}

app.Run();