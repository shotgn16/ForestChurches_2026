// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;
using ForestChurches.Components.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

namespace ForestChurches.Areas.Identity.Pages.Account
{
    public class LoginModel : PageModel
    {
        ForestChurches.Components.Storage.IBlobUrlService BlobUrlService;
        private readonly SignInManager<ChurchAccount> _signInManager;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(SignInManager<ChurchAccount> signInManager, ILogger<LoginModel> logger, Components.Storage.IBlobUrlService blobUrlService)
        {
            _signInManager = signInManager;
            _logger = logger;
            BlobUrlService = blobUrlService;
        }

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
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string ReturnUrl { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [TempData]
        public string ErrorMessage { get; set; }

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
            public string Email { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");

            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            ReturnUrl = returnUrl;
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                // Users sign in with their email; resolve it to the account first.
                // PasswordSignInAsync(user, ...) checks the password, counts failures towards
                // lockout (5 attempts, 15 minutes; see PolicyConfiguration) and sends users with
                // two-factor authentication on to the 2FA step instead of signing them straight in.
                var user = await _signInManager.UserManager.FindByEmailAsync(Input.Email);

                Microsoft.AspNetCore.Identity.SignInResult result = user != null
                    ? await _signInManager.PasswordSignInAsync(user, Input.Password, Input.RememberMe, lockoutOnFailure: true)
                    : await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: true);

                if (result.Succeeded)
                {
                    return LocalRedirect(returnUrl);
                }

                if (result.RequiresTwoFactor)
                {
                    return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                }

                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User account locked out.");
                    return RedirectToPage("./Lockout");
                }

                if (result.IsNotAllowed)
                {
                    // If we resolved a user, we can give a clearer message when email is unconfirmed.
                    if (user != null)
                    {
                        var emailConfirmed = await _signInManager.UserManager.IsEmailConfirmedAsync(user);
                        if (!emailConfirmed)
                        {
                            _logger.LogWarning("Sign-in not allowed for user {Email}: email not confirmed.", Input?.Email);
                            ModelState.AddModelError(string.Empty, "Sign-in is not allowed for this account because the email address has not been confirmed. Please check your email for a confirmation link.");
                            return Page();
                        }
                    }

                    _logger.LogWarning("Sign-in not allowed for user {Email}. Possible unconfirmed email or disabled account.", Input?.Email);
                    ModelState.AddModelError(string.Empty, "Sign-in is not allowed for this account. Please check your email for confirmation or contact support.");
                    return Page();
                }

                _logger.LogInformation("PasswordSignInAsync failed for {Email}. Result: {Result}", Input?.Email, result.ToString());
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return Page();
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }
    }
}
