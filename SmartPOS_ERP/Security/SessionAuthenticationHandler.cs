using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace SmartPOS_ERP.Security
{
    public sealed class SessionAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Session";

        public SessionAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var userName = Context.Session.GetString("UserName");
            if (string.IsNullOrEmpty(userName))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var role = Context.Session.GetString("UserRole") ?? "Cashier";
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, userName),
                new(ClaimTypes.Role, role)
            };

            var userId = Context.Session.GetString("UserId");
            if (!string.IsNullOrEmpty(userId))
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
            }

            var permissions = Context.Session.GetString("Permissions");
            if (!string.IsNullOrEmpty(permissions))
            {
                foreach (var permission in permissions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    claims.Add(new Claim(AppPermissions.ClaimType, permission));
                }
            }

            var identity = new ClaimsIdentity(claims, Scheme.Name);

            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            if (Request.IsJsonRequest())
            {
                Response.StatusCode = StatusCodes.Status401Unauthorized;
                Response.ContentType = "application/json; charset=utf-8";
                await Response.WriteAsync("{\"message\":\"يجب تسجيل الدخول\"}");
                return;
            }

            Response.Redirect("/Account/Login");
        }

        protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            if (Request.IsJsonRequest())
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;
                Response.ContentType = "application/json; charset=utf-8";
                await Response.WriteAsync("{\"message\":\"غير مصرح لك بتنفيذ هذه العملية\"}");
                return;
            }

            Response.Redirect("/Products/Index");
        }
    }
}
