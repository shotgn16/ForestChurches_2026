using ForestChurches.Components.Email;
using ForestChurches.Components.UserRegistration;
using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ForestChurches.Pages.Admin
{
    [Authorize(Policy = "Admin.Read")]
    public class InviteModel : PageModel
    {
        private readonly ForestChurchesContext _context;
        private readonly ILogger<InviteModel> _logger;
        private readonly iRegistrationGenerate _register;
        private readonly iMailSender _mailSender;
        private readonly ForestChurches.Components.Security.Encoder _Encoder;

        internal List<WhitelistedUsers> invites;
        string _url;
        
        [TempData]
        public string StatusMessage { get; set; }

        public InviteModel(ForestChurchesContext context, ILogger<InviteModel> logger, iRegistrationGenerate register, iMailSender mailSender, Components.Security.Encoder Encoder)
        {
            _register = register;
            _context = context;
            _logger = logger;
            _mailSender = mailSender;
            _Encoder = Encoder;

            invites = _context.WhitelistedUsers.ToList();
        }

        public void OnGet()
        {
            invites = _context.WhitelistedUsers.ToList();
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostAddWhitelistedUser(string email)
        {
            if (!ModelState.IsValid)
            {
                StatusMessage = $"Error: An error occurred while whitelisting {(email)}.";
            }

            else if (ModelState.IsValid)
            {
                if (!string.IsNullOrEmpty(email))
                {
                    _context.WhitelistedUsers.Add(new WhitelistedUsers
                    {
                        Email = email,
                        ID = Guid.NewGuid().ToString(),
                        DateOfRegistration = DateTime.MinValue,
                        Status = "Pending",
                        ExpiryDate = DateTime.UtcNow.AddHours(24)
                    });

                    // Send an email to the user telling them they are Invited
                    _url = _register.GenerateUrl(HttpContext.Request, email);

                    _mailSender.SendEmailInviteUser(email, _url);
                    _context.SaveChanges();
                    StatusMessage = $"Sent: {email} has been invited. The link lasts 24 hours.";
                }
            }

            return RedirectToPage();
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostRemoveWhitelistedUser(string email)
        {
            if (!ModelState.IsValid)
            {
                StatusMessage = $"Error: An error occurred while removing {(_Encoder.Unprotect<string>(email, "fc.data.admin.invite"))} from the whitelist.";
                return Page();
            }

            else if (ModelState.IsValid)
            {
                if (!string.IsNullOrEmpty(_Encoder.Unprotect<string>(email, "fc.data.admin.invite")))
                {
                    var _selectedUser = _context.WhitelistedUsers
                        .Where(a => a.Email == _Encoder.Unprotect<string>(email, "fc.data.admin.invite"))
                        .FirstOrDefault();

                    if (_selectedUser != null)
                    {
                        _context.WhitelistedUsers.Remove(_selectedUser);
                        _context.SaveChanges();
                        StatusMessage = $"Saved: {_selectedUser.Email} was removed from the invite list.";
                    }
                }
            }

            return RedirectToPage();
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostResendInvite(string email)
        {
            if (!ModelState.IsValid)
            {
                StatusMessage = $"Error: An error occurred while resending the invite to {(_Encoder.Unprotect<string>(email, "fc.data.admin.invite"))}.";
                return Page();
            }

            else if (ModelState.IsValid)
            {
                if (!string.IsNullOrEmpty(_Encoder.Unprotect<string>(email, "fc.data.admin.invite")))
                {
                    _url = _register.GenerateUrl(HttpContext.Request, _Encoder.Unprotect<string>(email, "fc.data.admin.invite"));
                    _mailSender.SendEmailInviteUser(_Encoder.Unprotect<string>(email, "fc.data.admin.invite"), _url);
                    StatusMessage = $"Sent: the invitation was emailed again (valid for another 24 hours) to {_Encoder.Unprotect<string>(email, "fc.data.admin.invite")}.";
                }
            }

            return RedirectToPage();
        }
    }
}
