using ForestChurches.Components.Email;
using ForestChurches.Components.Users;
using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;

namespace ForestChurches.Pages
{
    public class ContactModel : PageModel
    {
        // Dependency Injection
        private readonly ForestChurches.Components.Security.Encoder _Encoder;
        private readonly iMailSender _mailSender;
        private readonly ILogger<ContactModel> _logger;
        private readonly ForestChurches.Components.Configuration.Configuration _Configuration;
        private readonly ForestChurchesContext _context;
        private readonly UserManager<ChurchAccount> _userManager;
        private readonly IDataProtector _dataProtector;

        // Data Objects
        [BindProperty]
        public UserContact Input { get; set; }
        public string Recipient { get; set; } = "info@forestchurches.co.uk";

        [TempData]
        public string StatusMessage { get; set; }

        public ContactModel(iMailSender mailSender, UserManager<ChurchAccount> userManager, ForestChurchesContext context, ILogger<ContactModel> logger, Components.Security.Encoder Encoder, IDataProtectionProvider provider, Components.Configuration.Configuration configuration)
        {
            _logger = logger;
            _context = context;
            _mailSender = mailSender;
            _Encoder = Encoder;
            _userManager = userManager;
            _dataProtector = provider.CreateProtector("contact");
            _Configuration = configuration;
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnGetAsync(string cid)
        {
            Input = new UserContact();

            if (!ModelState.IsValid)
            {
                return Page();
            }

            else if (ModelState.IsValid)
            {
                if (cid != null)
                {
                    Input.Cid = cid;
                }

                if (User.Identity.IsAuthenticated == true)
                {
                    Input.ClientEmail = User.Identity.Name;
                }
            }

            return Page();
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostAsync(string cid = null)
        {
            try
            {
                string recipientEmail = await GetRecipientEmailAsync(cid);
                if (recipientEmail == null)
                {
                    return Page();
                }

                string senderEmail = User.Identity.IsAuthenticated ? User.Identity.Name : Input.ClientEmail;
                await SendEmailAsync(recipientEmail, senderEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while sending a message");
                StatusMessage = "Error: An error occurred while sending your message";
            }

            StatusMessage = "Success! Your message was sent!";
            return RedirectToPage();
        }

        private async Task<string> GetRecipientEmailAsync(string cid)
        {
            var target = _Configuration.Client.GetSecret("Email-Target").Value.Value;
            
            if (string.IsNullOrEmpty(cid))
            {
                return target;
            }

            var unprotectedCid = _Encoder.Unprotect<string>(cid, "fc.data.contact");
            if (unprotectedCid == null)
            {
                _logger.LogWarning("Unprotected Church ID is null for Church ID: {Cid}", cid);
                StatusMessage = "Error: Invalid Church ID.";
                return target;
            }

            _logger.LogInformation("Fetching recipient email for Church ID: {Cid}", unprotectedCid);
            var recipientEmail = await _context.ChurchInformation
                .Where(a => a.ID == unprotectedCid)
                .Select(b => b.ChurchAccount.Email)
                .FirstOrDefaultAsync();

            if (recipientEmail == null)
            {
                _logger.LogWarning("No recipient email found for Church ID: {Cid}", unprotectedCid);
                StatusMessage = "Error: No recipient email found for the provided Church ID.";
                return target;
            }

            return recipientEmail;
        }

        private async Task SendEmailAsync(string recipientEmail, string senderEmail)
        {
            string userName = User.Identity.IsAuthenticated ? _userManager.GetUserName(User) : string.Empty;
                _mailSender.SendEmailContactRequest(
                    userName, Input.Name, recipientEmail, senderEmail, Input.Message, "/Index");
        }

        private void LogModelErrors()
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors);
            foreach (var error in errors)
            {
                _logger.LogError(error.ErrorMessage);
            }
        }
    }
}
