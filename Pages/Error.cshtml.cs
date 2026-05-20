using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics;

namespace ForestChurches.Pages
{
    [AutoValidateAntiforgeryToken]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public class ErrorModel : PageModel
    {
        public string? RequestId { get; set; }
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
        public string? ExceptionMessage { get; set; }

        private readonly ILogger<ErrorModel> _logger;

        public ErrorModel(ILogger<ErrorModel> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
            var exceptionHandlerPathFeature =
                HttpContext.Features.Get<IExceptionHandlerPathFeature>();

            if (exceptionHandlerPathFeature is null)
            {
                @ViewData["error_title"] = "Are You Lost?";
                ExceptionMessage = "We're all good here :)";
                RequestId = string.Empty;
            }

            else if (exceptionHandlerPathFeature != null)
            {
                @ViewData["error_title"] = "Error";
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
                ExceptionMessage = exceptionHandlerPathFeature.Error.Message;
            }
        }

        public void OnPost()
        {
            var exceptionHandlerPathFeature =
                HttpContext.Features.Get<IExceptionHandlerPathFeature>();

            if (exceptionHandlerPathFeature is null)
            {
                @ViewData["error_title"] = "Are You Lost?";
                ExceptionMessage = "We're all good here :)";
                RequestId = string.Empty;
            }

            else if (exceptionHandlerPathFeature != null)
            {
                @ViewData["error_title"] = "Error";
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
                ExceptionMessage = exceptionHandlerPathFeature.Error.Message;
            }
        }
    }
}