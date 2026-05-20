// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using ForestChurches.Components.Email;
using ForestChurches.Components.Users;
using ForestChurches.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

namespace ForestChurches.Areas.Identity.Pages.Account.Manage
{
    public class EmailModel : PageModel
    {
        private readonly UserManager<ChurchAccount> _userManager;
        private readonly SignInManager<ChurchAccount> _signInManager;
        private readonly ForestChurches.Components.Security.Encoder _Encoder;
        private readonly IEmailSender _emailSender;
        private readonly iMailSender _mailSender;
        private readonly ForestChurchesContext _context;

        public EmailModel(
            UserManager<ChurchAccount> userManager,
            SignInManager<ChurchAccount> signInManager,
            iMailSender mailSender, ForestChurchesContext context, 
            Components.Security.Encoder Encoder)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _mailSender = mailSender;
            _context = context;
            _Encoder = Encoder;
        }

        internal ChurchAccount Currentuser { get; set; }
        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [TempData]
        internal Models.ChurchInformation CurrentChurch { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public bool IsEmailConfirmed { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [TempData]
        public string StatusMessage { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [EmailAddress]
            [Display(Name = "New email")]
            public string NewEmail { get; set; }
        }

        private async Task LoadAsync(ChurchAccount user)
        {
            var email = await _userManager.GetEmailAsync(user);
            Email = email;

            Input = new InputModel
            {
                NewEmail = email,
            };

            IsEmailConfirmed = await _userManager.IsEmailConfirmedAsync(user);
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            // Assign logged-in user to 'currentUser' variable && Load the header text
            Currentuser = user;
            CurrentChurch = _context.ChurchInformation.Where(ci => ci.ChurchAccountId == user.Id).FirstOrDefault();

            await LoadAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostChangeEmailAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_Encoder.Protect(_userManager.GetUserId(User), "fc.data.manage.email")}'.");
            }

            if (!ModelState.IsValid)
            {
                await LoadAsync(user);
                return Page();
            }

            var email = await _userManager.GetEmailAsync(user);
            if (Input.NewEmail != email)
            {
                var userId = await _userManager.GetUserIdAsync(user);
                var code = await _userManager.GenerateChangeEmailTokenAsync(user, Input.NewEmail);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                var callbackUrl = Url.Page(
                    "/Account/ConfirmEmailChange",
                    pageHandler: null,
                    values: new { area = "Identity", userId = _Encoder.Protect(userId, "fc.data.url.change"), email = _Encoder.Protect(Input.NewEmail, "fc.data.url.change"), code = _Encoder.Protect(code, "fc.data.url.change") },
                    protocol: Request.Scheme);

                // Send Email
                _mailSender.SendNewEmailConfirmation(_Encoder.Protect(_userManager.GetUserName(User), "fc.data.mail.confirm"), callbackUrl);

                StatusMessage = "Confirmation link to change email sent. Please check your email.";
                return RedirectToPage();
            }

            StatusMessage = "Your email is unchanged.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSendVerificationEmailAsync()
        {
            if (!ModelState.IsValid)
            {
                StatusMessage = "Error: Invalid model state.";
                return Page();
            }

            else if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
                }

                if (!ModelState.IsValid)
                {
                    await LoadAsync(user);
                    return Page();
                }

                var userId = await _userManager.GetUserIdAsync(user);
                var email = await _userManager.GetEmailAsync(user);
                var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                var callbackUrl = Url.Page(
                    "/Account/ConfirmEmail",
                    pageHandler: null,
                    values: new { area = "Identity", userId = _Encoder.Protect(userId, "fc.data.url.email"), code = _Encoder.Protect(code, "fc.data.url.email") },
                    protocol: Request.Scheme);


                // Send Email
                _mailSender.SendEmailConfirmAccount(_Encoder.Protect(_userManager.GetUserName(User), "fc.data.mail.confirm"), callbackUrl);

                StatusMessage = "Verification email sent. Please check your email.";
            }

            return RedirectToPage();
        }
    }
}
