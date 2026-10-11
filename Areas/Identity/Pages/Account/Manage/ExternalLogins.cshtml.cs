// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using ForestChurches.Components.Users;
using ForestChurches.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Primitives;
using Microsoft.EntityFrameworkCore;

namespace ForestChurches.Areas.Identity.Pages.Account.Manage
{
    public class ExternalLoginsModel : PageModel
    {
        private readonly UserManager<ChurchAccount> _userManager;
        private readonly SignInManager<ChurchAccount> _signInManager;
        private readonly ForestChurches.Components.Security.Encoder _Encoder;
        private readonly IUserStore<ChurchAccount> _userStore;
        private readonly ForestChurchesContext _context;

        public ExternalLoginsModel(
            UserManager<ChurchAccount> userManager,
            SignInManager<ChurchAccount> signInManager,
            IUserStore<ChurchAccount> userStore, 
            ForestChurches.Components.Security.Encoder Encoder,
            ForestChurchesContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _userStore = userStore;
            _Encoder = Encoder;
            _context = context;
        }

        [TempData]
        internal Models.ChurchInformation CurrentChurch { get; set; }
        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        internal ChurchAccount Currentuser { get; set; }
        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public IList<UserLoginInfo> CurrentLogins { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public IList<AuthenticationScheme> OtherLogins { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public bool ShowRemoveButton { get; set; }

        // Indicates whether the current user has active ChurchSuite OAuth credentials
        public bool HasChurchSuiteCredentials { get; set; }

        // Human readable status about ChurchSuite link for UI
        public string ChurchSuiteStatusMessage { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [TempData]
        public string StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            // Get current Church & User
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            // Assign logged-in user to 'currentUser' variable && load the header text
            Currentuser = user;
            CurrentChurch = _context.ChurchInformation.Where(ci => ci.ChurchAccountId == user.Id).FirstOrDefault();

            // Default actions
            CurrentLogins = await _userManager.GetLoginsAsync(user);
            OtherLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync())
                .Where(auth => CurrentLogins.All(ul => auth.Name != ul.LoginProvider))
                .ToList();

            string passwordHash = null;
            if (_userStore is IUserPasswordStore<ChurchAccount> userPasswordStore)
            {
                passwordHash = await userPasswordStore.GetPasswordHashAsync(user, HttpContext.RequestAborted);
            }

            ShowRemoveButton = passwordHash != null || CurrentLogins.Count > 1;

            // Check existing ChurchSuite credentials for this user
            var cred = await _context.ChurchSuiteCredentials.FirstOrDefaultAsync(c => c.UserId == user.Id && c.IsActive);
            HasChurchSuiteCredentials = cred != null && !string.IsNullOrEmpty(cred.AccessToken);
            ChurchSuiteStatusMessage = HasChurchSuiteCredentials ? "Connected to ChurchSuite" : "Not connected to ChurchSuite";

            // Provide current user data for view rendering
            Currentuser = user;
            CurrentChurch = _context.ChurchInformation.Where(ci => ci.ChurchAccountId == user.Id).FirstOrDefault();

            return Page();
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostRemoveLoginAsync(string loginProvider, string providerKey)
        {
            var user = await _userManager.GetUserAsync(User);
            if (!ModelState.IsValid)
            {
                StatusMessage = "Error: An error occurred while processing your request.";
                return Page();
            }

            else if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            else if (ModelState.IsValid && user != null)
            {
                var result = await _userManager.RemoveLoginAsync(user, loginProvider, providerKey);
                if (!result.Succeeded)
                {
                    StatusMessage = "The external login was not removed.";
                    return RedirectToPage();
                }

                await _signInManager.RefreshSignInAsync(user);
                StatusMessage = "The external login was removed.";
            }
           
            return RedirectToPage();
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostLinkLoginAsync(string provider)
        {
            string redirectUrl = string.Empty;
            AuthenticationProperties properties = new AuthenticationProperties();

            if (!ModelState.IsValid)
            {
                StatusMessage = "Error: An error occurred while processing your request.";
                return Page();
            }

            else if (ModelState.IsValid)
            {
                // Clear the existing external cookie to ensure a clean login process
                await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

                // Request a redirect to the external login provider to link a login for the current user
                redirectUrl = Url.Page("./ExternalLogins", pageHandler: "LinkLoginCallback");
                properties = _signInManager.ConfigureExternalAuthenticationProperties(_Encoder.Unprotect<string>(provider, "fc.data.login.external"), redirectUrl, _userManager.GetUserId(User));
            }

            return new ChallengeResult(_Encoder.Unprotect<string>(provider, "fc.data.login.external"), properties);
        }

        public async Task<IActionResult> OnGetLinkLoginCallbackAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var userId = await _userManager.GetUserIdAsync(user);
            var info = await _signInManager.GetExternalLoginInfoAsync(userId);
            if (info == null)
            {
                throw new InvalidOperationException($"Unexpected error occurred loading external login info.");
            }

            var result = await _userManager.AddLoginAsync(user, info);
            if (!result.Succeeded)
            {
                StatusMessage = "The external login was not added. External logins can only be associated with one account.";
                return RedirectToPage();
            }

            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            StatusMessage = "The external login was added.";
            return RedirectToPage();
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostSetClientIdAsync(string clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                ModelState.AddModelError("clientId", "Client ID is required.");
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            // Save clientId to user record
            user.ClientId = clientId.Trim();
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                StatusMessage = "Error: Unable to save Client ID.";
                return RedirectToPage();
            }

            // Initiate per-user PKCE authorize redirect
            var authorizeUrl = $"/churchsuite/authorize?user_id={Uri.EscapeDataString(user.Id)}&redirect_uri={Uri.EscapeDataString(Url.Page("~/"))}";
            return Redirect(authorizeUrl);
        }
    }
}
