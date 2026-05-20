using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using ForestChurches.Components.Email;
using ForestChurches.Components.Users;
using ForestChurches.Components.Roles;
using ForestChurches.Components.Token;
using Microsoft.AspNetCore.DataProtection;
using NuGet.Common;
using ForestChurches.Data;
using Microsoft.EntityFrameworkCore;

namespace ForestChurches.Areas.Identity.Pages.Account
{
    public class ConfirmEmailModel : PageModel
    {
        private readonly UserManager<ChurchAccount> _userManager;
        private readonly ForestChurchesContext _context;
        private readonly ForestChurches.Components.Security.Encoder _Encoder;
        private readonly iMailSender _mailSender;
        private ILogger<ConfirmEmailModel> _logger;

        public ConfirmEmailModel(
            UserManager<ChurchAccount> userManager, 
            iMailSender mailSender, 
            ForestChurches.Components.Security.Encoder Encoder, 
            ILogger<ConfirmEmailModel> logger,  
            ForestChurchesContext context)
        {
            _Encoder = Encoder;
            _userManager = userManager;
            _mailSender = mailSender;
            _logger = logger;
            _context = context; 
        }

        [TempData]
        public string StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(string userId, string code)
        {
            try
            {
                code = _Encoder.Unprotect<string>(code, "fc.data.url.email");
                userId = _Encoder.Unprotect<string>(userId, "fc.data.url.email");

                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(code))
                {
                    return RedirectToPage("/Index");
                }

                var user = await _userManager.FindByIdAsync(userId).ConfigureAwait(true);
                if (user == null)
                {
                    return NotFound($"Unable to load user with ID '{userId}'.");
                }

                // Decode the code
                code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
                var result = await _userManager.ConfirmEmailAsync(user, code).ConfigureAwait(true);
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, Roles.AuthorizedChurch.ToString());

                    var dbUser = await _context.Users.Where(a => a.Id == user.Id).FirstOrDefaultAsync();
                        dbUser.Role = Roles.AuthorizedChurch.ToString();
                    _context.Update(dbUser);

                    await _context.SaveChangesAsync();
                }
            }

            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
            }

            return Page();
        }
    }
}
