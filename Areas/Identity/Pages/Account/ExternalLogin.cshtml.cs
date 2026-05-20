// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using ForestChurches.Components.Email;
using ForestChurches.Components.Users;
using Microsoft.Extensions.Primitives;

namespace ForestChurches.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ExternalLoginModel : PageModel
    {
        private const string LoginPage = "./Login";
        private const string LockoutPage = "./Lockout";

        private readonly SignInManager<ChurchAccount> _signInManager;
        private readonly UserManager<ChurchAccount> _userManager;
        private readonly IUserStore<ChurchAccount> _userStore;
        private readonly IUserEmailStore<ChurchAccount> _emailStore;
        private readonly ILogger<ExternalLoginModel> _logger;
        private readonly MailRepository _mailRepository;

        public ExternalLoginModel(
            SignInManager<ChurchAccount> signInManager,
            UserManager<ChurchAccount> userManager,
            IUserStore<ChurchAccount> userStore,
            ILogger<ExternalLoginModel> logger, MailRepository mailRepository)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _logger = logger;
            _mailRepository = mailRepository;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        protected internal InputModel Input { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        protected internal string ProviderDisplayName { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        protected internal System.Uri ReturnUrl { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [TempData]
        protected internal string ErrorMessage { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        protected internal class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [EmailAddress]
            protected internal string Email { get; set; }
        }

        public IActionResult OnGet()
        {
            return RedirectToPage(LoginPage);
        }

        public IActionResult OnPost(string provider, System.Uri returnUrl = null)
        {
            // Request a redirect to the external login provider.

            string redirectUrlPath = Url.Page("./ExternalLogin", pageHandler: "Callback", values: new { returnUrl });
            Uri baseUri = new Uri(Request.Scheme + "://" + Request.Host);
            Uri redirectUrl = new Uri(baseUri, redirectUrlPath);
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl.ToString());

            return new ChallengeResult(provider, properties);
        }

        public async Task<IActionResult> OnGetCallbackAsync(System.Uri returnUrl = null, string remoteError = null)
        {

            returnUrl = returnUrl ?? new Uri(Url.Content("~/"), UriKind.RelativeOrAbsolute);
            if (remoteError != null)
            {
                ErrorMessage = $"Error from external provider: {remoteError}";
                return RedirectToPage(LoginPage, new { ReturnUrl = returnUrl });
            }
            var info = await _signInManager.GetExternalLoginInfoAsync()
                .ConfigureAwait(true);

            if (info == null)
            {
                ErrorMessage = "Error loading external login information.";
                return RedirectToPage(LoginPage, new { ReturnUrl = returnUrl });
            }

            // Sign in the user with this external login provider if the user already has a login.
            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true)
                .ConfigureAwait(true);

            if (result.Succeeded)
            {
                _logger.LogInformation("{Name} logged in with {LoginProvider} provider.", info.Principal.Identity.Name, info.LoginProvider);
                return LocalRedirect(returnUrl.ToString());
            }
            if (result.IsLockedOut)
            {
                return RedirectToPage(LockoutPage);
            }
            else
            {
                // If the user does not have an account, then ask the user to create an account.
                ReturnUrl = new Uri(returnUrl.ToString());
                ProviderDisplayName = info.ProviderDisplayName;
                if (info.Principal.HasClaim(c => c.Type == ClaimTypes.Email))
                {
                    Input = new InputModel
                    {
                        Email = info.Principal.FindFirstValue(ClaimTypes.Email)
                    };
                }
                return Page();
            }
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostConfirmationAsync(System.Uri returnUrl = null)
        {
            returnUrl = returnUrl ?? new Uri(Url.Content("~/"), UriKind.RelativeOrAbsolute);
            // Get the information about the user from the external login provider
            var info = await _signInManager.GetExternalLoginInfoAsync().ConfigureAwait(true);

            if (info == null)
            {
                ErrorMessage = "Error loading external login information during confirmation.";
                return RedirectToPage(LoginPage, new { ReturnUrl = returnUrl });
            }

            if (ModelState.IsValid)
            {
                var user = CreateUser();

                await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None)
                    .ConfigureAwait(true);

                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None)
                    .ConfigureAwait(true);

                var result = await _userManager.CreateAsync(user).ConfigureAwait(true);
                if (result.Succeeded)
                {
                    result = await _userManager.AddLoginAsync(user, info).ConfigureAwait(true);
                    if (result.Succeeded)
                    {
                        _logger.LogInformation("User created an account using {Name} provider.", info.LoginProvider);

                        var userId = await _userManager.GetUserIdAsync(user)
                            .ConfigureAwait(true);

                        var code = await _userManager.GenerateEmailConfirmationTokenAsync(user)
                            .ConfigureAwait(true);

                        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                        var callbackUrl = Url.Page(
                            "/Account/ConfirmEmail",
                            pageHandler: null,
                            values: new { area = "Identity", id = userId, code = code },
                            protocol: Request.Scheme);

                        var userData = new Dictionary<string, string>()
                        { { "{confirm_email_link}", HtmlEncoder.Default.Encode(callbackUrl) } };

                        await _mailRepository.StartEmailAsync(Input.Email, userData, "Confirm your email", "./templates/confirm_email.html")
                            .ConfigureAwait(true);

                        // If account confirmation is required, we need to show the link if we don't have a real email sender
                        if (_userManager.Options.SignIn.RequireConfirmedAccount)
                        {
                            return RedirectToPage("./RegisterConfirmation", new { Email = Input.Email });
                        }

                        await _signInManager.SignInAsync(user, isPersistent: false, info.LoginProvider)
                            .ConfigureAwait(true);

                        return LocalRedirect(returnUrl.ToString());
                    }
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            ProviderDisplayName = info.ProviderDisplayName;
            ReturnUrl = new Uri(returnUrl.ToString());
            return Page();
        }

        private ChurchAccount CreateUser()
        {
            try
            {
                return Activator.CreateInstance<ChurchAccount>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(ChurchAccount)}'. " +
                    $"Ensure that '{nameof(ChurchAccount)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                    $"override the external login page in /Areas/Identity/Pages/Account/ExternalLogin.cshtml");
            }
        }

        private IUserEmailStore<ChurchAccount> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<ChurchAccount>)_userStore;
        }
    }
}