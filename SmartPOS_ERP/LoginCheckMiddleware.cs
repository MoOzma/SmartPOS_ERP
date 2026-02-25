using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;



    public class LoginCheckMiddleware
    {
        private readonly RequestDelegate _next;

        public LoginCheckMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            string path = context.Request.Path.Value.ToLower();

            // 1. السماح بمرور أي طلب يحتوي على كلمة account 
            // لضمان وصول الـ POST والـ GET الخاص باللوجن
            if (path.Contains("account") || path.Contains("/lib/") || path.Contains("/css/") || path.Contains("/js/"))
            {
                await _next(context);
                return;
            }

            // 2. التحقق من السيشين
            var userSession = context.Session.GetString("UserName");

            if (string.IsNullOrEmpty(userSession))
            {
                // تأكد من أن التوجيه يذهب للمسار الصحيح تماماً
                context.Response.Redirect("/Account/Login");
                return;
            }

            await _next(context);
        }
    }
