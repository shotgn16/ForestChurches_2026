using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ForestChurches.Components.Http;
using ForestChurches.Components.Users;
using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ForestChurches.Pages.ChurchSuite
{
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class CallbackModel : PageModel
    {
        private readonly ForestChurchesContext _context;
        private readonly UserManager<ChurchAccount> _userManager;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IChurchSuiteTokenService _tokenService;
        private readonly ILogger<CallbackModel> _logger;
        private readonly IChurchSuiteService _churchSuiteService;

        private readonly ForestChurches.Components.Users.IManagedChurches _churches;

        public CallbackModel(ForestChurchesContext context, UserManager<ChurchAccount> userManager, IHttpClientFactory httpClientFactory, IChurchSuiteTokenService tokenService, IChurchSuiteService churchSuiteService, ILogger<CallbackModel> logger, ForestChurches.Components.Users.IManagedChurches churches)
        {
            _churches = churches;
            _context = context;
            _userManager = userManager;
            _httpClientFactory = httpClientFactory;
            _tokenService = tokenService;
            _churchSuiteService = churchSuiteService;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync(string code, string state, string user_id, string redirect_uri)
        {
            // Security: tokens are only ever stored against the signed-in user. A user_id in the
            // query string or the session must match them, otherwise the request is refused.
            var signedInUserId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(signedInUserId))
            {
                return Challenge();
            }

            string? sessionUserId = null;
            if (!string.IsNullOrWhiteSpace(state))
            {
                try
                {
                    sessionUserId = HttpContext.Session.GetString($"pkce:{state}:user_id");
                    var sessRedirect = HttpContext.Session.GetString($"pkce:{state}:redirect_uri");
                    if (string.IsNullOrWhiteSpace(redirect_uri) && !string.IsNullOrWhiteSpace(sessRedirect))
                    {
                        redirect_uri = sessRedirect;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[Callback] Could not read PKCE session data for state {State}", state);
                }
            }

            // Only redirect back into this site
            if (string.IsNullOrWhiteSpace(redirect_uri) || !Url.IsLocalUrl(redirect_uri))
            {
                redirect_uri = "/";
            }

            if ((!string.IsNullOrWhiteSpace(user_id) && !string.Equals(user_id, signedInUserId, StringComparison.Ordinal)) ||
                (!string.IsNullOrWhiteSpace(sessionUserId) && !string.Equals(sessionUserId, signedInUserId, StringComparison.Ordinal)))
            {
                _logger.LogWarning("[Callback] Rejected ChurchSuite callback: requested user does not match the signed-in user {UserId}", signedInUserId);
                TempData["ChurchSuiteAuthError"] = "Authorization failed: this ChurchSuite connection was started by a different account.";
                return Redirect(redirect_uri);
            }
            user_id = signedInUserId;

            // Basic validation
            if (string.IsNullOrWhiteSpace(code))
            {
                TempData["ChurchSuiteAuthError"] = "Authorization failed: missing code.";
                return Redirect(redirect_uri);
            }

            // Find credential for this user
            var credential = await _context.ChurchSuiteCredentials.FirstOrDefaultAsync(c => c.UserId == user_id);
            if (credential == null)
            {
                // Attempt to create a placeholder credential so callback can complete.
                try
                {
                    var user = await _userManager.FindByIdAsync(user_id);
                    var sessionVerifier = string.IsNullOrWhiteSpace(state) ? null : HttpContext.Session.GetString($"pkce:{state}:verifier");

                    credential = new ChurchSuiteCredential
                    {
                        UserId = user_id,
                        Subdomain = "unknown",
                        AccessToken = string.Empty,
                        RefreshToken = null,
                        ClientId = user?.ClientId,
                        TempCodeVerifier = sessionVerifier,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        LastRefreshedAt = null,
                        IsActive = false
                    };

                    await _context.ChurchSuiteCredentials.AddAsync(credential);
                    await _context.SaveChangesAsync().ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Callback] Failed to create placeholder ChurchSuiteCredential for user {UserId}", user_id);
                    // If creation fails, surface a user-friendly error and stop.
                    TempData["ChurchSuiteAuthError"] = "Authorization failed: no ChurchSuite credential found for this user.";
                    return Redirect(string.IsNullOrWhiteSpace(redirect_uri) ? "/" : redirect_uri);
                }
            }

            // Ensure we have a PKCE code_verifier - try DB first, then session fallback using the state
            var codeVerifier = credential.TempCodeVerifier;
            if (string.IsNullOrWhiteSpace(codeVerifier) && !string.IsNullOrWhiteSpace(state))
            {
                try
                {
                    var verifierKey = $"pkce:{state}:verifier";
                    var sessionVerifier = HttpContext.Session.GetString(verifierKey);
                    if (!string.IsNullOrWhiteSpace(sessionVerifier))
                    {
                        codeVerifier = sessionVerifier;

                        // Persist recovered verifier to DB so subsequent retries work even if session is lost
                        credential.TempCodeVerifier = codeVerifier;
                        credential.UpdatedAt = DateTime.UtcNow;
                        _context.ChurchSuiteCredentials.Update(credential);
                        await _context.SaveChangesAsync().ConfigureAwait(true);

                        _ = 0; // no-op to keep code compact
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[Callback] Failed to persist recovered PKCE verifier for state {State} user {UserId}", state, user_id);
                    // Continue to fail below if still missing
                }
            }

            if (string.IsNullOrWhiteSpace(codeVerifier))
            {
                TempData["ChurchSuiteAuthError"] = "Authorization failed: missing PKCE verifier. Please try again.";
                return Redirect(string.IsNullOrWhiteSpace(redirect_uri) ? "/" : redirect_uri);
            }

            var clientId = credential.ClientId;
            if (string.IsNullOrWhiteSpace(clientId))
            {
                TempData["ChurchSuiteAuthError"] = "Authorization failed: no client id configured for your account.";
                return Redirect(string.IsNullOrWhiteSpace(redirect_uri) ? "/" : redirect_uri);
            }

            var tokenEndpoint = "https://login.churchsuite.com/oauth2/token";
            var client = _httpClientFactory.CreateClient();

            var callbackUrl = $"{Request.Scheme}://{Request.Host}/ChurchSuite/Callback";
            _logger.LogInformation("[Callback] Exchanging code for tokens. user:{UserId}, clientId:{ClientId}, state:{State}, callback:{CallbackUrl}", user_id, clientId, state, callbackUrl);

            var form = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("redirect_uri", callbackUrl),
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("code_verifier", codeVerifier)
            };

            var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
            {
                Content = new FormUrlEncodedContent(form)
            };

            HttpResponseMessage response;
            string body;
            try
            {
                response = await client.SendAsync(request).ConfigureAwait(true);
                body = await response.Content.ReadAsStringAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Callback] Exception calling token endpoint for user {UserId}", user_id);
                TempData["ChurchSuiteAuthError"] = "Token exchange failed: network error.";
                return Redirect(string.IsNullOrWhiteSpace(redirect_uri) ? "/" : redirect_uri);
            }

            if (!response.IsSuccessStatusCode)
            {
                // Try to extract provider error details from JSON response
                string providerError = null;
                string providerDescription = null;
                try
                {
                    var parsed = JsonSerializer.Deserialize<JsonElement>(body);
                    if (parsed.ValueKind == JsonValueKind.Object)
                    {
                        if (parsed.TryGetProperty("error", out var pe)) providerError = pe.GetString();
                        if (parsed.TryGetProperty("error_description", out var pd)) providerDescription = pd.GetString();
                    }
                }
                catch { /* ignore parse errors */ }

                var userMessage = providerError != null
                    ? $"Token exchange failed: {providerError}. {providerDescription ?? string.Empty} Ensure the ClientId is valid, the redirect URI is registered exactly as '{Request.Scheme}://{Request.Host}/ChurchSuite/Callback', and that the ChurchSuite app is configured as a public/PKCE client."
                    : $"Token exchange failed: {response.StatusCode}. Response: {body}";

                _logger.LogWarning("[Callback] Token exchange failed for user {UserId}. Status: {StatusCode}. ProviderError: {ProviderError}. ResponseBody: {ResponseBody}", user_id, response.StatusCode, providerError, body);
                TempData["ChurchSuiteAuthError"] = userMessage;
                return Redirect(string.IsNullOrWhiteSpace(redirect_uri) ? "/" : redirect_uri);
            }

            var json = JsonSerializer.Deserialize<JsonElement>(body);
            var accessToken = json.GetProperty("access_token").GetString();
            var refreshToken = json.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
            var expiresIn = json.TryGetProperty("expires_in", out var ei) ? ei.GetInt32() : 3600;

            // Store credentials via token service
            await _tokenService.StoreCredentialsAsync(user_id, accessToken, refreshToken, expiresIn, credential.Subdomain, clientId).ConfigureAwait(true);

            // Attempt to fetch ChurchSuite profile and persist to local ChurchInformation record
            try
            {
                if (!string.IsNullOrWhiteSpace(accessToken))
                {
                    var profile = await _churchSuiteService.GetProfileFromOAuthAsync(accessToken).ConfigureAwait(true);
                    if (profile?.Account != null)
                    {
                        var acct = profile.Account;
                        // The church the user is working on (accounts can manage several churches).
                        var church = await _churches.CurrentAsync(HttpContext, user_id).ConfigureAwait(true);
                        if (church != null)
                        {
                            if (!string.IsNullOrWhiteSpace(acct.Name)) church.Name = acct.Name;
                            if (!string.IsNullOrWhiteSpace(acct.Description)) church.Description = acct.Description;
                            if (!string.IsNullOrWhiteSpace(acct.Phone)) church.Phone = acct.Phone;

                            // Use billing address if present (prefer billing address fields)
                            if (acct.BillingAddress != null)
                            {
                                var parts = new List<string>();
                                if (!string.IsNullOrWhiteSpace(acct.BillingAddress.Line1)) parts.Add(acct.BillingAddress.Line1);
                                if (!string.IsNullOrWhiteSpace(acct.BillingAddress.Line2)) parts.Add(acct.BillingAddress.Line2);
                                if (!string.IsNullOrWhiteSpace(acct.BillingAddress.Line3)) parts.Add(acct.BillingAddress.Line3);
                                var cityCounty = string.Empty;
                                if (!string.IsNullOrWhiteSpace(acct.BillingAddress.City)) cityCounty = acct.BillingAddress.City;
                                if (!string.IsNullOrWhiteSpace(acct.BillingAddress.County)) cityCounty = string.IsNullOrWhiteSpace(cityCounty) ? acct.BillingAddress.County : cityCounty + " " + acct.BillingAddress.County;
                                if (!string.IsNullOrWhiteSpace(cityCounty)) parts.Add(cityCounty);
                                if (!string.IsNullOrWhiteSpace(acct.BillingAddress.Postcode)) parts.Add(acct.BillingAddress.Postcode);
                                if (!string.IsNullOrWhiteSpace(acct.BillingAddress.Country)) parts.Add(acct.BillingAddress.Country);
                                var formatted = string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
                                if (!string.IsNullOrWhiteSpace(formatted)) church.Address = formatted;
                            }

                            // Website: try account URL, add scheme if missing
                            if (!string.IsNullOrWhiteSpace(acct.Url))
                            {
                                var url = acct.Url.Trim();
                                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                                {
                                    url = "https://" + url;
                                }
                                try { church.Website = new Uri(url); } catch { /* ignore bad urls */ }
                            }

                            if (!string.IsNullOrWhiteSpace(acct.Subdomain))
                            {
                                church.ChurchSuiteSubdomain = acct.Subdomain;
                                // Build login URL that opens ChurchSuite account by subdomain
                                try
                                {
                                    var csUrl = $"https://login.churchsuite.com/?account={acct.Subdomain}";
                                    church.Churchsuite = new Uri(csUrl);
                                }
                                catch { /* ignore malformed subdomain */ }
                            }

                            _context.ChurchInformation.Update(church);
                            await _context.SaveChangesAsync().ConfigureAwait(true);
                            TempData["ChurchSuiteSyncSuccess"] = "ChurchSuite profile synchronized successfully.";
                        }
                        else
                        {
                            TempData["ChurchSuiteSyncError"] = "ChurchSuite authorized but local church record not found.";
                        }

                    }
                    else
                    {
                        _logger.LogWarning("[Callback] ChurchSuite profile fetch returned null for user {UserId}", user_id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Callback] Failed to sync ChurchSuite profile for user {UserId}", user_id);
                TempData["ChurchSuiteSyncError"] = "Failed to sync ChurchSuite profile.";
            }

            // Clear code_verifier now it's used
            credential.TempCodeVerifier = null;
            credential.UpdatedAt = DateTime.UtcNow;
            _context.ChurchSuiteCredentials.Update(credential);
            await _context.SaveChangesAsync().ConfigureAwait(true);

            // Redirect to requested page
            return Redirect(string.IsNullOrWhiteSpace(redirect_uri) ? "/" : redirect_uri);
        }
    }
}
