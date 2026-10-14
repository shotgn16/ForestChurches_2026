using System.Net;
using System.Text.Json;
using ForestChurches.Components.Security;
using RestSharp;

namespace ForestChurches.Components.Email
{
    /// <summary>Works out the email app's base address. Shared by MailRepository and EmailServiceMonitor.</summary>
    public static class EmailApiEndpoint
    {
        public static string Resolve(IConfiguration configuration, Configuration.Configuration? configurationClass)
        {
            // Prefer an explicitly configured HTTPS base URL. Fail fast if it's present but not HTTPS.
            var baseUrl = configuration["EmailApi:BaseUrl"] ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                if (!baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("EmailApi:BaseUrl must start with 'https://'. Configure a secure HTTPS URL.");
                }
            }
            else if (configurationClass?.Client != null)
            {
                if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production")
                {
                    baseUrl = configurationClass.Client.GetSecret("app-email-url")?.Value?.Value ?? string.Empty;
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

            return baseUrl.TrimEnd('/') + "/";
        }
    }

    /// <summary>Result of asking the email app whether it's up.</summary>
    public class EmailServiceHealth
    {
        public bool Reachable { get; set; }
        public bool Authorised { get; set; }
        public int? HttpStatus { get; set; }
        public long ResponseMs { get; set; }
        public string? Error { get; set; }
        public DateTime CheckedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>False when the email app answered but is an older version without /Email/health.</summary>
        public bool SupportsStatus { get; set; }
        public int? QueueLength { get; set; }
        public DateTime? LastSendRunUtc { get; set; }
        public int? Sent { get; set; }
        public int? Failed { get; set; }
        public int? Waiting { get; set; }
    }

    /// <summary>
    /// Asks the email app (api.forestchurches.co.uk) about its health and about individual emails,
    /// and records the answers in <see cref="EmailDeliveryTracker"/>. Used by Admin → Email delivery.
    /// </summary>
    public class EmailServiceMonitor
    {
        private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

        private readonly IConfiguration _configuration;
        private readonly Configuration.Configuration _configurationClass;
        private readonly JwtTokenGenerator _jwt;
        private readonly EmailDeliveryTracker _tracker;
        private readonly ILogger<EmailServiceMonitor> _logger;

        public EmailServiceMonitor(IConfiguration configuration, Configuration.Configuration configurationClass, JwtTokenGenerator jwt,
            EmailDeliveryTracker tracker, ILogger<EmailServiceMonitor> logger)
        {
            _configuration = configuration;
            _configurationClass = configurationClass;
            _jwt = jwt;
            _tracker = tracker;
            _logger = logger;
        }

        public async Task<EmailServiceHealth> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            var health = new EmailServiceHealth();
            var timer = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var response = await SendAsync("Email/health", TimeSpan.FromSeconds(6), cancellationToken).ConfigureAwait(false);
                health.ResponseMs = timer.ElapsedMilliseconds;
                health.HttpStatus = (int)response.StatusCode == 0 ? null : (int)response.StatusCode;

                if (response.StatusCode == 0 || response.ErrorException is HttpRequestException)
                {
                    health.Error = response.ErrorMessage ?? "No response from the email app.";
                }
                else
                {
                    health.Reachable = true;
                    health.Authorised = response.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden);

                    if (response.IsSuccessful && !string.IsNullOrWhiteSpace(response.Content))
                    {
                        var body = JsonSerializer.Deserialize<HealthResponse>(response.Content, Json);
                        health.SupportsStatus = true;
                        health.QueueLength = body?.QueueLength;
                        health.LastSendRunUtc = body?.LastSendRunUtc;
                        health.Sent = body?.Sent;
                        health.Failed = body?.Failed;
                        health.Waiting = (body?.Queued ?? 0) + (body?.Retrying ?? 0);
                    }
                    else if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        health.Error = "The email app is running an older version without delivery checks. Deploy the latest EmailManagement-ForestChurches to enable them.";
                    }
                    else if (!health.Authorised)
                    {
                        health.Error = "The email app rejected this site's sign-in token (check the shared JWT settings).";
                    }
                    else if (!response.IsSuccessful)
                    {
                        health.Error = $"The email app answered {(int)response.StatusCode} {response.StatusDescription}.";
                    }
                }
            }
            catch (Exception ex)
            {
                health.ResponseMs = timer.ElapsedMilliseconds;
                health.Error = ex.Message;
            }

            if (health.Reachable && health.Authorised && health.Error == null)
            {
                _logger.LogInformation("Email app health check OK in {ResponseMs}ms: queue {QueueLength}, last send run {LastSendRunUtc}, sent {Sent}, failed {Failed}",
                    health.ResponseMs, health.QueueLength, health.LastSendRunUtc, health.Sent, health.Failed);
            }
            else
            {
                _logger.LogWarning("Email app health check problem: reachable {Reachable}, authorised {Authorised}, HTTP {HttpStatus}, error {Error}",
                    health.Reachable, health.Authorised, health.HttpStatus, health.Error);
            }

            return health;
        }

        /// <summary>Asks the email app what happened to one email and stores the answer on its record.</summary>
        public async Task<EmailDeliveryRecord?> CheckEmailAsync(string id, CancellationToken cancellationToken = default)
        {
            var record = _tracker.Get(id);
            if (record == null)
            {
                return null;
            }

            using var scope = _logger.BeginScope(new Dictionary<string, object> { ["EmailId"] = id });
            try
            {
                var response = await SendAsync($"Email/{Uri.EscapeDataString(id)}/status", TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);

                if (response.IsSuccessful && !string.IsNullOrWhiteSpace(response.Content))
                {
                    var body = JsonSerializer.Deserialize<StatusResponse>(response.Content, Json);
                    _tracker.Update(id, r =>
                    {
                        r.RemoteStatus = body?.Status ?? "Unknown";
                        r.RemoteAttempts = body?.Attempts;
                        r.RemoteSentUtc = body?.SentUtc;
                        r.RemoteError = body?.Error;
                        r.RemoteCheckedUtc = DateTime.UtcNow;
                    });
                    _logger.LogInformation("Email app reports email {EmailId} to {Recipient} is {RemoteStatus} (attempts {Attempts}, sent {SentUtc})",
                        id, record.Recipient, body?.Status, body?.Attempts, body?.SentUtc);
                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _tracker.Update(id, r =>
                    {
                        r.RemoteStatus = "Unknown";
                        r.RemoteError = "The email app has no record of this email. It may have restarted, or it's an older version without delivery checks.";
                        r.RemoteCheckedUtc = DateTime.UtcNow;
                    });
                    _logger.LogWarning("Email app has no record of email {EmailId} to {Recipient}", id, record.Recipient);
                }
                else
                {
                    var problem = response.ErrorMessage ?? $"HTTP {(int)response.StatusCode} {response.StatusDescription}";
                    _tracker.Update(id, r => { r.RemoteError = "Couldn't check: " + problem; r.RemoteCheckedUtc = DateTime.UtcNow; });
                    _logger.LogWarning("Couldn't check email {EmailId} with the email app: {Problem}", id, problem);
                }
            }
            catch (Exception ex)
            {
                _tracker.Update(id, r => { r.RemoteError = "Couldn't check: " + ex.Message; r.RemoteCheckedUtc = DateTime.UtcNow; });
                _logger.LogWarning(ex, "Couldn't check email {EmailId} with the email app", id);
            }

            return _tracker.Get(id);
        }

        /// <summary>Checks every recent email that was accepted but hasn't finished yet.</summary>
        public async Task<int> CheckUnfinishedAsync(int max = 50, CancellationToken cancellationToken = default)
        {
            var pending = _tracker.Recent(500)
                .Where(r => r.Stage == EmailHandOverStage.Accepted && !r.IsFinal)
                .Take(max)
                .ToList();

            foreach (var record in pending)
            {
                await CheckEmailAsync(record.Id, cancellationToken).ConfigureAwait(false);
            }

            return pending.Count;
        }

        private async Task<RestResponse> SendAsync(string path, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var baseUri = new Uri(EmailApiEndpoint.Resolve(_configuration, _configurationClass));
            var client = new RestClient(new RestClientOptions(baseUri) { FollowRedirects = false, Timeout = timeout });
            var request = new RestRequest(path, Method.Get);

            var token = _jwt.GenerateToken();
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.AddHeader("Authorization", $"Bearer {token}");
            }

            return await client.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        }

        private sealed class HealthResponse
        {
            public int? QueueLength { get; set; }
            public DateTime? LastSendRunUtc { get; set; }
            public int? Queued { get; set; }
            public int? Retrying { get; set; }
            public int? Sent { get; set; }
            public int? Failed { get; set; }
        }

        private sealed class StatusResponse
        {
            public string? Status { get; set; }
            public int? Attempts { get; set; }
            public DateTime? SentUtc { get; set; }
            public string? Error { get; set; }
        }
    }
}
