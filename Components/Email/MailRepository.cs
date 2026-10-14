
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
        private readonly EmailDeliveryTracker _tracker;

        public MailRepository(
            IConfiguration configuration,
            Configuration.Configuration configurationClass,
            IHttpClientFactory httpClientFactory,
            ILogger<MailRepository> logger,
            JwtTokenGenerator jwtTokenGenerator,
            EmailDeliveryTracker tracker)
        {
            jwt = jwtTokenGenerator;
            _tracker = tracker;
            _configuration = configuration;
            _configurationClass = configurationClass;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// Builds an EmailMessage payload and POSTs it to the email app (EmailManagement) using RestSharp.
        /// - EmailManagement expects POST /Email with EmailMessage JSON: { Id, To, Subject, Body, Template, Data }
        /// - Every email gets a tracking ID (EmailId). It's sent to the email app, written on every log line
        ///   about this email, and recorded in EmailDeliveryTracker so admins can check it under
        ///   Admin → Email delivery. A 2xx reply means the email app has received and queued the email.
        /// </summary>
        public async Task StartEmailAsync(string recipientEmail, Dictionary<string, string> userData, string subject, string templatePath)
        {
            var emailId = Guid.NewGuid().ToString("N");
            _tracker.Start(emailId, recipientEmail, subject, templatePath);

            using var logScope = _logger.BeginScope(new Dictionary<string, object> { ["EmailId"] = emailId });
            _logger.LogInformation("Email {EmailId} created: To={Recipient}, Subject={Subject}, Template={Template}", emailId, recipientEmail, subject, templatePath);

            try
            {
                var payload = new
                {
                    Id = emailId,
                    To = recipientEmail,
                    Subject = subject,
                    Body = string.Empty,
                    Template = templatePath ?? string.Empty,
                    Data = userData ?? new Dictionary<string, string>()
                };

                var baseUrl = EmailApiEndpoint.Resolve(_configuration, _configurationClass);
                var baseUri = new Uri(baseUrl);

                // Disable automatic redirects to avoid losing the Authorization header.
                var client = new RestClient(new RestClientOptions(baseUri)
                {
                    FollowRedirects = false
                });

                var request = new RestRequest("Email", Method.Post)
                    .AddJsonBody(payload);

                // Hard limit for the request (35s)
                request.Timeout = TimeSpan.FromSeconds(35);

                var token = jwt.GenerateToken();

                // Never log the token itself; only whether one exists.
                _logger.LogDebug("Email {EmailId}: token present={HasToken} length={Len}", emailId, !string.IsNullOrWhiteSpace(token), token?.Length ?? 0);

                if (!string.IsNullOrWhiteSpace(token))
                {
                    request.AddHeader("Authorization", $"Bearer {token}");
                }

                // Retry with growing timeouts for transient failures
                int maxRetries = 3;
                int[] timeoutSeconds = { 30, 40, 50 };
                Exception lastException = null;

                for (int attempt = 0; attempt < maxRetries; attempt++)
                {
                    _tracker.Update(emailId, r => r.Attempts = attempt + 1);
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        int timeoutSecs = timeoutSeconds[attempt];
                        _logger.LogInformation("Email {EmailId}: posting to email app {BaseUrl}, attempt {Attempt}/{MaxAttempts}, timeout {TimeoutSecs}s",
                            emailId, baseUri.Host, attempt + 1, maxRetries, timeoutSecs);

                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSecs));

                        var response = await client.ExecuteAsync(request, cts.Token).ConfigureAwait(false);

                        if (response == null)
                        {
                            throw new InvalidOperationException("Email API returned no response.");
                        }

                        _tracker.Update(emailId, r => r.LastHttpStatus = (int)response.StatusCode == 0 ? null : (int)response.StatusCode);

                        // Follow a single redirect by hand so the Authorization header is kept.
                        var statusCodeInt = (int)response.StatusCode;
                        var locationHeader = response.Headers?.FirstOrDefault(h => string.Equals(h.Name, "Location", StringComparison.OrdinalIgnoreCase))?.Value?.ToString();
                        if (statusCodeInt >= 300 && statusCodeInt < 400 && !string.IsNullOrWhiteSpace(locationHeader))
                        {
                            var redirectUri = Uri.TryCreate(locationHeader, UriKind.Absolute, out var abs) ? abs : new Uri(baseUri, locationHeader);
                            _logger.LogInformation("Email {EmailId}: email app redirected {Status} to {Location}; re-posting with the token", emailId, response.StatusCode, redirectUri);

                            var redirectClient = new RestClient(new RestClientOptions(redirectUri) { FollowRedirects = false });
                            var redirectRequest = new RestRequest().AddJsonBody(payload);
                            redirectRequest.Timeout = TimeSpan.FromSeconds(timeoutSecs);
                            if (!string.IsNullOrWhiteSpace(token))
                            {
                                redirectRequest.AddHeader("Authorization", $"Bearer {token}");
                            }

                            response = await redirectClient.ExecuteAsync(redirectRequest, cts.Token).ConfigureAwait(false)
                                ?? throw new InvalidOperationException("Email API returned no response on redirect.");
                            _tracker.Update(emailId, r => r.LastHttpStatus = (int)response.StatusCode == 0 ? null : (int)response.StatusCode);
                        }

                        if (!response.IsSuccessful)
                        {
                            var respBody = response.Content ?? "(none)";
                            if (respBody.Length > 500) respBody = respBody[..500] + "…";
                            var wwwAuthHeader = response.Headers?.FirstOrDefault(h => string.Equals(h.Name, "WWW-Authenticate", StringComparison.OrdinalIgnoreCase))?.Value?.ToString() ?? "(none)";

                            _logger.LogError("Email {EmailId}: email app did NOT accept the email. HTTP {Status} after {ElapsedMs}ms. Body: {Body}. WWW-Authenticate: {WwwAuth}. Transport error: {TransportError}",
                                emailId, (int)response.StatusCode, timer.ElapsedMilliseconds, respBody, wwwAuthHeader, response.ErrorMessage ?? "(none)");

                            if (response.ErrorException != null)
                            {
                                throw new InvalidOperationException("Failed to POST to Email API", response.ErrorException);
                            }

                            throw new InvalidOperationException($"Email API returned {(int)response.StatusCode} {response.StatusDescription}: {respBody}");
                        }

                        // Received. Newer email app versions reply with { id, status, receivedUtc, queueLength }.
                        string remoteStatus = null;
                        int? queueLength = null;
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(response.Content) && response.Content.TrimStart().StartsWith("{"))
                            {
                                using var doc = JsonDocument.Parse(response.Content);
                                if (doc.RootElement.TryGetProperty("status", out var st)) remoteStatus = st.GetString();
                                if (doc.RootElement.TryGetProperty("queueLength", out var ql) && ql.TryGetInt32(out var q)) queueLength = q;
                            }
                        }
                        catch (JsonException)
                        {
                            // Older email app versions reply with an empty 200; that's still a successful hand-over.
                        }

                        _tracker.Update(emailId, r =>
                        {
                            r.Stage = EmailHandOverStage.Accepted;
                            r.AcceptedUtc = DateTime.UtcNow;
                            r.LastError = null;
                            r.RemoteStatus = remoteStatus;
                            r.RemoteCheckedUtc = remoteStatus == null ? null : DateTime.UtcNow;
                        });

                        _logger.LogInformation("Email {EmailId} RECEIVED by email app: HTTP {Status} in {ElapsedMs}ms on attempt {Attempt}. To={Recipient}, Subject={Subject}, RemoteStatus={RemoteStatus}, QueueLength={QueueLength}",
                            emailId, (int)response.StatusCode, timer.ElapsedMilliseconds, attempt + 1, recipientEmail, subject, remoteStatus ?? "(not reported)", queueLength);
                        return;
                    }
                    catch (OperationCanceledException ocex)
                    {
                        lastException = ocex;
                        _tracker.Update(emailId, r => r.LastError = "Timed out waiting for the email app.");
                        _logger.LogWarning("Email {EmailId}: attempt {Attempt} timed out after {ElapsedMs}ms", emailId, attempt + 1, timer.ElapsedMilliseconds);
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        _tracker.Update(emailId, r => r.LastError = ex.Message);
                        _logger.LogWarning("Email {EmailId}: attempt {Attempt} failed after {ElapsedMs}ms: {Message}", emailId, attempt + 1, timer.ElapsedMilliseconds, ex.Message);
                    }
                }

                throw new InvalidOperationException($"Email API failed after {maxRetries} attempts", lastException);
            }
            catch (Exception ex)
            {
                _tracker.Update(emailId, r =>
                {
                    r.Stage = EmailHandOverStage.Failed;
                    r.LastError ??= ex.Message;
                });
                _logger.LogError(ex, "Email {EmailId} NOT RECEIVED by email app. To={Recipient}, Subject={Subject}. All attempts failed.", emailId, recipientEmail, subject);
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