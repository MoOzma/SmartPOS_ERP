using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Data;
using SmartPOS_ERP.Middleware;
using SmartPOS_ERP.Models;
using SmartPOS_ERP.Security;

var builder = WebApplication.CreateBuilder(args);

if (!builder.Environment.IsDevelopment())
{
    builder.WebHost.UseUrls("http://127.0.0.1:5202");
}

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDatabaseDeveloperPageExceptionFilter();
}

builder.Services.AddAuthentication(SessionAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(
        SessionAuthenticationHandler.SchemeName, _ => { });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    foreach (var permission in AppPermissions.All)
    {
        var name = permission;
        options.AddPolicy(name, policy => policy.RequireAssertion(ctx =>
            AppPermissions.Has(ctx.User, name)));
    }
});

builder.Services.AddScoped<SmartPOS_ERP.Filters.StoreSettingsResultFilter>();
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.AddService<SmartPOS_ERP.Filters.StoreSettingsResultFilter>();
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<SmartPOS_ERP.Services.StockLedgerService>();
builder.Services.AddScoped<SmartPOS_ERP.Services.DashboardService>();
builder.Services.AddScoped<SmartPOS_ERP.Services.ShiftCashService>();
builder.Services.AddScoped<SmartPOS_ERP.Services.SalesProfitReportService>();
builder.Services.AddScoped<SmartPOS_ERP.Services.ExpenseReportService>();
builder.Services.AddScoped<SmartPOS_ERP.Services.SupplierDirectoryService>();
builder.Services.AddScoped<SmartPOS_ERP.Services.PurchaseInvoiceReportService>();
builder.Services.AddScoped<SmartPOS_ERP.Services.StoreSettingsService>();
builder.Services.AddScoped<SmartPOS_ERP.Services.CreditInvoiceService>();
builder.Services.AddScoped<SmartPOS_ERP.Services.DatabaseBackupService>();
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = SmartPOS_ERP.Services.DatabaseBackupService.MaxImportBytes;
});

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(12);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    app.UseHttpsRedirection();
}
else
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseMiddleware<LoginCheckMiddleware>();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.Migrate();
    BootstrapAdmin.Ensure(context);
    OwnerAccount.Ensure(context);
}

app.Run();
