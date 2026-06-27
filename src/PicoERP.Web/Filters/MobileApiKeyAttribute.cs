using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PicoERP.Web.Filters;

/// <summary>
/// Validates the X-Mobile-Api-Key header on every mobile API request.
/// The key is set in appsettings.json under Mobile:ApiKey.
/// Only the ERP owner who knows this key can activate / use the mobile app.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class MobileApiKeyAttribute : Attribute, IResourceFilter
{
    private const string HeaderName = "X-Mobile-Api-Key";

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var expected = config["Mobile:ApiKey"];

        if (string.IsNullOrWhiteSpace(expected))
        {
            // Key not configured — block all access
            context.Result = new ObjectResult(new { error = "Mobile API not configured." })
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable
            };
            return;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var provided)
            || provided != expected)
        {
            context.Result = new UnauthorizedObjectResult(new { error = "Invalid or missing mobile API key." });
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context) { }
}
