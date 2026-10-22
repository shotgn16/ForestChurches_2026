using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ForestChurches.Components.Users;
using System.Security.Claims;
using System.Text.Json;
using ForestChurches.Data;
using ForestChurches.Models;
using ForestChurches.Components.Http;
using Microsoft.EntityFrameworkCore;

namespace ForestChurches.Controllers
{
    /// <summary>
    /// Handles OAuth callback endpoints for external authentication providers.
    /// Specifically designed for ChurchSuite OAuth flow callback handling.
    /// Uses centralized token service for credential management.
    /// </summary>
    [AllowAnonymous]
    public class OAuthCallbackController : Controller
    {
        private readonly SignInManager<ChurchAccount> _signInManager;
        private readonly UserManager<ChurchAccount> _userManager;
        private readonly ILogger<OAuthCallbackController> _logger;
        private readonly IChurchSuiteTokenService _tokenService;
        private readonly IChurchSuiteAuthenticationService _authService;

        public OAuthCallbackController(
            SignInManager<ChurchAccount> signInManager,
            UserManager<ChurchAccount> userManager,
            ILogger<OAuthCallbackController> logger,
            IChurchSuiteTokenService tokenService,
            IChurchSuiteAuthenticationService authService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _tokenService = tokenService;
            _authService = authService;
        }

        /// <summary>
        /// ChurchSuite OAuth callback endpoint.
        /// Handles token exchange, credential storage, and user authentication.
        /// </summary>
        [HttpGet("/signin-churchsuite")]
        public async Task<IActionResult> SignInChurchSuite()
        {
            var code = Request.Query["code"].ToString();
            var state = Request.Query["state"].ToString();
            var error = Request.Query["error"].ToString();

            // Check for OAuth errors
            if (!string.IsNullOrWhiteSpace(error))
            {
                _logger.LogWarning("ChurchSuite OAuth error: {Error}", error);
                return RedirectToPage("/Identity/Account/Login", 
                    new { area = "Identity", error = $"ChurchSuite authorization failed: {error}" });
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                _logger.LogWarning("SignInChurchSuite callback received without authorization code");
                return RedirectToPage("/Identity/Account/Login", new { area = "Identity" });
            }

            try
            {
                // Retrieve PKCE challenge verifier from session
                var codeVerifier = _authService.GetPKCEVerifierFromSession(HttpContext, state);
                var optionalUserId = HttpContext.Session.GetString($"pkce:{state}:user_id");
                var incomingRedirect = HttpContext.Session.GetString($"pkce:{state}:redirect_uri") ?? "/";
                if (!Url.IsLocalUrl(incomingRedirect)) incomingRedirect = "/"; // never redirect off-site

                if (string.IsNullOrWhiteSpace(codeVerifier))
                {
                    _logger.LogWarning("[OAuthCallback] PKCE code_verifier not found in session for state {State}", state);
                    return RedirectToPage("/Identity/Account/Login", new { area = "Identity" });
                }

                // Exchange authorization code for access token
                var tokenExchangeResult = await ExchangeCodeForTokenAsync(code, state, codeVerifier, optionalUserId);
                if (!tokenExchangeResult.IsSuccess)
                {
                    _logger.LogWarning("[OAuthCallback] Token exchange failed: {Message}", tokenExchangeResult.ErrorMessage);
                    return RedirectToPage("/Identity/Account/Login", 
                        new { area = "Identity", error = "Token exchange failed" });
                }

                // Clear PKCE data from session
                _authService.ClearPKCEDataFromSession(HttpContext, state);

                // Security: only ever use the user who is actually signed in. A user ID stored when the
                // connection was started must belong to that same user; it is never used to sign anyone in.
                var authenticatedUser = await _userManager.GetUserAsync(HttpContext.User);
                if (!string.IsNullOrWhiteSpace(optionalUserId) &&
                    (authenticatedUser == null || !string.Equals(authenticatedUser.Id, optionalUserId, StringComparison.Ordinal)))
                {
                    _logger.LogWarning("[OAuthCallback] Rejected ChurchSuite callback: the connection was started for a different user or nobody is signed in");
                    return RedirectToPage("/Account/Login",
                        new { area = "Identity", error = "Please sign in to connect ChurchSuite to your account." });
                }

                // If we have an authenticated user, store credentials and proceed
                if (authenticatedUser != null)
                {
                    _logger.LogInformation("[OAuthCallback] Storing ChurchSuite credentials for authenticated user {UserId}", authenticatedUser.Id);

                    try
                    {
                        await _tokenService.StoreCredentialsAsync(
                            authenticatedUser.Id,
                            tokenExchangeResult.AccessToken,
                            tokenExchangeResult.RefreshToken,
                            tokenExchangeResult.ExpiresIn,
                            subdomain: tokenExchangeResult.Subdomain,
                            clientId: authenticatedUser.ClientId);

                        // Refresh sign-in
                        await _signInManager.RefreshSignInAsync(authenticatedUser);
                        return Redirect(incomingRedirect);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[OAuthCallback] Failed to store credentials for authenticated user {UserId}", authenticatedUser.Id);
                        return RedirectToPage("/Identity/Account/Login", 
                            new { area = "Identity", error = "Failed to store ChurchSuite credentials" });
                    }
                }

                // If no authenticated user, create external login session for registration/sign-in
                _logger.LogInformation("[OAuthCallback] No authenticated user found. Creating external login session for registration.");
                var externalLoginSessionToken = Guid.NewGuid().ToString("N");
                var displayName = tokenExchangeResult.DisplayName ?? "ChurchSuite User";
                var providerKey = tokenExchangeResult.ProviderKey ?? Guid.NewGuid().ToString();

                var externalLoginDto = new
                {
                    Provider = "ChurchSuite",
                    ProviderKey = providerKey,
                    DisplayName = displayName,
                    AccessToken = tokenExchangeResult.AccessToken,
                    RefreshToken = tokenExchangeResult.RefreshToken,
                    Claims = tokenExchangeResult.Claims ?? new List<object>()
                };

                var jsonData = System.Text.Json.JsonSerializer.Serialize(externalLoginDto);
                try
                {
                    HttpContext.Session.SetString($"ExternalLoginTemp:{externalLoginSessionToken}", jsonData);
                    await HttpContext.Session.CommitAsync();
                    _logger.LogInformation("[OAuthCallback] Stored external login session for new user: {Token}", externalLoginSessionToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[OAuthCallback] Failed to store external login session");
                    return RedirectToPage("/Identity/Account/Login", 
                        new { area = "Identity", error = "Failed to create session" });
                }

                var callbackUrl = $"/Identity/Account/ExternalLogin?handler=Callback&token={externalLoginSessionToken}&returnUrl={Uri.EscapeDataString(incomingRedirect)}";
                return Redirect(callbackUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OAuthCallback] Unhandled error in ChurchSuite callback");
                return RedirectToPage("/Identity/Account/Login", new { area = "Identity" });
            }
        }

        /// <summary>
        /// Exchanges authorization code for access/refresh tokens.
        /// </summary>
        private async Task<TokenExchangeResult> ExchangeCodeForTokenAsync(
            string code, string state, string codeVerifier, string? optionalUserId)
        {
            try
            {
                // Determine ClientId to use
                string clientId = null;

                // Try to get per-user ClientId if user_id was provided
                if (!string.IsNullOrWhiteSpace(optionalUserId))
                {
                    try
                    {
                        var user = await _userManager.FindByIdAsync(optionalUserId);
                        if (user != null && !string.IsNullOrWhiteSpace(user.ClientId))
                        {
                            clientId = user.ClientId;
                            _logger.LogInformation("[OAuthCallback] Using per-user ClientId for token exchange");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[OAuthCallback] Failed to retrieve user for ClientId lookup");
                    }
                }

                // Fallback to configured global ClientId
                if (string.IsNullOrWhiteSpace(clientId))
                {
                    var optionsService = HttpContext.RequestServices.GetRequiredService<
                        Microsoft.Extensions.Options.IOptions<ForestChurches.Components.Configuration.ChurchSuiteOAuthOptions>>();
                    clientId = optionsService?.Value?.ClientId;
                    if (!string.IsNullOrWhiteSpace(clientId))
                    {
                        _logger.LogInformation("[OAuthCallback] Using configured global ClientId for token exchange");
                    }
                }

                if (string.IsNullOrWhiteSpace(clientId))
                {
                    _logger.LogError("[OAuthCallback] No ClientId available for token exchange");
                    return TokenExchangeResult.Failed("No ClientId configured");
                }

                // Exchange code for token
                var httpFactory = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>();
                var client = httpFactory.CreateClient();

                var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://login.churchsuite.com/oauth2/token");
                var parameters = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("grant_type", "authorization_code"),
                    new KeyValuePair<string, string>("code", code),
                    new KeyValuePair<string, string>("redirect_uri", $"https://{Request.Host}/signin-churchsuite"),
                    new KeyValuePair<string, string>("client_id", clientId),
                    new KeyValuePair<string, string>("code_verifier", codeVerifier)
                };

                tokenRequest.Content = new FormUrlEncodedContent(parameters);
                var response = await client.SendAsync(tokenRequest);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("[OAuthCallback] Token exchange failed with status {Status}: {Body}", response.StatusCode, errorBody);
                    return TokenExchangeResult.Failed($"Token exchange failed: {response.StatusCode}");
                }

                var body = await response.Content.ReadAsStringAsync();
                var json = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(body);

                var accessToken = json.TryGetProperty("access_token", out var at) ? at.GetString() : null;
                var refreshToken = json.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
                var expiresIn = json.TryGetProperty("expires_in", out var ei) ? ei.GetInt32() : 3600;

                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    _logger.LogWarning("[OAuthCallback] Token response missing access_token");
                    return TokenExchangeResult.Failed("No access_token in response");
                }

                _logger.LogInformation("[OAuthCallback] Successfully exchanged authorization code for access token");

                // Fetch profile information if possible
                string subdomain = null;
                string displayName = null;
                string providerKey = null;
                List<object> claims = null;

                try
                {
                    var churchSuiteService = HttpContext.RequestServices.GetService(typeof(IChurchSuiteService)) as IChurchSuiteService;
                    if (churchSuiteService != null)
                    {
                        var profile = await churchSuiteService.GetProfileFromOAuthAsync(accessToken);
                        if (profile?.Account != null)
                        {
                            subdomain = profile.Account.Subdomain;
                            displayName = profile.Account.Name ?? profile.Account.Email;
                            providerKey = profile.Account.Uuid ?? profile.Account.Subdomain;

                            claims = new List<object>();
                            if (!string.IsNullOrWhiteSpace(displayName))
                                claims.Add(new { Type = "name", Value = displayName });
                            if (!string.IsNullOrWhiteSpace(profile.Account.Email))
                                claims.Add(new { Type = "email", Value = profile.Account.Email });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[OAuthCallback] Failed to fetch profile after token exchange");
                }

                return TokenExchangeResult.CreateSuccess(
                    accessToken, refreshToken, expiresIn,
                    subdomain: subdomain,
                    displayName: displayName,
                    providerKey: providerKey,
                    claims: claims);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OAuthCallback] Exception during token exchange");
                return TokenExchangeResult.Failed(ex.Message);
            }
        }

        /// <summary>
        /// Diagnoses OAuth configuration and callback setup.
        /// Useful for troubleshooting OAuth issues in development.
        /// </summary>
        [HttpGet("oauth-debug")]
        public IActionResult OAuthDebug()
        {
            // Diagnostics only: not available outside Development.
            if (!string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound();
            }

            if (!User.Identity.IsAuthenticated)
            {
                return Ok(new
                {
                    status = "debug_info",
                    message = "OAuth Debug Info",
                    callbackUrl = Url.Action("SignInChurchSuite", "OAuthCallback", null, Request.Scheme, Request.Host.ToString()),
                    currentUser = "Not authenticated",
                    timestamp = DateTime.UtcNow
                });
            }

            var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
            return Ok(new
            {
                status = "debug_info",
                message = "OAuth Debug Info",
                callbackUrl = Url.Action("SignInChurchSuite", "OAuthCallback", null, Request.Scheme, Request.Host.ToString()),
                currentUser = User.Identity.Name,
                isAuthenticated = User.Identity.IsAuthenticated,
                claims = claims,
                timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Result of token exchange operation.
        /// </summary>
        private class TokenExchangeResult
        {
            public bool IsSuccess { get; set; }
            public string AccessToken { get; set; }
            public string RefreshToken { get; set; }
            public int ExpiresIn { get; set; }
            public string Subdomain { get; set; }
            public string DisplayName { get; set; }
            public string ProviderKey { get; set; }
            public List<object> Claims { get; set; }
            public string ErrorMessage { get; set; }

            public static TokenExchangeResult CreateSuccess(
                string accessToken, string refreshToken, int expiresIn,
                string subdomain = null, string displayName = null, 
                string providerKey = null, List<object> claims = null)
            {
                return new TokenExchangeResult
                {
                    IsSuccess = true,
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    ExpiresIn = expiresIn,
                    Subdomain = subdomain,
                    DisplayName = displayName,
                    ProviderKey = providerKey,
                    Claims = claims
                };
            }

            public static TokenExchangeResult Failed(string errorMessage)
            {
                return new TokenExchangeResult
                {
                    IsSuccess = false,
                    ErrorMessage = errorMessage
                };
            }
        }
    }
}
