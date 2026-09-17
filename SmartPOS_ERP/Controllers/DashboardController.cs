using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPOS_ERP.Security;
using SmartPOS_ERP.Services;

namespace SmartPOS_ERP.Controllers
{
    [Authorize(Policy = AppPermissions.Dashboard)]
    public class DashboardController : Controller
    {
        private readonly DashboardService _dashboard;

        public DashboardController(DashboardService dashboard)
        {
            _dashboard = dashboard;
        }

        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var model = await _dashboard.BuildAsync(DateTime.Now, cancellationToken);
            return View(model);
        }
    }
}
