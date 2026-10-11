using ForestChurches.Components.Configuration;
using ForestChurches.Components.Users;
using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace ForestChurches.Components.Http
{
    /// <summary>
    /// Service for orchestrating ChurchSuite PKCE OAuth authentication flow.
    /// Handles code challenge/verifier generation and authorization URL construction.
    /// </summary>
    public interface IChurchSuiteAuthenticationService
    {
        /// <summary>
        /// Initiates PKCE flow for public client (no static secret).
        /// Stores challenge verifier in session and returns authorization URL.
        /// </summary>
        Task<string> GeneratePKCEAuthorizationUrlAsync(
            HttpContext httpContext,
            string? redirectUri = null,
            string? userId = null);

        /// <summary>
        /// Initiates per-user ClientId flow for users with registered apps.
        /// </summary>
        Task<string> GeneratePerUserClientIdAuthorizationUrlAsync(
            HttpContext httpContext,
            string userId,
            string? redirectUri = null);

        /// <summary>
        /// Validates and retrieves PKCE challenge verifier from session.
        /// </summary>
        string? GetPKCEVerifierFromSession(HttpContext httpContext, string state);

        /// <summary>
        /// Stores PKCE challenge verifier in session.
        /// </summary>
        void StorePKCEVerifierInSession(HttpContext httpContext, string state, string verifier, string? userId = null, string? redirectUri = null);

        /// <summary>
        /// Clears PKCE data from session.
        /// </summary>
        void ClearPKCEDataFromSession(HttpContext httpContext, string state);
    }

    public class ChurchSuiteAuthenticationService : IChurchSuiteAuthenticationService
    {
        private readonly IOptions<ChurchSuiteOAuthOptions> _options;
        private readonly ILogger<ChurchSuiteAuthenticationService> _logger;
        private readonly UserManager<ChurchAccount> _userManager;
        private readonly ForestChurchesContext _context;

        public ChurchSuiteAuthenticationService(
            IOptions<ChurchSuiteOAuthOptions> options,
            ILogger<ChurchSuiteAuthenticationService> logger,
            UserManager<ChurchAccount> userManager,
            ForestChurchesContext context)
        {
            _options = options;
            _logger = logger;
            _userManager = userManager;
            _context = context;
        }

        /// <summary>
        /// Generates PKCE authorization URL for public client flow.
        /// </summary>
        public async Task<string> GeneratePKCEAuthorizationUrlAsync(
            HttpContext httpContext,
            string? redirectUri = null,
            string? userId = null)
        {
            // Generate PKCE parameters
            var codeVerifier = GenerateCodeVerifier();
            var codeChallenge = GenerateCodeChallenge(codeVerifier);
            var state = Guid.NewGuid().ToString("N");

            // Store verifier and metadata in session
            StorePKCEVerifierInSession(httpContext, state, codeVerifier, userId, redirectUri);

            // If a userId was supplied, persist the verifier to DB so the callback can succeed even if session is lost
            if (!string.IsNullOrWhiteSpace(userId))
            {
                try
                {
                    var cred = await _context.ChurchSuiteCredentials.FirstOrDefaultAsync(c => c.UserId == userId).ConfigureAwait(true);
                    if (cred == null)
                    {
                        cred = new ChurchSuiteCredential
                        {
                            UserId = userId,
                            Subdomain = "unknown",
                            AccessToken = string.Empty,
                            RefreshToken = null,
                            ClientId = _options?.Value?.ClientId,
                            TempCodeVerifier = codeVerifier,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                            IsActive = false
                        };

                        await _context.ChurchSuiteCredentials.AddAsync(cred).ConfigureAwait(true);
                    }
                    else
                    {
                        cred.TempCodeVerifier = codeVerifier;
                        cred.UpdatedAt = DateTime.UtcNow;
                        _context.ChurchSuiteCredentials.Update(cred);
                    }

                    await _context.SaveChangesAsync().ConfigureAwait(true);
                    _logger.LogInformation("[AuthService] Persisted PKCE verifier to DB for user {UserId}", userId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[AuthService] Failed to persist PKCE verifier to DB for user {UserId}", userId);
                }
            }

            var clientId = _options?.Value?.ClientId;
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new InvalidOperationException("ChurchSuite ClientId not configured for PKCE flow");
            }

            // Construct authorization URL
            var callbackUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/ChurchSuite/Callback";
            var authorizeUrl = new StringBuilder("https://login.churchsuite.com/oauth2/authorize?");
            authorizeUrl.Append($"response_type=code&client_id={Uri.EscapeDataString(clientId)}");
            authorizeUrl.Append($"&redirect_uri={Uri.EscapeDataString(callbackUrl)}");
            authorizeUrl.Append($"&scope=full_access%20offline_access");
            authorizeUrl.Append($"&state={state}");
            authorizeUrl.Append($"&code_challenge={Uri.EscapeDataString(codeChallenge)}&code_challenge_method=S256");

            var finalUrl = authorizeUrl.ToString();
            _logger.LogInformation("[AuthService] Generated PKCE authorization URL for state {State}. CallbackUrl: {CallbackUrl}, AuthUrl: {AuthUrl}", state, callbackUrl, finalUrl);

            return finalUrl;
        }

        /// <summary>
        /// Generates authorization URL for per-user ClientId flow.
        /// </summary>
        public async Task<string> GeneratePerUserClientIdAuthorizationUrlAsync(
            HttpContext httpContext,
            string userId,
            string? redirectUri = null)
        {
            // Validate user exists and has a ClientId
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                throw new ArgumentException($"User {userId} not found");
            }

            if (string.IsNullOrWhiteSpace(user.ClientId))
            {
                throw new InvalidOperationException($"User {userId} does not have a ClientId configured");
            }

            // Generate PKCE parameters
            var codeVerifier = GenerateCodeVerifier();
            var codeChallenge = GenerateCodeChallenge(codeVerifier);
            var state = Guid.NewGuid().ToString("N");

            // Store verifier and metadata in session
            StorePKCEVerifierInSession(httpContext, state, codeVerifier, userId, redirectUri);

            // Persist verifier to DB for this user so the callback can read it even if session cookie is lost
            try
            {
                var cred = await _context.ChurchSuiteCredentials.FirstOrDefaultAsync(c => c.UserId == userId).ConfigureAwait(true);
                if (cred == null)
                {
                    cred = new ChurchSuiteCredential
                    {
                        UserId = userId,
                        Subdomain = "unknown",
                        AccessToken = string.Empty,
                        RefreshToken = null,
                        ClientId = user.ClientId,
                        TempCodeVerifier = codeVerifier,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        IsActive = false
                    };

                    await _context.ChurchSuiteCredentials.AddAsync(cred).ConfigureAwait(true);
                }
                else
                {
                    cred.TempCodeVerifier = codeVerifier;
                    cred.ClientId = user.ClientId ?? cred.ClientId;
                    cred.UpdatedAt = DateTime.UtcNow;
                    _context.ChurchSuiteCredentials.Update(cred);
                }

                await _context.SaveChangesAsync().ConfigureAwait(true);
                _logger.LogInformation("[AuthService] Persisted PKCE verifier to DB for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AuthService] Failed to persist PKCE verifier to DB for user {UserId}", userId);
            }

            // Construct authorization URL with per-user ClientId
            var callbackUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/ChurchSuite/Callback";
            var authorizeUrl = new StringBuilder("https://login.churchsuite.com/oauth2/authorize?");
            authorizeUrl.Append($"response_type=code&client_id={Uri.EscapeDataString(user.ClientId)}");
            authorizeUrl.Append($"&redirect_uri={Uri.EscapeDataString(callbackUrl)}");
            authorizeUrl.Append($"&scope=full_access%20offline_access");
            authorizeUrl.Append($"&state={state}");
            authorizeUrl.Append($"&code_challenge={Uri.EscapeDataString(codeChallenge)}&code_challenge_method=S256");

            var finalUrl = authorizeUrl.ToString();
            _logger.LogInformation("[AuthService] Generated per-user ClientId authorization URL for user {UserId}, state {State}. CallbackUrl: {CallbackUrl}, AuthUrl: {AuthUrl}", userId, state, callbackUrl, finalUrl);

            return finalUrl;
        }

        /// <summary>
        /// Retrieves PKCE verifier from session.
        /// </summary>
        public string? GetPKCEVerifierFromSession(HttpContext httpContext, string state)
        {
            try
            {
                var verifierKey = $"pkce:{state}:verifier";
                var verifier = httpContext.Session.GetString(verifierKey);

                if (!string.IsNullOrWhiteSpace(verifier))
                {
                    _logger.LogInformation("[AuthService] Retrieved PKCE verifier from session for state {State}", state);
                }
                else
                {
                    _logger.LogWarning("[AuthService] PKCE verifier not found in session for state {State}", state);
                }

                return verifier;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AuthService] Failed to retrieve PKCE verifier from session for state {State}", state);
                return null;
            }
        }

        /// <summary>
        /// Stores PKCE verifier and optional metadata in session.
        /// </summary>
        public void StorePKCEVerifierInSession(
            HttpContext httpContext,
            string state,
            string verifier,
            string? userId = null,
            string? redirectUri = null)
        {
            try
            {
                var verifierKey = $"pkce:{state}:verifier";
                httpContext.Session.SetString(verifierKey, verifier);

                if (!string.IsNullOrWhiteSpace(userId))
                {
                    var userIdKey = $"pkce:{state}:user_id";
                    httpContext.Session.SetString(userIdKey, userId);
                }

                if (!string.IsNullOrWhiteSpace(redirectUri))
                {
                    var redirectKey = $"pkce:{state}:redirect_uri";
                    httpContext.Session.SetString(redirectKey, redirectUri);
                }

                _logger.LogInformation("[AuthService] Stored PKCE verifier in session for state {State}", state);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AuthService] Failed to store PKCE verifier in session for state {State}", state);
                throw;
            }
        }

        /// <summary>
        /// Clears PKCE data from session after successful or failed flow.
        /// </summary>
        public void ClearPKCEDataFromSession(HttpContext httpContext, string state)
        {
            try
            {
                var keys = new[]
                {
                    $"pkce:{state}:verifier",
                    $"pkce:{state}:user_id",
                    $"pkce:{state}:redirect_uri"
                };

                foreach (var key in keys)
                {
                    httpContext.Session.Remove(key);
                }

                _logger.LogInformation("[AuthService] Cleared PKCE data from session for state {State}", state);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AuthService] Failed to clear PKCE data from session for state {State}", state);
            }
        }

        /// <summary>
        /// Generates a cryptographically secure code verifier for PKCE.
        /// </summary>
        private static string GenerateCodeVerifier()
        {
            var bytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Base64UrlEncode(bytes);
        }

        /// <summary>
        /// Generates a code challenge from a verifier.
        /// </summary>
        private static string GenerateCodeChallenge(string verifier)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.ASCII.GetBytes(verifier);
            var hash = sha256.ComputeHash(bytes);
            return Base64UrlEncode(hash);
        }

        /// <summary>
        /// Encodes bytes to base64url format (RFC 4648).
        /// </summary>
        private static string Base64UrlEncode(byte[] input)
        {
            return Convert.ToBase64String(input)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }
}
