using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using ForestChurches.Components.Email;

namespace ForestChurches.Pages
{
    public class ValidateTokenModel : PageModel
    {
        private readonly iMailSender _mailSender;
        private readonly ILogger<ValidateTokenModel> _logger;

        public ValidateTokenModel(iMailSender mailSender, ILogger<ValidateTokenModel> logger)
        {
            _mailSender = mailSender;
            _logger = logger;
        }

        [BindProperty(SupportsGet = true)]
        public string Token { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool TokenValidated { get; set; } = false;

        public bool IsValidating { get; private set; } = true;
        public bool IsTokenValid { get; private set; } = false;

        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrEmpty(Token))
            {
                _logger.LogWarning("Token is null or empty.");
                IsValidating = false;
                return Page();
            }

            if (TokenValidated)
            {
                _logger.LogInformation("Token has already been validated.");
                return Page();
            }

            var result = _mailSender.ValidateToken(Token);

            if (result == "Token has expired." || result == "Token is invalid.")
            {
                _logger.LogWarning(result);
                IsValidating = false;
                return Page();
            }

            IsTokenValid = true;
            _logger.LogInformation("Token is valid. Redirecting to: {RedirectUrl}", result);
            return Redirect(result);
        }
    }
}
