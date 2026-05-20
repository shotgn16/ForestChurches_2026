
using System.Text.Json;
using ForestChurches.Components.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using RestSharp;
using System.Linq;

namespace ForestChurches.Components.Email
{
    // keeps the same public surface as before: StartEmailAsync + IEmailSender.SendEmailAsync
    public class MailRepository : IEmailSender, iEmail
    {
        private readonly IConfiguration _configuration;
        private readonly Configuration.Configuration _configurationClass;
        private readonly IHttpClientFactory _httpClientFactory; // kept for compatibility with DI registrations
        private readonly ILogger<MailRepository> _logger;

        private readonly JwtTokenGenerator jwt;

        public MailRepository(
            IConfiguration configuration,
            Configuration.Configuration configurationClass,
            IHttpClientFactory httpClientFactory,
            ILogger<MailRepository> logger,
            JwtTokenGenerator jwtTokenGenerator)
        {
            jwt = jwtTokenGenerator;
            _configuration = configuration;
            _configurationClass = configurationClass;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// Builds an EmailMessage payload and POSTs it to the remote EmailManagement API using RestSharp.
        /// Assumptions:
        /// - EmailManagement expects POST /Email with EmailMessage JSON:
        ///   { To, Subject, Body, Template, Data }
        /// - Template should be a path available to the receiving app (or use shared template names).
        /// </summary>
        public async Task StartEmailAsync(string recipientEmail, Dictionary<string, string> userData, string subject, string templatePath)
        {
            try
            {
                var payload = new
                {
                    To = recipientEmail,
                    Subject = subject,
                    Body = string.Empty,
                    Template = templatePath ?? string.Empty,
                    Data = userData ?? new Dictionary<string, string>()
                };

                // Prefer an explicitly configured HTTPS base URL. Fail fast if it's present but not HTTPS.
                var baseUrl = _configuration["EmailApi:BaseUrl"] ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(baseUrl))
                {
                    if (!baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("EmailApi:BaseUrl must start with 'https://'. Configure a secure HTTPS URL.");
                    }
                }
                else if (_configurationClass?.Client != null)
                {
                    if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production")
                    {
                        baseUrl = _configurationClass.Client.GetSecret("app-email-url")?.Value?.Value;
                    }
                    else if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
                    {
                        baseUrl = "https://localhost:7291";
                    }
                }

                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    throw new InvalidOperationException("EmailApi:BaseUrl is not configured.");
                }

                // Ensure trailing slash handled by RestSharp when baseUri provided
                var baseUri = new Uri(baseUrl.TrimEnd('/') + "/");

                // Create RestSharp client and request. Disable automatic redirects to avoid losing Authorization header.
                var client = new RestClient(new RestClientOptions(baseUri)
                {
                    FollowRedirects = false
                });

                var request = new RestRequest("Email", Method.Post)
                    .AddJsonBody(payload);

                // Hard limit for the request in milliseconds (35s)
                request.Timeout = TimeSpan.FromSeconds(35);

                // Bearer token: prefer config, fall back to KeyVault via configurationClass
                var token = jwt.GenerateToken();

                // debug: do not log token contents; only presence/length
                _logger.LogInformation("Generated token present={HasToken} length={Len} for Recipient={Recipient}", !string.IsNullOrWhiteSpace(token), token?.Length ?? 0, recipientEmail);

                if (!string.IsNullOrWhiteSpace(token))
                {
                    request.AddHeader("Authorization", $"Bearer {token}");
                }

                // Create a cancellation token for 30 seconds (matches previous behaviour)
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

                var response = await client.ExecuteAsync(request, cts.Token).ConfigureAwait(false);

                if (response == null)
                {
                    _logger.LogError("Email API returned no response (null). Recipient: {Recipient}", recipientEmail);
                    throw new InvalidOperationException("Email API returned no response.");
                }

                if (!response.IsSuccessful)
                {
                    var respBody = response.Content ?? "(none)";

                    // Log WWW-Authenticate challenges (if any) and redirect target (Location) to diagnose 401/403 or redirect behaviour
                    var wwwAuthHeader = response.Headers.FirstOrDefault(h => string.Equals(h.Name, "WWW-Authenticate", StringComparison.OrdinalIgnoreCase))?.Value?.ToString() ?? "(none)";
                    var locationHeader = response.Headers.FirstOrDefault(h => string.Equals(h.Name, "Location", StringComparison.OrdinalIgnoreCase))?.Value?.ToString() ?? "(none)";

                    _logger.LogError("Email API returned {Status}. Body: {Body}. WWW-Authenticate: {WwwAuth}. Location: {Location}", response.StatusCode, respBody, wwwAuthHeader, locationHeader);

                    // If the server returned a redirect (3xx) and provided a Location, handle a single
                    // manual redirect by re-sending the POST and preserving the Authorization header.
                    var statusCodeInt = (int)response.StatusCode;
                    if (statusCodeInt >= 300 && statusCodeInt < 400 && !string.Equals(locationHeader, "(none)", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            // Resolve relative Location against the original baseUri if needed
                            var redirectUri = Uri.TryCreate(locationHeader, UriKind.Absolute, out var abs) ? abs : new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), locationHeader);

                            _logger.LogInformation("Email API returned redirect {Status} to {Location}. Attempting manual POST to preserve Authorization header.", response.StatusCode, redirectUri);

                            var redirectClient = new RestClient(new RestClientOptions(redirectUri)
                            {
                                FollowRedirects = false
                            });

                            var redirectRequest = new RestRequest()
                                .AddJsonBody(payload);

                            redirectRequest.Timeout = TimeSpan.FromSeconds(35);

                            if (!string.IsNullOrWhiteSpace(token))
                            {
                                redirectRequest.AddHeader("Authorization", $"Bearer {token}");
                            }

                            var redirectResponse = await redirectClient.ExecuteAsync(redirectRequest, cts.Token).ConfigureAwait(false);

                            if (redirectResponse == null)
                            {
                                _logger.LogError("Email API redirect returned no response (null). Recipient: {Recipient}", recipientEmail);
                                throw new InvalidOperationException("Email API returned no response on redirect.");
                            }

                            if (!redirectResponse.IsSuccessful)
                            {
                                var rBody = redirectResponse.Content ?? "(none)";
                                var rWwwAuth = redirectResponse.Headers.FirstOrDefault(h => string.Equals(h.Name, "WWW-Authenticate", StringComparison.OrdinalIgnoreCase))?.Value?.ToString() ?? "(none)";
                                var rLocation = redirectResponse.Headers.FirstOrDefault(h => string.Equals(h.Name, "Location", StringComparison.OrdinalIgnoreCase))?.Value?.ToString() ?? "(none)";

                                _logger.LogError("Email API redirect attempt returned {Status}. Body: {Body}. WWW-Authenticate: {WwwAuth}. Location: {Location}", redirectResponse.StatusCode, rBody, rWwwAuth, rLocation);

                                if (redirectResponse.ErrorException != null)
                                {
                                    throw new InvalidOperationException("Failed to POST to Email API on redirect", redirectResponse.ErrorException);
                                }

                                throw new InvalidOperationException($"Email API redirect returned {(int)redirectResponse.StatusCode} {redirectResponse.StatusDescription}: {rBody}");
                            }

                            _logger.LogInformation("Enqueued email via Email API (redirect): To={To}, Subject={Subject}", recipientEmail, subject);
                            return; // success via redirect
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to follow redirect for Email API POST to {Location}", locationHeader);
                            throw;
                        }
                    }

                    // Preserve original error if available
                    if (response.ErrorException != null)
                    {
                        throw new InvalidOperationException("Failed to POST to Email API", response.ErrorException);
                    }

                    throw new InvalidOperationException($"Email API returned {(int)response.StatusCode} {response.StatusDescription}: {respBody}");
                }
                else
                {
                    _logger.LogInformation("Enqueued email via Email API: To={To}, Subject={Subject}", recipientEmail, subject);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogError("Email API timed out (Server did not respond in time). Recipient: {Recipient}", recipientEmail);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to POST email to Email API for {Recipient}", recipientEmail);
                throw;
            }
        }

        // IEmailSender implementation (called by Identity, etc.)
        // This wraps StartEmailAsync: keep behaviour consistent with remote approach.
        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // send htmlMessage as a data token in case remote templates expect a body placeholder
            var data = new Dictionary<string, string>
            {
                { "{body}", htmlMessage }
            };

            // Fire-and-forget semantics preserved by the original code; this awaits to bubble errors.
            await StartEmailAsync(email, data, subject, null).ConfigureAwait(false);
        }
    }
}