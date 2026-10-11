using ForestChurches.Components.Configuration;
using ForestChurches.Components.Users;
using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ForestChurches.Components.Http
{
    /// <summary>
    /// Centralized service for ChurchSuite OAuth token lifecycle management.
    /// Handles token retrieval, validation, refresh, and storage.
    /// </summary>
    public interface IChurchSuiteTokenService
    {
        /// <summary>
        /// Gets a valid access token for the user, refreshing if needed.
        /// Returns null if user has no credentials or authorization is required.
        /// </summary>
        Task<string?> GetValidAccessTokenAsync(string userId);

        /// <summary>
        /// Attempts to get a valid credential for the user, refreshing if needed.
        /// </summary>
        Task<ChurchSuiteCredential?> GetValidCredentialAsync(string userId);

        /// <summary>
        /// Checks if user has a valid (non-expired) token without refresh.
        /// </summary>
        Task<bool> HasValidTokenAsync(string userId);

        /// <summary>
        /// Refreshes the token using the refresh_token.
        /// </summary>
        Task<bool> RefreshTokenAsync(ChurchSuiteCredential credential);

        /// <summary>
        /// Stores or updates credentials after successful OAuth callback.
        /// </summary>
        Task StoreCredentialsAsync(string userId, string accessToken, string refreshToken, int expiresIn, string? subdomain = null, string? clientId = null);

        /// <summary>
        /// Checks if user has ever authorized with ChurchSuite.
        /// </summary>
        Task<bool> UserHasAuthorizationAsync(string userId);
    }

    public class ChurchSuiteTokenService : IChurchSuiteTokenService
    {
        private readonly ForestChurchesContext _context;
        private readonly UserManager<ChurchAccount> _userManager;
        private readonly ILogger<ChurchSuiteTokenService> _logger;
        private readonly HttpClient _httpClient;
        private readonly IOptions<ChurchSuiteOAuthOptions> _options;

        public ChurchSuiteTokenService(
            ForestChurchesContext context,
            UserManager<ChurchAccount> userManager,
            ILogger<ChurchSuiteTokenService> logger,
            HttpClient httpClient,
            IOptions<ChurchSuiteOAuthOptions> options)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _httpClient = httpClient;
            _options = options;
        }

        /// <summary>
        /// Gets a valid access token for the user, refreshing if necessary.
        /// </summary>
        public async Task<string?> GetValidAccessTokenAsync(string userId)
        {
            var credential = await GetValidCredentialAsync(userId);
            return credential?.AccessToken;
        }

        /// <summary>
        /// Gets a valid credential for the user, refreshing if necessary.
        /// </summary>
        public async Task<ChurchSuiteCredential?> GetValidCredentialAsync(string userId)
        {
            try
            {
                // Retrieve the user's credential
                var credential = await _context.ChurchSuiteCredentials
                    .FirstOrDefaultAsync(c => c.UserId == userId && c.IsActive);

                if (credential == null)
                {
                    _logger.LogInformation("[TokenService] No active credential found for user {UserId}", userId);
                    return null;
                }

                // Check if token is expired
                if (credential.ExpiresAt <= DateTime.UtcNow)
                {
                    _logger.LogInformation("[TokenService] Token expired for user {UserId}. Attempting refresh.", userId);

                    // Attempt refresh
                    var refreshed = await RefreshTokenAsync(credential);
                    if (!refreshed)
                    {
                        _logger.LogWarning("[TokenService] Failed to refresh token for user {UserId}", userId);
                        return null;
                    }
                }

                return credential;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TokenService] Error retrieving valid credential for user {UserId}", userId);
                return null;
            }
        }

        /// <summary>
        /// Checks if user has a valid (non-expired) token without attempting refresh.
        /// </summary>
        public async Task<bool> HasValidTokenAsync(string userId)
        {
            try
            {
                var credential = await _context.ChurchSuiteCredentials
                    .FirstOrDefaultAsync(c => c.UserId == userId && c.IsActive);

                if (credential == null) return false;

                return credential.ExpiresAt > DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TokenService] Error checking token validity for user {UserId}", userId);
                return false;
            }
        }

        /// <summary>
        /// Refreshes the access token using the refresh_token.
        /// </summary>
        public async Task<bool> RefreshTokenAsync(ChurchSuiteCredential credential)
        {
            if (string.IsNullOrWhiteSpace(credential.RefreshToken))
            {
                _logger.LogWarning("[TokenService] No refresh token available for credential ID {CredentialId}", credential.Id);
                return false;
            }

            try
            {
                _logger.LogInformation("[TokenService] Attempting to refresh token for user {UserId}", credential.UserId);

                // Determine which ClientId to use
                string clientId = credential.ClientId;
                if (string.IsNullOrWhiteSpace(clientId))
                {
                    clientId = _options?.Value?.ClientId;
                }

                if (string.IsNullOrWhiteSpace(clientId))
                {
                    _logger.LogWarning("[TokenService] No ClientId available for token refresh");
                    return false;
                }

                // Prepare token refresh request
                var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://login.churchsuite.com/oauth2/token");
                var formData = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("grant_type", "refresh_token"),
                    new KeyValuePair<string, string>("refresh_token", credential.RefreshToken),
                    new KeyValuePair<string, string>("client_id", clientId)
                };

                var content = new FormUrlEncodedContent(formData);
                tokenRequest.Content = content;

                var response = await _httpClient.SendAsync(tokenRequest);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("[TokenService] Token refresh failed with status {StatusCode} for user {UserId}. Response: {ResponseBody}",
                        response.StatusCode, credential.UserId, errorBody);
                    return false;
                }

                var tokenResponse = await response.Content.ReadFromJsonAsync<TokenRefreshResponse>();
                if (tokenResponse?.AccessToken == null)
                {
                    _logger.LogWarning("[TokenService] Token refresh response missing access_token for user {UserId}", credential.UserId);
                    return false;
                }

                // Update credential with new tokens
                credential.AccessToken = tokenResponse.AccessToken;
                if (!string.IsNullOrWhiteSpace(tokenResponse.RefreshToken))
                {
                    credential.RefreshToken = tokenResponse.RefreshToken;
                }
                credential.ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn ?? 3600);
                credential.LastRefreshedAt = DateTime.UtcNow;

                _context.ChurchSuiteCredentials.Update(credential);
                await _context.SaveChangesAsync();

                _logger.LogInformation("[TokenService] Successfully refreshed token for user {UserId}", credential.UserId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TokenService] Exception during token refresh for user {UserId}", credential.UserId);
                return false;
            }
        }

        /// <summary>
        /// Stores or updates credentials after successful OAuth authorization.
        /// </summary>
        public async Task StoreCredentialsAsync(string userId, string accessToken, string refreshToken, int expiresIn, string? subdomain = null, string? clientId = null)
        {
            try
            {
                var expiresAt = DateTime.UtcNow.AddSeconds(expiresIn > 0 ? expiresIn : 3600);

                var credential = await _context.ChurchSuiteCredentials
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                if (credential != null)
                {
                    // Update existing credential
                    credential.AccessToken = accessToken;
                    credential.RefreshToken = refreshToken;
                    credential.ExpiresAt = expiresAt;
                    credential.UpdatedAt = DateTime.UtcNow;
                    credential.LastRefreshedAt = DateTime.UtcNow;
                    credential.IsActive = true;

                    if (!string.IsNullOrWhiteSpace(subdomain))
                    {
                        credential.Subdomain = subdomain;
                    }

                    if (!string.IsNullOrWhiteSpace(clientId))
                    {
                        credential.ClientId = clientId;
                    }

                    _context.ChurchSuiteCredentials.Update(credential);
                    _logger.LogInformation("[TokenService] Updated ChurchSuite credentials for user {UserId}", userId);
                }
                else
                {
                    // Create new credential
                    var newCredential = new ChurchSuiteCredential
                    {
                        UserId = userId,
                        Subdomain = subdomain ?? "unknown",
                        AccessToken = accessToken,
                        RefreshToken = refreshToken,
                        ExpiresAt = expiresAt,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        LastRefreshedAt = DateTime.UtcNow,
                        IsActive = true,
                        ClientId = clientId
                    };

                    await _context.ChurchSuiteCredentials.AddAsync(newCredential);
                    _logger.LogInformation("[TokenService] Created new ChurchSuite credentials for user {UserId}", userId);
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TokenService] Error storing credentials for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Checks if user has ever authorized with ChurchSuite.
        /// </summary>
        public async Task<bool> UserHasAuthorizationAsync(string userId)
        {
            try
            {
                return await _context.ChurchSuiteCredentials
                    .AnyAsync(c => c.UserId == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TokenService] Error checking user authorization for {UserId}", userId);
                return false;
            }
        }
    }

    /// <summary>
    /// DTO for token refresh response from ChurchSuite OAuth endpoint.
    /// </summary>
    public class TokenRefreshResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
        public int? ExpiresIn { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("token_type")]
        public string TokenType { get; set; }
    }
}
