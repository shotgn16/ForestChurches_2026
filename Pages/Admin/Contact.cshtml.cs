using ForestChurches.Components.Email;
using ForestChurches.Components.Users;
using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ForestChurches.Components.Security;
using Microsoft.AspNetCore.Authorization;

namespace ForestChurches.Pages.Admin
{
    [Authorize(Roles = "SuperAdmin")]
    public class ContactModel : PageModel
    {
        private readonly ForestChurchesContext _context;
        private readonly ILogger<ContactModel> _logger;
        private readonly UserManager<ChurchAccount> _userManager;
        private readonly iMailSender _mailSender;

        private readonly Encoder _Encoder;

        [BindProperty]
        public UserContact Input { get; set; }
        
        [TempData]
        public string StatusMessage { get; set; }
        public List<SelectListItem> UserList { get; set; }

        public ContactModel(ForestChurchesContext context, ILogger<ContactModel> logger, iMailSender iMailSender, UserManager<ChurchAccount> userManager, Encoder encoder)
        {
            _context = context;
            _logger = logger;
            _mailSender = iMailSender;
            _userManager = userManager;
            _Encoder = encoder;

            var users = _context.Users.ToList();
            UserList = users.Select(user => new SelectListItem
            {
                Text = user.UserName,
                Value = user.UserName
            })
            .ToList();

            // Add "All Users" option
            UserList.Insert(0, new SelectListItem
            {
                Text = "All Users",
                Value = "All"
            });
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            try
            {
                ModelState.Remove(nameof(Input.Cid));
                ModelState.Remove(nameof(Input.Subject));

                if (currentUser == null)
                {
                    return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
                }

                if (string.IsNullOrWhiteSpace(Input.Message))
                {
                    ModelState.AddModelError(string.Empty, "Message cannot be empty.");
                    return Page();
                }

                var decodedEmail = _Encoder.Unprotect<string>(Input.ClientEmail, "fc.data.admin.contact");
                var decodedHtmlEmail = _Encoder.HtmlDecode(decodedEmail);

                if (decodedHtmlEmail == "All")
                {
                    // Send message to all users
                    var users = await _context.Users.ToListAsync();
                    foreach (var user in users)
                    {
                        _mailSender.SendEmailAdminContactRequest(user.UserName, user.Email, Input.Message, Input.Name);
                    }
                    StatusMessage = "Your message has been sent to all users successfully!";
                }
                else
                {
                    // Send message to a specific user
                    ChurchAccount recipient = await _context.Users.FirstOrDefaultAsync(u => u.UserName == decodedHtmlEmail);
                    if (recipient == null)
                    {
                        ModelState.AddModelError(string.Empty, "Recipient not found.");
                        return Page();
                    }

                    await _mailSender.SendEmailAdminContactRequest(recipient.UserName, recipient.Email, Input.Message, Input.Name);
                    StatusMessage = "Your message has been sent successfully!";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending Admin Email...");
                ModelState.AddModelError(string.Empty, "An error occurred while sending the message.");
                return Page();
            }

            return Page();
        }
    }
}
 