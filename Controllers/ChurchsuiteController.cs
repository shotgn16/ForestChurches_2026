using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ForestChurches.Components.Http;

namespace ForestChurches.Controllers
{
    /// <summary>
    /// API controller for ChurchSuite OAuth authorization flow.
    /// Supports both PKCE (public client) and per-user ClientId flows.
    /// </summary>
    [ApiController]
    [Route("churchsuite")]
    public class ChurchsuiteController : Controller
    {
        private readonly IChurchSuiteAuthenticationService _authService;
        private readonly ILogger<ChurchsuiteController> _logger;

        public ChurchsuiteController(
            IChurchSuiteAuthenticationService authService,
            ILogger<ChurchsuiteController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        /// <summary>
        /// Initiates PKCE OAuth flow (public client - no static secret needed).
        /// This is the primary flow for all users.
        /// </summary>
        [HttpGet("login-pkce")]
        public async Task<IActionResult> LoginPKCE([FromQuery] string? redirect_uri)
        {
            try
            {
                _logger.LogInformation("[ChurchsuiteController] Initiating PKCE login flow");
                var authUrl = await _authService.GeneratePKCEAuthorizationUrlAsync(
                    HttpContext,
                    redirectUri: redirect_uri);

                return Redirect(authUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ChurchsuiteController] Error initiating PKCE login");
                return BadRequest(new { error = "Failed to initiate login", message = ex.Message });
            }
        }

        /// <summary>
        /// Initiates OAuth flow using per-user ClientId (fallback for users with registered apps).
        /// Requires user_id parameter and user must have ClientId configured.
        /// </summary>
        [HttpGet("authorize")]
        public async Task<IActionResult> Authorize([FromQuery] string user_id, [FromQuery] string? redirect_uri)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(user_id))
                {
                    return BadRequest(new { error = "user_id is required" });
                }

                // Ensure a ChurchSuiteCredential row exists for this user so the callback can look it up reliably.
                try
                {
                    var context = HttpContext.RequestServices.GetRequiredService<ForestChurches.Data.ForestChurchesContext>();
                    var userManager = HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ForestChurches.Components.Users.ChurchAccount>>();
                    var user = await userManager.FindByIdAsync(user_id).ConfigureAwait(true);
                    if (user != null)
                    {
                        var existing = await context.ChurchSuiteCredentials.FirstOrDefaultAsync(c => c.UserId == user_id).ConfigureAwait(true);
                        if (existing == null)
                        {
                            var placeholder = new ForestChurches.Models.ChurchSuiteCredential
                            {
                                UserId = user.Id,
                                Subdomain = "unknown",
                                AccessToken = string.Empty,
                                RefreshToken = null,
                                ClientId = user.ClientId,
                                TempCodeVerifier = null,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow,
                                LastRefreshedAt = null,
                                IsActive = false
                            };

                            await context.ChurchSuiteCredentials.AddAsync(placeholder).ConfigureAwait(true);
                            await context.SaveChangesAsync().ConfigureAwait(true);

                            _logger.LogInformation("[ChurchsuiteController] Created placeholder ChurchSuiteCredential for user {UserId}", user_id);
                        }
                        else
                        {
                            // Ensure ClientId is populated on existing credential if available on user
                            if (string.IsNullOrWhiteSpace(existing.ClientId) && !string.IsNullOrWhiteSpace(user.ClientId))
                            {
                                existing.ClientId = user.ClientId;
                                existing.UpdatedAt = DateTime.UtcNow;
                                context.ChurchSuiteCredentials.Update(existing);
                                await context.SaveChangesAsync().ConfigureAwait(true);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[ChurchsuiteController] Failed to ensure ChurchSuiteCredential exists for user {UserId}", user_id);
                    // Continue - the auth service and callback include session fallbacks
                }

                _logger.LogInformation("[ChurchsuiteController] Initiating per-user ClientId flow for user {UserId}", user_id);
                var authUrl = await _authService.GeneratePerUserClientIdAuthorizationUrlAsync(
                    HttpContext,
                    user_id,
                    redirectUri: redirect_uri);

                return Redirect(authUrl);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "[ChurchsuiteController] Invalid user for per-user ClientId flow");
                return BadRequest(new { error = "Invalid user", message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "[ChurchsuiteController] User does not have ClientId configured");
                // Fall back to PKCE flow
                _logger.LogInformation("[ChurchsuiteController] Falling back to PKCE flow for user {UserId}", user_id);
                try
                {
                    var authUrl = await _authService.GeneratePKCEAuthorizationUrlAsync(
                        HttpContext,
                        redirectUri: redirect_uri,
                        userId: user_id);
                    return Redirect(authUrl);
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "[ChurchsuiteController] Fallback to PKCE flow failed");
                    return BadRequest(new { error = "User does not have ClientId configured and PKCE is unavailable", message = fallbackEx.Message });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ChurchsuiteController] Error initiating per-user ClientId flow");
                return BadRequest(new { error = "Failed to initiate login", message = ex.Message });
            }
        }

        /// <summary>
        /// Checks if user has active ChurchSuite authorization.
        /// Used by UI to determine if authorization is needed.
        /// </summary>
        [HttpGet("check-auth")]
        public async Task<IActionResult> CheckAuthorization([FromQuery] string user_id)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(user_id))
                {
                    return BadRequest(new { error = "user_id is required" });
                }

                var tokenService = HttpContext.RequestServices.GetRequiredService<IChurchSuiteTokenService>();
                var hasAuth = await tokenService.UserHasAuthorizationAsync(user_id);
                var hasValidToken = await tokenService.HasValidTokenAsync(user_id);

                return Ok(new
                {
                    hasAuthorization = hasAuth,
                    hasValidToken = hasValidToken,
                    requiresAuthorization = !hasAuth
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ChurchsuiteController] Error checking authorization for user {UserId}");
                return StatusCode(500, new { error = "Failed to check authorization", message = ex.Message });
            }
        }
    }
}
