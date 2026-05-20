using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

namespace ForestChurches.Components.Token
{
    [Route("callback")]
    public class CallbackController : Controller
    {
        private readonly CallbackTokenDataFormat _tokenDataFormat;

        public CallbackController(IDataProtectionProvider dataProtectionProvider)
        {
            // Initialize CallbackTokenDataFormat with the injected IDataProtectionProvider
            _tokenDataFormat = new CallbackTokenDataFormat(dataProtectionProvider, "User  Registration");
        }

        [HttpGet("{token}")]
        public IActionResult HandleCallback(string token)
        {
            try
            {
                // Unprotect the token
                var callbackToken = _tokenDataFormat.Unprotect(token);

                // Check if the token has expired
                if (callbackToken.ExpirationTime < DateTime.UtcNow)
                {
                    // Token has expired
                    return RedirectToAction("TokenExpired"); // Redirect to an expired token page
                }

                // Token is valid, proceed with the redirect
                return Redirect(callbackToken.RedirectUrl);
            }
            catch (Exception ex)
            {
                // Handle any exceptions (e.g., token invalid)
                return RedirectToAction("TokenInvalid"); // Redirect to an invalid token page
            }
        }

        public IActionResult TokenExpired()
        {
            // Return a view or message indicating the token has expired
            return View("TokenExpired"); // Ensure you have a view named TokenExpired
        }

        public IActionResult TokenInvalid()
        {
            // Return a view or message indicating the token is invalid
            return View("TokenInvalid"); // Ensure you have a view named TokenInvalid
        }
    }
}