using Microsoft.EntityFrameworkCore;
using SmartPOS_ERP.Data;
using SmartPOS_ERP.Security;

namespace SmartPOS_ERP.Middleware
{
    public class LoginCheckMiddleware
    {
        private readonly RequestDelegate _next;

        public LoginCheckMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ApplicationDbContext db)
        {
            if (IsAnonymousPath(context.Request.Path))
            {
                await _next(context);
                return;
            }

            if (context.Session.GetString("MustChangePassword") == "true"
                && !IsPasswordChangeAllowed(context.Request.Path))
            {
                await RejectUntilPasswordChangedAsync(context);
                return;
            }

            var userIdValue = context.Session.GetString("UserId");
            if (int.TryParse(userIdValue, out var userId))
            {
                var active = await db.Users
                    .Where(u => u.Id == userId)
                    .Select(u => (bool?)u.IsActive)
                    .FirstOrDefaultAsync();
                if (active != true)
                {
                    context.Session.Clear();
                    if (context.Request.IsJsonRequest())
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json; charset=utf-8";
                        await context.Response.WriteAsync("{\"message\":\"هذا الحساب موقوف\"}");
                        return;
                    }

                    context.Response.Redirect("/Account/Login");
                    return;
                }
            }

            await _next(context);
        }

        private static bool IsAnonymousPath(PathString path)
        {
            var value = path.Value ?? string.Empty;
            return value.Equals("/Account/Login", StringComparison.OrdinalIgnoreCase)
                || value.Equals("/Account/Login/", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPasswordChangeAllowed(PathString path)
        {
            var value = path.Value ?? string.Empty;
            return value.Equals("/Account/Profile", StringComparison.OrdinalIgnoreCase)
                || value.Equals("/Account/Profile/", StringComparison.OrdinalIgnoreCase)
                || value.Equals("/Account/Logout", StringComparison.OrdinalIgnoreCase)
                || value.Equals("/Account/Logout/", StringComparison.OrdinalIgnoreCase);
        }

        private static async Task RejectUntilPasswordChangedAsync(HttpContext context)
        {
            if (context.Request.IsJsonRequest())
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync("{\"message\":\"يجب تعيين رقم سري من 4 أرقام أولاً\"}");
                return;
            }

            context.Response.Redirect("/Account/Profile");
        }
    }
}
