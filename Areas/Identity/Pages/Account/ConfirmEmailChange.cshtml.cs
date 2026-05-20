// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using ForestChurches.Components.Users;

namespace ForestChurches.Areas.Identity.Pages.Account
{
    public class ConfirmEmailChangeModel : PageModel
    {
        private readonly UserManager<ChurchAccount> _userManager;
        private readonly SignInManager<ChurchAccount> _signInManager;
        private readonly ForestChurches.Components.Security.Encoder _Encoder;

        public ConfirmEmailChangeModel(
            UserManager<ChurchAccount> userManager, 
            SignInManager<ChurchAccount> signInManager, 
            Components.Security.Encoder Encoder)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _Encoder = Encoder;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [TempData]
        public string StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(string userId, string email, string code)
        {
            if (userId == null || email == null || code == null)
            {
                return RedirectToPage("/Index");
            }

            userId = _Encoder.Unprotect<string>(userId, "fc.data.url.change");
            email = _Encoder.Unprotect<string>(email, "fc.data.url.change");
            code = _Encoder.Unprotect<string>(code, "fc.data.url.change");

            var user = await _userManager.FindByIdAsync(userId).ConfigureAwait(true);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{userId}'.");
            }

            code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            var result = await _userManager.ChangeEmailAsync(user, email, code).ConfigureAwait(true);
            if (!result.Succeeded)
            {
                StatusMessage = "Error changing email.";
                return Page();
            }

            // In our UI email and user name are one and the same, so when we update the email
            // we need to update the user name.
            var setUserNameResult = await _userManager.SetUserNameAsync(user, email).ConfigureAwait(true);
            if (!setUserNameResult.Succeeded)
            {
                StatusMessage = "Error changing user name.";
                return Page();
            }

            await _signInManager.RefreshSignInAsync(user).ConfigureAwait(true);
            StatusMessage = "Thank you for confirming your email change.";
            return Page();
        }
    }
}
