using Serilog;
using System.Globalization;

namespace ForestChurches.Localization
{
    /// <summary>
    /// Diagnostic middleware that logs the current UI culture for each request.
    /// </summary>
    internal class LocalizationDiagnosticsMiddleware
    {
        private readonly RequestDelegate _next;

        public LocalizationDiagnosticsMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var culture = CultureInfo.CurrentUICulture.Name;
            var cultureCookie = context.Request.Query["culture"].FirstOrDefault();

            Log.Information("[LOCALIZATION_DIAGNOSTIC] CurrentCulture={Culture} QueryString culture={QueryCulture} Path={Path}",
                culture, cultureCookie ?? "none", context.Request.Path);

            await _next(context);
        }
    }
}
