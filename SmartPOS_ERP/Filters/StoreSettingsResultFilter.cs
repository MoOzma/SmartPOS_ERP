using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SmartPOS_ERP.Services;

namespace SmartPOS_ERP.Filters;

public sealed class StoreSettingsResultFilter : IAsyncResultFilter
{
    private readonly StoreSettingsService _storeSettings;

    public StoreSettingsResultFilter(StoreSettingsService storeSettings)
    {
        _storeSettings = storeSettings;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is ViewResult or PartialViewResult or ViewComponentResult
            && context.Controller is Controller controller)
        {
            controller.ViewData["StoreSettings"] = await _storeSettings.GetAsync(context.HttpContext.RequestAborted);
        }

        await next();
    }
}
