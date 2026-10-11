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
using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.Extensions.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;

namespace ForestChurches.Areas.Identity.Pages.Account
{
    /// <summary>
    /// DTO for temporarily storing external login information in session.
    /// </summary>
    public class ExternalLoginTempDto
    {
        public string Provider { get; set; }
        public string ProviderKey { get; set; }
        public string DisplayName { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public List<ClaimDto> Claims { get; set; }
    }

    public class ClaimDto
    {
        public string Type { get; set; }
        public string Value { get; set; }
    }

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
        private readonly iEmail _mailRepository;
        private readonly IChurchSuiteService _churchSuiteService;
        private readonly ForestChurchesContext _context;

        public ExternalLoginModel(
            SignInManager<ChurchAccount> signInManager,
            UserManager<ChurchAccount> userManager,
            IUserStore<ChurchAccount> userStore,
            ILogger<ExternalLoginModel> logger, 
            iEmail mailRepository,
            IChurchSuiteService churchSuiteService,
            ForestChurchesContext context)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _logger = logger;
            _mailRepository = mailRepository;
            _churchSuiteService = churchSuiteService;
            _context = context;
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
        protected internal string ProviderDisplayName { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        protected internal string ReturnUrl { get; set; }

        /// <summary>
        /// Temporary token used to reconstruct external login from server session when the external cookie
        /// is not available during the confirmation POST.
        /// </summary>
        protected internal string Token { get; set; }

        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [TempData]
        protected internal string ErrorMessage { get; set; }

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

            [Required]
            [DataType(DataType.Password)]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 12)]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
        }

        public async Task<IActionResult> OnGet(string provider = null, string returnUrl = null)
        {
            // If provider is supplied via querystring, initiate the external login flow (allow GET -> challenge)
            if (!string.IsNullOrEmpty(provider))
            {
                return await OnPost(provider, returnUrl).ConfigureAwait(true);
            }

            return RedirectToPage(LoginPage);
        }

        public async Task<IActionResult> OnPost(string provider, string returnUrl = null)
        {
            // Don't validate Input model for external login initiation
            // External login only needs provider and returnUrl, not the Input form fields
            ModelState.Clear();

            // Request a redirect to the external login provider.
            if (string.IsNullOrEmpty(provider))
            {
                return BadRequest("Provider is required");
            }

            // Special handling for ChurchSuite: if an authenticated user exists use per-user PKCE flow,
            // otherwise allow the standard external challenge so users can register via ChurchSuite.
            if (string.Equals(provider, "ChurchSuite", StringComparison.OrdinalIgnoreCase))
            {
                var currentUser = await _userManager.GetUserAsync(User).ConfigureAwait(true);
                if (currentUser != null)
                {
                    if (string.IsNullOrWhiteSpace(currentUser.ClientId))
                    {
                        // Redirect to ExternalLogins with TempData error explaining how to configure
                        TempData["ChurchSuiteAuthError"] = "You must set a ChurchSuite Client ID on the Manage -> External Logins page before connecting.";
                        return RedirectToPage("./Manage/ExternalLogins");
                    }

                    // Redirect to manual PKCE authorize endpoint using per-user client id
                    var ru = returnUrl ?? Url.Content("~/");
                    var url = $"/churchsuite/authorize?user_id={Uri.EscapeDataString(currentUser.Id)}&redirect_uri={Uri.EscapeDataString(ru)}";
                    try
                    {
                        HttpContext.Session.SetString("ExternalLoginInitiated", DateTime.UtcNow.ToString("o"));
                        _logger.LogDebug("Session marker set for ChurchSuite PKCE initiation");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to set session marker before PKCE initiation");
                    }

                    return Redirect(url);
                }

                // No authenticated user - allow regular external challenge so new users can register via ChurchSuite
                var propertiesForAnon = _signInManager.ConfigureExternalAuthenticationProperties(provider, null);
                if (returnUrl != null)
                {
                    propertiesForAnon.RedirectUri = returnUrl.ToString();
                }

                try
                {
                    HttpContext.Session.SetString("ExternalLoginInitiated", DateTime.UtcNow.ToString("o"));
                    _logger.LogDebug("Session marker set for external ChurchSuite initiation (anonymous user)");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to set session marker before external challenge for ChurchSuite");
                }

                var challengeResult = new ChallengeResult(provider, propertiesForAnon);
                _logger.LogInformation("Returning ChallengeResult for provider {Provider}", provider);
                return challengeResult;
            }

            // Configure properties for the external authentication challenge for other providers
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, null);

            // Store the return URL in the authentication properties so it can be retrieved after callback
            if (returnUrl != null)
            {
                properties.RedirectUri = returnUrl.ToString();
            }

            try
            {
                // Create a session marker so the browser will send the session cookie on the provider callback.
                HttpContext.Session.SetString("ExternalLoginInitiated", DateTime.UtcNow.ToString("o"));
                _logger.LogDebug("Session marker set for external login initiation");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set session marker before external challenge");
            }

            return new ChallengeResult(provider, properties);
        }

        public async Task<IActionResult> OnGetCallbackAsync(string returnUrl = null, string remoteError = null, string token = null)
        {

            returnUrl = string.IsNullOrEmpty(returnUrl) ? Url.Content("~/") : returnUrl;

            _logger.LogInformation("OnGetCallbackAsync invoked. Token: {Token}, ReturnUrl: {ReturnUrl}", token, returnUrl);

            if (remoteError != null)
            {
                ErrorMessage = $"Error from external provider: {remoteError}";
                return RedirectToPage(LoginPage, new { ReturnUrl = returnUrl });
            }
            Microsoft.AspNetCore.Identity.ExternalLoginInfo info = null;

            // Inspect the external identity cookie directly without consuming it so the confirmation POST can
            // still call GetExternalLoginInfoAsync().
            var authResult = await HttpContext.AuthenticateAsync(IdentityConstants.ExternalScheme).ConfigureAwait(true);
            if (authResult?.Succeeded == true && authResult.Principal != null)
            {
                _logger.LogInformation("External auth cookie found and succeeded");

                // Log authResult.Properties for debugging
                try
                {
                    if (authResult.Properties?.Items != null && authResult.Properties.Items.Count > 0)
                    {
                        var props = string.Join("; ", authResult.Properties.Items.Select(kv => $"{kv.Key}={kv.Value}"));
                        _logger.LogDebug("External auth.Properties.Items: {Properties}", props);
                    }
                    else
                    {
                        _logger.LogDebug("External auth.Properties.Items is empty or null");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to log external auth properties");
                }

                // Log principal claims for debugging (avoid logging overly large or sensitive values in production)
                try
                {
                    var claimList = authResult.Principal.Claims.Select(c => new { c.Type, c.Value }).ToList();
                    var claimsDump = string.Join("; ", claimList.Select(c => $"{c.Type}={c.Value}"));
                    _logger.LogDebug("External auth principal claims: {Claims}", claimsDump);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to log external principal claims");
                }

                var provider = authResult.Properties?.Items.ContainsKey(".AuthScheme") == true
                    ? authResult.Properties.Items[".AuthScheme"]
                    : "ChurchSuite";

                var providerKey = authResult.Principal.FindFirstValue(ClaimTypes.NameIdentifier)
                                  ?? authResult.Principal.FindFirstValue("sub");

                if (!string.IsNullOrWhiteSpace(providerKey))
                {
                    info = new Microsoft.AspNetCore.Identity.ExternalLoginInfo(authResult.Principal, provider, providerKey, provider);
                    _logger.LogInformation("Rebuilt ExternalLoginInfo from external cookie. Provider: {Provider}, Key: {Key}", provider, providerKey);
                }
                else
                {
                    _logger.LogWarning("Could not extract ProviderKey from external auth cookie");
                }
            }
            else
            {
                _logger.LogInformation("No external auth cookie found or authentication failed");
            }

            // If still null, try session token (set by middleware)
            if (info == null && !string.IsNullOrWhiteSpace(token))
            {
                _logger.LogInformation("Attempting to recover external login from session with token: {Token}", token);

                try
                {
                    var key = $"ExternalLoginTemp:{token}";
                    var json = HttpContext.Session.GetString(key);

                    _logger.LogInformation("Session key lookup for '{SessionKey}'. Found: {Found}", key, !string.IsNullOrWhiteSpace(json));

                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        _logger.LogDebug("Session JSON content: {Json}", json);

                        var dto = System.Text.Json.JsonSerializer.Deserialize<ExternalLoginTempDto>(json);
                        if (dto != null)
                        {
                            _logger.LogInformation("Successfully deserialized session DTO. Provider: {Provider}, ProviderKey: {ProviderKey}", dto.Provider, dto.ProviderKey);

                            var claims = new List<Claim>();
                            if (dto.Claims != null)
                            {
                                foreach (var c in dto.Claims)
                                {
                                    if (!string.IsNullOrWhiteSpace(c.Type))
                                    {
                                        claims.Add(new Claim(c.Type, c.Value ?? string.Empty));
                                    }
                                }
                            }
                            _logger.LogInformation("Reconstructed {ClaimCount} claims from session", claims.Count);

                            // Restore the access token as a claim if available, so it can be found during profile fetch
                            if (!string.IsNullOrWhiteSpace(dto.AccessToken))
                            {
                                claims.Add(new Claim("access_token", dto.AccessToken));
                                _logger.LogInformation("Added access_token claim to reconstructed principal. Token length: {Length}", dto.AccessToken.Length);
                            }
                            else
                            {
                                _logger.LogWarning("Session DTO has no AccessToken - profile fetch will fail silently and default church will be created");
                            }

                            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "External"));
                            info = new Microsoft.AspNetCore.Identity.ExternalLoginInfo(principal, dto.Provider ?? "ChurchSuite", dto.ProviderKey ?? string.Empty, dto.Provider ?? "ChurchSuite");
                            _logger.LogInformation("Created ExternalLoginInfo from session token {Token}", token);

                            try
                            {
                                // Preserve the external principal in the external cookie so GetExternalLoginInfoAsync
                                // will succeed during the confirmation POST.
                                var authProps = new Microsoft.AspNetCore.Authentication.AuthenticationProperties();
                                authProps.Items[".AuthScheme"] = dto.Provider ?? "ChurchSuite";
                                await HttpContext.SignInAsync(IdentityConstants.ExternalScheme, principal, authProps).ConfigureAwait(true);
                                _logger.LogInformation("Signed external principal into {Scheme} for confirmation flow", IdentityConstants.ExternalScheme);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to persist external principal to external cookie");
                            }

                            // Preserve the temporary session entry so the confirmation POST can still recover the external principal if
                            // the external cookie is not available. The session entry will be removed by the confirmation handler when consumed.
                            _logger.LogInformation("Preserving temporary session entry for token {Token} so POST confirmation can recover external principal", token);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to deserialize session DTO from JSON");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Session key '{SessionKey}' not found or empty", key);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to rebuild external login from session token {Token}", token);
                }
            }
            else if (info == null)
            {
                _logger.LogWarning("No token provided and no external auth cookie found");
            }

            if (info == null)
            {
                ErrorMessage = "Error loading external login information.";
                _logger.LogError("ExternalLoginInfo is null. Redirecting to Login page.");
                return RedirectToPage(LoginPage, new { ReturnUrl = returnUrl });
            }

            _logger.LogInformation("ExternalLoginInfo successfully obtained. Provider: {Provider}", info.LoginProvider);

            // Sign in the user with this external login provider if the user already has a login.
            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true)
                .ConfigureAwait(true);

            if (result.Succeeded)
            {
                _logger.LogInformation("{Name} logged in with {LoginProvider} provider.", info.Principal.Identity.Name, info.LoginProvider);

                // If this is ChurchSuite, attempt to sync profile data
                if (info.LoginProvider == "ChurchSuite")
                {
                    await SyncChurchSuiteProfileAsync(info.Principal).ConfigureAwait(true);
                }

                return LocalRedirect(returnUrl);
            }
            if (result.IsLockedOut)
            {
                return RedirectToPage(LockoutPage);
            }
            else
            {
                // If the user does not have an account, then ask the user to create an account.
                // Preserve the session token so the confirmation POST can restore the external principal if needed.
                Token = token;
                ReturnUrl = returnUrl;
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

        private class ExternalLoginTempDto
        {
            public string Provider { get; set; }
            public string ProviderKey { get; set; }
            public string AccessToken { get; set; }
            public string RefreshToken { get; set; }
            public List<ClaimDto> Claims { get; set; }
        }

        private class ClaimDto
        {
            public string Type { get; set; }
            public string Value { get; set; }
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostConfirmationAsync(string returnUrl = null, string token = null)
        {
            returnUrl = string.IsNullOrEmpty(returnUrl) ? Url.Content("~/") : returnUrl;
            _logger.LogInformation("[POST-CONFIRM] === START === Token={Token}, ReturnUrl={ReturnUrl}, Input={InputNull}, Input.Email={Email}", token, returnUrl, Input == null ? "NULL" : "OBJECT", Input?.Email ?? "NULL");

            // Log raw form data to debug binding issues
            try
            {
                var formData = await Request.ReadFormAsync();
                var keys = string.Join(", ", formData.Keys);
                _logger.LogInformation("[POST-CONFIRM] Form data keys: {Keys}", keys);
                if (formData.ContainsKey("Input.Email"))
                {
                    _logger.LogInformation("[POST-CONFIRM] Form['Input.Email'] = {Value}", formData["Input.Email"].ToString());
                }
                else if (formData.ContainsKey("Email"))
                {
                    _logger.LogInformation("[POST-CONFIRM] Form['Email'] = {Value}", formData["Email"].ToString());
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[POST-CONFIRM] Failed to read form data");
            }

            // Get the information about the user from the external login provider
            var info = await _signInManager.GetExternalLoginInfoAsync().ConfigureAwait(true);
            _logger.LogInformation("[POST-CONFIRM] Initial GetExternalLoginInfoAsync: {Result}", info == null ? "NULL" : $"Provider={info.LoginProvider}, ProviderKey={info.ProviderKey}");

            // If info is null, attempt to rebuild the external login from the server session using the provided token.
            if (info == null && !string.IsNullOrWhiteSpace(token))
            {
                _logger.LogInformation("[POST-CONFIRM] ExternalLoginInfo null. Attempting to recover from session token: {Token}", token);
                try
                {
                    var key = $"ExternalLoginTemp:{token}";
                    var json = HttpContext.Session.GetString(key);
                    _logger.LogInformation("[POST-CONFIRM] Session key lookup for '{SessionKey}'. Found: {Found}", key, !string.IsNullOrWhiteSpace(json));

                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        _logger.LogDebug("[POST-CONFIRM] Session JSON content: {Json}", json);
                        var dto = System.Text.Json.JsonSerializer.Deserialize<ExternalLoginTempDto>(json);
                        if (dto != null)
                        {
                            _logger.LogInformation("[POST-CONFIRM] Successfully deserialized session DTO. Provider: {Provider}, ProviderKey: {ProviderKey}", dto.Provider, dto.ProviderKey);

                            var claims = new List<Claim>();
                            if (dto.Claims != null)
                            {
                                foreach (var c in dto.Claims)
                                {
                                    if (!string.IsNullOrWhiteSpace(c.Type))
                                    {
                                        claims.Add(new Claim(c.Type, c.Value ?? string.Empty));
                                    }
                                }
                            }
                            _logger.LogInformation("[POST-CONFIRM] Reconstructed {ClaimCount} claims from session", claims.Count);

                            // Restore the access token as a claim if available, so it can be found during profile fetch
                            if (!string.IsNullOrWhiteSpace(dto.AccessToken))
                            {
                                claims.Add(new Claim("access_token", dto.AccessToken));
                                _logger.LogInformation("[POST-CONFIRM] Added access_token claim to reconstructed principal. Token length: {Length}", dto.AccessToken.Length);
                            }
                            else
                            {
                                _logger.LogWarning("[POST-CONFIRM] Session DTO has no AccessToken - profile fetch will fail silently");
                            }

                            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "External"));
                            info = new Microsoft.AspNetCore.Identity.ExternalLoginInfo(principal, dto.Provider ?? "ChurchSuite", dto.ProviderKey ?? string.Empty, dto.Provider ?? "ChurchSuite");
                            _logger.LogInformation("[POST-CONFIRM] Created ExternalLoginInfo from session token {Token}", token);

                            try
                            {
                                var authProps = new Microsoft.AspNetCore.Authentication.AuthenticationProperties();
                                authProps.Items[".AuthScheme"] = dto.Provider ?? "ChurchSuite";
                                await HttpContext.SignInAsync(IdentityConstants.ExternalScheme, principal, authProps).ConfigureAwait(true);
                                _logger.LogInformation("[POST-CONFIRM] Signed external principal into {Scheme} for confirmation flow", IdentityConstants.ExternalScheme);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "[POST-CONFIRM] Failed to persist external principal to external cookie");
                            }

                            // Preserve the temporary session entry for now; it will be removed after successful registration.
                            _logger.LogInformation("[POST-CONFIRM] Preserving temporary session entry for token {Token} until registration completes", token);
                        }
                        else
                        {
                            _logger.LogWarning("[POST-CONFIRM] Failed to deserialize session DTO from JSON");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("[POST-CONFIRM] Session key '{SessionKey}' not found or empty", key);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[POST-CONFIRM] Failed to rebuild external login from session token {Token}", token);
                }
            }

            if (info == null)
            {
                _logger.LogError("[POST-CONFIRM] ExternalLoginInfo is NULL - cannot proceed. Redirecting to login.");
                ErrorMessage = "Error loading external login information during confirmation.";
                return RedirectToPage(LoginPage, new { ReturnUrl = returnUrl });
            }

            _logger.LogInformation("[POST-CONFIRM] ExternalLoginInfo available: Provider={Provider}, ProviderKey={ProviderKey}", info.LoginProvider, info.ProviderKey);

            // Re-bind the Input values from form since model binding may have failed
            if (Input == null)
            {
                Input = new InputModel();
            }
            try
            {
                var formData = await Request.ReadFormAsync();

                if (string.IsNullOrWhiteSpace(Input.Email) && formData.ContainsKey("Input.Email"))
                {
                    Input.Email = formData["Input.Email"].ToString();
                    _logger.LogInformation("[POST-CONFIRM] Manually bound Input.Email from form: {Email}", Input.Email);
                }

                if (string.IsNullOrWhiteSpace(Input.Password) && formData.ContainsKey("Input.Password"))
                {
                    Input.Password = formData["Input.Password"].ToString();
                    _logger.LogInformation("[POST-CONFIRM] Manually bound Input.Password from form (masked)");
                }

                if (string.IsNullOrWhiteSpace(Input.ConfirmPassword) && formData.ContainsKey("Input.ConfirmPassword"))
                {
                    Input.ConfirmPassword = formData["Input.ConfirmPassword"].ToString();
                    _logger.LogInformation("[POST-CONFIRM] Manually bound Input.ConfirmPassword from form (masked)");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[POST-CONFIRM] Failed to manually bind Input model from form");
            }

            if (string.IsNullOrWhiteSpace(Input.Email))
            {
                _logger.LogError("[POST-CONFIRM] Input.Email is empty after binding attempts. Redirecting to login.");
                ErrorMessage = "Email address is required.";
                return RedirectToPage(LoginPage, new { ReturnUrl = returnUrl });
            }

            // Validate the input model after manual binding. If invalid, return the page with validation errors.
            if (!TryValidateModel(Input))
            {
                _logger.LogWarning("[POST-CONFIRM] Input model validation failed: {Errors}", string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                // Preserve provider display and token for redisplay
                ProviderDisplayName = info.ProviderDisplayName;
                ReturnUrl = returnUrl;
                Token = token;
                return Page();
            }

            _logger.LogInformation("[POST-CONFIRM] Email confirmed: {Email}", Input.Email);

            if (ModelState.IsValid)
            {
                _logger.LogInformation("[POST-CONFIRM] ModelState is VALID. Email={Email}", Input?.Email);

                // For ChurchSuite external logins, validate that the email is whitelisted
                if (info.LoginProvider == "ChurchSuite")
                {
                    _logger.LogInformation("[POST-CONFIRM] Checking ChurchSuite whitelist for email={Email}", Input?.Email);
                    var whitelistedUsers = await _context.WhitelistedUsers.Select(a => a.Email).ToListAsync();
                    _logger.LogInformation("[POST-CONFIRM] Whitelist count={Count}, Emails=[{Emails}]", whitelistedUsers.Count, string.Join(", ", whitelistedUsers));

                    if (!whitelistedUsers.Contains(Input.Email))
                    {
                        ErrorMessage = "You are not authorized to register with this account. Please contact support for more information.";
                        _logger.LogWarning("[POST-CONFIRM] Email {Email} NOT whitelisted. Redirecting to login.", Input.Email);
                        return RedirectToPage(LoginPage, new { ReturnUrl = returnUrl });
                    }
                    _logger.LogInformation("[POST-CONFIRM] Email {Email} is whitelisted. Proceeding.", Input.Email);
                }

                var user = CreateUser();

                await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None)
                    .ConfigureAwait(true);

                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None)
                    .ConfigureAwait(true);

                // Validate password against configured Identity password validators before creating user
                var passwordValidationErrors = new List<IdentityError>();
                foreach (var validator in _userManager.PasswordValidators)
                {
                    var validationResult = await validator.ValidateAsync(_userManager, user, Input.Password).ConfigureAwait(true);
                    if (!validationResult.Succeeded)
                    {
                        passwordValidationErrors.AddRange(validationResult.Errors);
                    }
                }

                if (passwordValidationErrors.Any())
                {
                    foreach (var err in passwordValidationErrors)
                    {
                        ModelState.AddModelError(string.Empty, err.Description);
                    }
                    _logger.LogWarning("[POST-CONFIRM] Password validation failed: {Errors}", string.Join("; ", passwordValidationErrors.Select(e => e.Description)));
                    ProviderDisplayName = info.ProviderDisplayName;
                    ReturnUrl = returnUrl;
                    Token = token;
                    return Page();
                }

                var result = await _userManager.CreateAsync(user, Input.Password).ConfigureAwait(true);
                _logger.LogInformation("[POST-CONFIRM] CreateAsync(with password): Succeeded={Succeeded}, ErrorCount={ErrorCount}, Errors=[{Errors}]", 
                    result.Succeeded, result.Errors.Count(), string.Join("; ", result.Errors.Select(e => $"{e.Code}:{e.Description}")));

                if (result.Succeeded)
                {
                    _logger.LogInformation("[POST-CONFIRM] User created successfully. UserId={UserId}", user.Id);

                    result = await _userManager.AddLoginAsync(user, info).ConfigureAwait(true);
                    _logger.LogInformation("[POST-CONFIRM] AddLoginAsync: Succeeded={Succeeded}, ErrorCount={ErrorCount}, Errors=[{Errors}]", 
                        result.Succeeded, result.Errors.Count(), string.Join("; ", result.Errors.Select(e => $"{e.Code}:{e.Description}")));

                    if (result.Succeeded)
                    {
                        _logger.LogInformation("[POST-CONFIRM] AddLoginAsync succeeded. User now linked to external provider.");

                        var userId = await _userManager.GetUserIdAsync(user)
                            .ConfigureAwait(true);

                        // If this is ChurchSuite, persist the credentials for later API use
                        if (info.LoginProvider == "ChurchSuite")
                        {
                            await SaveChurchSuiteCredentialsAsync(user, info, token).ConfigureAwait(true);
                        }

                        // If this is ChurchSuite, attempt to fetch ChurchSuite profile and create a ChurchInformation record
                        if (info.LoginProvider == "ChurchSuite")
                        {
                            ChurchInformation churchInfo = null;
                            try
                            {
                                // Try to retrieve access token from session DTO (from OnTicketReceived)
                                var profile = await _churchSuiteService.GetProfileFromOAuthAsync(info.Principal).ConfigureAwait(true);

                                // If that failed, try to get access token from session token data
                                if (profile == null && !string.IsNullOrWhiteSpace(token))
                                {
                                    try
                                    {
                                        var key = $"ExternalLoginTemp:{token}";
                                        var json = HttpContext.Session.GetString(key);
                                        if (!string.IsNullOrWhiteSpace(json))
                                        {
                                            var dto = System.Text.Json.JsonSerializer.Deserialize<ExternalLoginTempDto>(json);
                                            if (dto?.AccessToken != null)
                                            {
                                                _logger.LogInformation("[POST-CONFIRM] Retrieved access token from session. Length: {Length}, Starts with: {StartsWith}", 
                                                    dto.AccessToken.Length, dto.AccessToken.Length > 10 ? dto.AccessToken.Substring(0, 10) + "..." : "SHORT");
                                                _logger.LogInformation("[POST-CONFIRM] Attempting profile fetch using access token from session");
                                                profile = await _churchSuiteService.GetProfileFromOAuthAsync(dto.AccessToken).ConfigureAwait(true);
                                            }
                                            else
                                            {
                                                _logger.LogWarning("[POST-CONFIRM] Session DTO has no AccessToken property");
                                            }
                                        }
                                        else
                                        {
                                            _logger.LogWarning("[POST-CONFIRM] Session key returned no data for token={Token}", token);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "[POST-CONFIRM] Failed to retrieve access token from session for profile fetch");
                                    }
                                }

                                if (profile?.Account != null)
                                {
                                    churchInfo = await _context.ChurchInformation.FirstOrDefaultAsync(c => c.ChurchAccountId == user.Id).ConfigureAwait(true);
                                    if (churchInfo == null)
                                    {
                                        churchInfo = new ChurchInformation
                                        {
                                            ChurchAccountId = user.Id,
                                            Name = profile.Data?.Name ?? "Imported Church",
                                            Description = profile.Account.Description ?? "Imported from ChurchSuite (No Description availlable)",
                                            Phone = profile.Account.Phone ?? "000-000-0000 (No Phone available)",
                                            Churchsuite = new Uri("https://login.churchsuite.com/?account=" + profile.Account.Subdomain),
                                            Website = Uri.TryCreate(profile.Account.Url, UriKind.Absolute, out var webUri) ? webUri : new Uri("https://forestchurches.co.uk/"),
                                            Status = "Open",
                                            Denomination = "Unknown",
                                            Congregation = "Unknown",
                                            Address = string.Join(", ", new string[] { profile.Data.BillingAddress.Line1, profile.Data.BillingAddress.Line2, profile.Data.BillingAddress.County, profile.Data.BillingAddress.Postcode } .Where(s => !string.IsNullOrEmpty(s))),
                                            Parking = false,
                                            Restrooms = false,
                                            WheelchairAccess = false,
                                            Wifi = false,
                                            ChurchSuiteSubdomain = profile.Account.Subdomain,
                                            Refreshments = false,
                                            WorshipHall = false,
                                            Childcare = false,
                                            MeetingHall = false,
                                            Kitchen = false,
                                            OutdoorSpaces = false,
                                            HearingAidSupport = false,
                                        };

                                        try
                                        {
                                            if (!string.IsNullOrWhiteSpace(churchInfo.Churchsuite?.Host) && churchInfo.Churchsuite.Host.EndsWith(".churchsuite.com", StringComparison.OrdinalIgnoreCase))
                                            {
                                                churchInfo.ChurchSuiteSubdomain = churchInfo.Churchsuite.Host.Split('.')[0];
                                            }
                                        }
                                        catch { }

                                        await _context.ChurchInformation.AddAsync(churchInfo).ConfigureAwait(true);
                                        await _context.SaveChangesAsync().ConfigureAwait(true);
                                        _logger.LogInformation("[POST-CONFIRM] Created ChurchInformation for user {UserId} from ChurchSuite profile. Name: {Name}, Phone: {Phone}", 
                                            userId, churchInfo.Name, churchInfo.Phone);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "[POST-CONFIRM] Failed to fetch ChurchSuite profile");
                                // Don't fail the registration - we'll create a default church below
                            }

                            // If church creation failed or ChurchSuite profile wasn't available, create a default church
                            if (churchInfo == null)
                            {
                                try
                                {
                                    churchInfo = await _context.ChurchInformation.FirstOrDefaultAsync(c => c.ChurchAccountId == user.Id).ConfigureAwait(true);
                                    if (churchInfo == null)
                                    {
                                        churchInfo = new ChurchInformation
                                        {
                                            ChurchAccountId = user.Id,
                                            Name = "Default Church",
                                            Description = "Some church description. (This is not an actual church.)",
                                            Status = "Open",
                                            Denomination = "Anglican",
                                            Congregation = "20-50",
                                            Address = "TBA",
                                            Phone = "000-000-0000",
                                            Churchsuite = new Uri("https://churchsuite.com/"),
                                            Website = new Uri("https://forestchurches.co.uk/"),
                                            Parking = true,
                                            Restrooms = true,
                                            WheelchairAccess = true,
                                            Wifi = true,
                                            Refreshments = true,
                                            WorshipHall = true,
                                            Childcare = true,
                                            MeetingHall = true,
                                            Kitchen = true,
                                            OutdoorSpaces = true,
                                            HearingAidSupport = true,
                                        };

                                        await _context.ChurchInformation.AddAsync(churchInfo).ConfigureAwait(true);
                                        await _context.SaveChangesAsync().ConfigureAwait(true);
                                        _logger.LogInformation("[POST-CONFIRM] Created default ChurchInformation for user {UserId}", userId);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "[POST-CONFIRM] Failed to create default ChurchInformation for user {UserId}. Deleting user to maintain referential integrity.", userId);
                                    try
                                    {
                                        await _userManager.DeleteAsync(user).ConfigureAwait(true);
                                        _logger.LogInformation("[POST-CONFIRM] Deleted user {UserId} due to church creation failure.", userId);
                                    }
                                    catch (Exception deleteEx)
                                    {
                                        _logger.LogError(deleteEx, "[POST-CONFIRM] Failed to delete user {UserId} after church creation failure.", userId);
                                    }

                                    ModelState.AddModelError(string.Empty, "An error occurred while setting up your church profile. Please try registering again.");
                                    return Page();
                                }
                            }

                            // Also attempt a profile sync to update any additional fields
                            try
                            {
                                await SyncChurchSuiteProfileAsync(info.Principal, user).ConfigureAwait(true);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "[POST-CONFIRM] SyncChurchSuiteProfileAsync failed (non-fatal)");
                            }
                        }
                        else
                        {
                            // For non-ChurchSuite external providers, create a default church record
                            try
                            {
                                var existingChurch = await _context.ChurchInformation.FirstOrDefaultAsync(c => c.ChurchAccountId == user.Id).ConfigureAwait(true);
                                if (existingChurch == null)
                                {
                                    var defaultChurch = new ChurchInformation
                                    {
                                        ChurchAccountId = user.Id,
                                        Name = "Default Church",
                                        Description = "Some church description. (This is not an actual church.)",
                                        Status = "Open",
                                        Denomination = "Anglican",
                                        Congregation = "20-50",
                                        Address = "TBA",
                                        Phone = "000-000-0000",
                                        Churchsuite = new Uri("https://churchsuite.com/"),
                                        Website = new Uri("https://forestchurches.co.uk/"),
                                        Parking = true,
                                        Restrooms = true,
                                        WheelchairAccess = true,
                                        Wifi = true,
                                        Refreshments = true,
                                        WorshipHall = true,
                                        Childcare = true,
                                        MeetingHall = true,
                                        Kitchen = true,
                                        OutdoorSpaces = true,
                                        HearingAidSupport = true,
                                    };

                                    await _context.ChurchInformation.AddAsync(defaultChurch).ConfigureAwait(true);
                                    await _context.SaveChangesAsync().ConfigureAwait(true);
                                    _logger.LogInformation("[POST-CONFIRM] Created default ChurchInformation for non-ChurchSuite user {UserId}", userId);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "[POST-CONFIRM] Failed to create default ChurchInformation for user {UserId}. Deleting user to maintain referential integrity.", userId);
                                try
                                {
                                    await _userManager.DeleteAsync(user).ConfigureAwait(true);
                                    _logger.LogInformation("[POST-CONFIRM] Deleted user {UserId} due to church creation failure.", userId);
                                }
                                catch (Exception deleteEx)
                                {
                                    _logger.LogError(deleteEx, "[POST-CONFIRM] Failed to delete user {UserId} after church creation failure.", userId);
                                }

                                ModelState.AddModelError(string.Empty, "An error occurred while setting up your church profile. Please try registering again.");
                                return Page();
                            }
                        }

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

                        try
                        {
                            await _mailRepository.StartEmailAsync(Input.Email, userData, "Confirm your email", "./templates/confirm_email.html")
                                .ConfigureAwait(true);
                        }
                        catch (Exception ex)
                        {
                            // Don't let email failures block user registration/sign-in. Log and continue.
                            _logger.LogError(ex, "[POST-CONFIRM] Failed to send confirmation email to {Email}", Input.Email);
                        }

                        // If account confirmation is required, we need to show the link if we don't have a real email sender
                        if (_userManager.Options.SignIn.RequireConfirmedAccount)
                        {
                            _logger.LogInformation("[POST-CONFIRM] RequireConfirmedAccount=true. Redirecting to RegisterConfirmation.");
                            return RedirectToPage("./RegisterConfirmation", new { Email = Input.Email });
                        }

                        _logger.LogInformation("[POST-CONFIRM] Signing in user with provider={Provider}", info.LoginProvider);
                        await _signInManager.SignInAsync(user, isPersistent: false, info.LoginProvider)
                            .ConfigureAwait(true);

                        try
                        {
                            if (!string.IsNullOrWhiteSpace(token))
                            {
                                var sessionKey = $"ExternalLoginTemp:{token}";
                                HttpContext.Session.Remove(sessionKey);
                                _logger.LogInformation("[POST-CONFIRM] Removed temporary session entry for token {Token}", token);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "[POST-CONFIRM] Failed to remove temporary session entry after successful registration");
                        }

                        _logger.LogInformation("[POST-CONFIRM] === SUCCESS === User signed in. Redirecting to {ReturnUrl}", returnUrl);
                        return LocalRedirect(returnUrl);
                    }
                    else
                    {
                        _logger.LogError("[POST-CONFIRM] AddLoginAsync failed: {Errors}", string.Join("; ", result.Errors.Select(e => $"{e.Code}:{e.Description}")));
                    }
                }
                else
                {
                    _logger.LogError("[POST-CONFIRM] CreateAsync failed: {Errors}", string.Join("; ", result.Errors.Select(e => $"{e.Code}:{e.Description}")));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            else
            {
                _logger.LogWarning("[POST-CONFIRM] ModelState INVALID. Errors: {Errors}", 
                    string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            }

            _logger.LogInformation("[POST-CONFIRM] === FAILED === Returning confirmation page");
            ProviderDisplayName = info.ProviderDisplayName;
            ReturnUrl = returnUrl;
            Token = token;
            return Page();
        }

        private ChurchAccount CreateUser()
        {
            try
            {
                // Create a new ChurchAccount and ensure required identity key properties are initialized.
                var user = Activator.CreateInstance<ChurchAccount>();
                // Ensure Id is set (EF Core requires a non-null key when attaching/adding entities).
                user.Id = Guid.NewGuid().ToString();
                user.SecurityStamp = Guid.NewGuid().ToString();
                // Store created date as ISO timestamp for auditing
                user.CreatedDate = DateTime.UtcNow.ToString("o");
                // Ensure non-nullable DB columns have safe defaults to avoid INSERT constraint failures.
                user.ImageArray = Array.Empty<byte>();
                // Role and ImageBlobName are optional; leave as null to be filled later if needed.
                return user;
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

        /// <summary>
        /// Syncs ChurchSuite profile data to the authenticated user's church information.
        /// </summary>
        private async Task SyncChurchSuiteProfileAsync(ClaimsPrincipal principal, ChurchAccount user = null)
        {
            try
            {
                // If user not provided, get the current authenticated user
                if (user == null)
                {
                    var userId = _userManager.GetUserId(User);
                    user = await _userManager.FindByIdAsync(userId).ConfigureAwait(true);
                    if (user == null)
                    {
                        _logger.LogWarning("Could not find authenticated user for ChurchSuite profile sync.");
                        return;
                    }
                }

                // Fetch profile from ChurchSuite OAuth
                var profile = await _churchSuiteService.GetProfileFromOAuthAsync(principal).ConfigureAwait(true);
                if (profile?.Account == null)
                {
                    _logger.LogWarning("Failed to fetch ChurchSuite profile for user {UserId}.", user.Id);
                    return;
                }

                // Get or create church information for this user
                var churchInfo = await _context.ChurchInformation
                    .FirstOrDefaultAsync(c => c.ChurchAccountId == user.Id)
                    .ConfigureAwait(true);

                if (churchInfo == null)
                {
                    churchInfo = new ForestChurches.Models.ChurchInformation
                    {
                        ChurchAccountId = user.Id
                    };
                    await _context.ChurchInformation.AddAsync(churchInfo).ConfigureAwait(true);
                }

                // Update church information from ChurchSuite profile
                churchInfo.Name = profile.Account.Name ?? churchInfo.Name;

                if (!string.IsNullOrEmpty(profile.Account.Url) && Uri.TryCreate(profile.Account.Url, UriKind.Absolute, out var siteUri))
                {
                    churchInfo.Website = siteUri;
                }

                // Update user email if available
                if (!string.IsNullOrEmpty(profile.Account.Email))
                {
                    user.Email = profile.Account.Email;
                    user.NormalizedEmail = _userManager.NormalizeEmail(profile.Account.Email);
                }

                await _context.SaveChangesAsync().ConfigureAwait(true);
                await _userManager.UpdateAsync(user).ConfigureAwait(true);

                _logger.LogInformation("Successfully synced ChurchSuite profile data for user {UserId}.", user.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing ChurchSuite profile data during external login for user.");
                // Continue - don't let profile sync failure prevent login
            }
        }

        /// <summary>
        /// Saves or updates ChurchSuite credentials for a user for later API access.
        /// </summary>
        private async Task SaveChurchSuiteCredentialsAsync(ChurchAccount user, ExternalLoginInfo info, string? token)
        {
            try
            {
                string? accessToken = null;
                string? refreshToken = null;
                DateTime? expiresAt = null;

                // Try to get tokens from external principal claims first
                accessToken = info.Principal?.FindFirstValue("access_token");
                refreshToken = info.Principal?.FindFirstValue("refresh_token");

                // If not found, try the session token data
                if (string.IsNullOrWhiteSpace(accessToken) && !string.IsNullOrWhiteSpace(token))
                {
                    try
                    {
                        var key = $"ExternalLoginTemp:{token}";
                        var json = HttpContext.Session.GetString(key);
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            var dto = System.Text.Json.JsonSerializer.Deserialize<ExternalLoginTempDto>(json);
                            if (dto?.AccessToken != null)
                            {
                                accessToken = dto.AccessToken;
                                refreshToken = dto.RefreshToken;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[SaveChurchSuiteCredentialsAsync] Failed to retrieve tokens from session");
                    }
                }

                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    _logger.LogWarning("[SaveChurchSuiteCredentialsAsync] No access token available for user {UserId}", user.Id);
                    return;
                }

                // Try to get subdomain from profile or claims
                var subdomain = info.Principal?.FindFirstValue("subdomain");
                if (string.IsNullOrWhiteSpace(subdomain))
                {
                    // Try to fetch from ChurchSuite profile if available
                    try
                    {
                        var profile = await _churchSuiteService.GetProfileFromOAuthAsync(info.Principal).ConfigureAwait(true);
                        subdomain = profile?.Account?.Subdomain ?? "unknown";
                    }
                    catch
                    {
                        subdomain = "unknown";
                    }
                }

                // Determine token expiration (usually 1 hour from now if not specified)
                var expiresInString = info.Principal?.FindFirstValue("expires_in");
                if (int.TryParse(expiresInString, out var expiresInSeconds))
                {
                    expiresAt = DateTime.UtcNow.AddSeconds(expiresInSeconds);
                }
                else
                {
                    expiresAt = DateTime.UtcNow.AddHours(1); // Default to 1 hour
                }

                // Check if credential already exists for this user/subdomain combo
                var existingCredential = await _context.ChurchSuiteCredentials
                    .FirstOrDefaultAsync(c => c.UserId == user.Id && c.Subdomain == subdomain)
                    .ConfigureAwait(true);

                if (existingCredential != null)
                {
                    // Update existing credential
                    existingCredential.AccessToken = accessToken;
                    existingCredential.RefreshToken = refreshToken;
                    existingCredential.ExpiresAt = expiresAt;
                    existingCredential.UpdatedAt = DateTime.UtcNow;
                    existingCredential.LastRefreshedAt = DateTime.UtcNow;
                    existingCredential.IsActive = true;
                    _context.ChurchSuiteCredentials.Update(existingCredential);
                    _logger.LogInformation("[SaveChurchSuiteCredentialsAsync] Updated ChurchSuite credentials for user {UserId}, subdomain {Subdomain}", user.Id, subdomain);
                }
                else
                {
                    // Create new credential
                    var newCredential = new ChurchSuiteCredential
                    {
                        UserId = user.Id,
                        Subdomain = subdomain,
                        AccessToken = accessToken,
                        RefreshToken = refreshToken,
                        ExpiresAt = expiresAt,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        LastRefreshedAt = DateTime.UtcNow,
                        IsActive = true
                    };
                    await _context.ChurchSuiteCredentials.AddAsync(newCredential).ConfigureAwait(true);
                    _logger.LogInformation("[SaveChurchSuiteCredentialsAsync] Created new ChurchSuite credentials for user {UserId}, subdomain {Subdomain}", user.Id, subdomain);
                }

                await _context.SaveChangesAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SaveChurchSuiteCredentialsAsync] Failed to save ChurchSuite credentials for user {UserId}", user.Id);
                // Don't throw - this should not block the login process
            }
        }
    }
}