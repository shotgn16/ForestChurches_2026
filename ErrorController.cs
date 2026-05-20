using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ForestChurches
{
    public class ErrorController : Controller, IExceptionFilter
    {
        public void OnException(ExceptionContext filterContext)
        {
            int statusCode = filterContext.HttpContext.Response.StatusCode;

            if (statusCode >= 400 && statusCode < 500)
            {
                filterContext.Result = RedirectToPage("/error4xx", new { area = "errors" });
            }

            else if (statusCode >= 500 && statusCode < 600)
            {
                filterContext.Result = RedirectToPage("/error5xx", new { area = "errors" });
            }
        }
    }
}
