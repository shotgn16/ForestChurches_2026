using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using ForestChurches.Models;
using ForestChurches.Data;
using ForestChurches.Components.Http;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;

public interface IChurchSuiteService
{
    Task<List<EventsModel>> GetCalendarEventsAsync(string accountId, string apiKey);
    Task<ChurchSuiteProfileResponse?> GetProfileAsync(string accountId, string apiKey);
    Task<ChurchSuiteProfileResponse?> GetProfileFromOAuthAsync(string accessToken);
    Task<ChurchSuiteProfileResponse?> GetProfileFromOAuthAsync(ClaimsPrincipal user);
    Task<List<EventsModel>> GetCalendarEventsFromOAuthAsync(string accessToken, string subdomain);
    Task<ChurchSuiteCredential?> GetActiveCredentialForUserAsync(string userId);
    EventsModel MapChurchSuiteEventToEventsModel(ChurchSuiteEventDto churchSuiteEvent);
}

public class ChurchSuiteService : IChurchSuiteService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ChurchSuiteService> _logger;
    private readonly ForestChurchesContext _context;
    private readonly IConfiguration _configuration;
    private readonly IChurchSuiteTokenService _tokenService;

    public ChurchSuiteService(
        HttpClient httpClient,
        ILogger<ChurchSuiteService> logger,
        ForestChurchesContext context,
        IConfiguration configuration,
        IChurchSuiteTokenService tokenService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _context = context;
        _configuration = configuration;
        _tokenService = tokenService;
    }

    public async Task<List<EventsModel>> GetCalendarEventsAsync(string accountId, string apiKey)
    {
        try
        {
            if (string.IsNullOrEmpty(accountId) || string.IsNullOrEmpty(apiKey)) return new List<EventsModel>();

            var request = new HttpRequestMessage(HttpMethod.Get, $"https://{accountId}.churchsuite.com/api/v1/calendar/export");
            request.Headers.Add("X-Account", accountId);
            request.Headers.Add("X-Authorization", apiKey);

            var response = await _httpClient.SendAsync(request).ConfigureAwait(true);
            response.EnsureSuccessStatusCode();

            var events = await response.Content.ReadFromJsonAsync<List<ChurchSuiteEventDto>>().ConfigureAwait(true);
            return events?.Select(MapChurchSuiteEventToEventsModel).ToList() ?? new List<EventsModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching events for {AccountId}", accountId);
            throw;
        }
    }

    public async Task<ChurchSuiteProfileResponse?> GetProfileAsync(string accountId, string apiKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.churchsuite.com/v2/account");

        if (!string.IsNullOrEmpty(apiKey) && apiKey.Length > 20)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        else
        {
            request.Headers.Add("X-Account", accountId);
            request.Headers.Add("X-Authorization", apiKey);
        }

        var response = await _httpClient.SendAsync(request).ConfigureAwait(true);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<ChurchSuiteProfileResponse>().ConfigureAwait(true) : null;
    }

    public async Task<ChurchSuiteProfileResponse?> GetProfileFromOAuthAsync(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken)) return null;
        var (success, data) = await TryEndpointAsync("https://api.churchsuite.com/v2/account", accessToken).ConfigureAwait(true);
        return success ? data : null;
    }

    private async Task<(bool success, ChurchSuiteProfileResponse? data)> TryEndpointAsync(string url, string accessToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(request).ConfigureAwait(true);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<ChurchSuiteProfileResponse>().ConfigureAwait(true);
                return (data?.Account != null, data);
            }
            return (false, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Request to {Url} failed", url);
            return (false, null);
        }
    }

    public async Task<ChurchSuiteProfileResponse?> GetProfileFromOAuthAsync(ClaimsPrincipal user)
    {
        try
        {
            // Attempt to find a stored credential for the currently authenticated local user
            var localUserId = user?.FindFirstValue(ClaimTypes.NameIdentifier) ?? user?.FindFirstValue("sub");
            if (!string.IsNullOrWhiteSpace(localUserId))
            {
                // Use centralized token service for automatic refresh
                var accessToken = await _tokenService.GetValidAccessTokenAsync(localUserId);
                if (!string.IsNullOrWhiteSpace(accessToken))
                {
                    var (ok, data) = await TryEndpointAsync("https://api.churchsuite.com/v2/account", accessToken).ConfigureAwait(true);
                    return ok ? data : null;
                }
            }

            // Fallback: try to read token from the provided principal (session flow preserved token)
            var token = user?.FindFirstValue("access_token") ?? user?.FindFirstValue("urn:oauth:access_token");
            if (!string.IsNullOrWhiteSpace(token))
            {
                return await GetProfileFromOAuthAsync(token).ConfigureAwait(true);
            }

            _logger.LogWarning("GetProfileFromOAuthAsync: No valid ChurchSuite token found for user");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetProfileFromOAuthAsync: Error fetching profile for authenticated user");
            return null;
        }
    }

    private async Task<TokenResponse?> RefreshTokenAsync(string refreshToken)
    {
        try
        {
            var tokenEndpoint = _configuration["ChurchSuite:OAuth:TokenEndpoint"] ?? "https://login.churchsuite.com/oauth2/token";
            var clientId = _configuration["ChurchSuite:OAuth:ClientId"] ?? Environment.GetEnvironmentVariable("CHURCHSUITE_OAUTH_CLIENT_ID");
            var clientSecret = _configuration["ChurchSuite:OAuth:ClientSecret"] ?? Environment.GetEnvironmentVariable("CHURCHSUITE_OAUTH_CLIENT_SECRET");

            // ChurchSuite token endpoint expects HTTP Basic auth for client credentials when client secret is present.
            using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint);

            var parameters = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", refreshToken),
            };

            request.Content = new FormUrlEncodedContent(parameters);

            // If the provided refresh token was obtained and stored alongside a credential that includes a client id, prefer that client id
            // The caller can pass the client id as part of the refresh flow by setting an ambient configuration or by updating this method signature.
            // For backward compatibility, fall back to the configured client id and secret.

            if (!string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret))
            {
                // Use HTTP Basic auth with client_id:client_secret
                var basicToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicToken);
            }
            else if (!string.IsNullOrWhiteSpace(clientId))
            {
                // If client secret missing (PKCE/public-client), include client_id in body
                // Note: some providers require client_id in body when no Basic auth present.
                request.Content = new FormUrlEncodedContent(parameters.Concat(new[] { new KeyValuePair<string, string>("client_id", clientId) }));
            }

            var response = await _httpClient.SendAsync(request).ConfigureAwait(true);
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(true);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Refresh token request failed: {Status} - {Body}", response.StatusCode, body);
                return null;
            }

            var tokenData = JsonSerializer.Deserialize<TokenResponse>(body, new JsonSerializerOptions{PropertyNameCaseInsensitive=true});
            return tokenData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh ChurchSuite token");
            return null;
        }
    }

    public async Task<List<EventsModel>> GetCalendarEventsFromOAuthAsync(string accessToken, string subdomain)
    {
        try
        {
            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(subdomain)) 
                return new List<EventsModel>();

            _logger.LogInformation("Fetching calendar events via OAuth for subdomain: {Subdomain}", subdomain);

            // Use the OAuth v2 endpoint with Bearer token to fetch ALL events
            var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.churchsuite.com/v2/calendar/events");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(request).ConfigureAwait(true);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch events via OAuth: {Status} {Reason}", response.StatusCode, response.ReasonPhrase);
                return new List<EventsModel>();
            }

            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(true);
            _logger.LogInformation("OAuth calendar response length: {Length} bytes", responseBody.Length);
            _logger.LogInformation("Raw response (first 500 chars): {Response}", 
                responseBody.Length > 500 ? responseBody.Substring(0, 500) : responseBody);

            // Diagnostic: attempt to enumerate & log key fields from the returned event objects so we can
            // detect recurrence properties / field names returned by ChurchSuite (starts_at, datetime_start, rrule, recurrence etc.).
            try
            {
                using var docDiag = JsonDocument.Parse(responseBody);
                var rootDiag = docDiag.RootElement;
                JsonElement eventsArrayDiag;

                if (rootDiag.ValueKind == JsonValueKind.Array)
                {
                    eventsArrayDiag = rootDiag;
                }
                else if (rootDiag.TryGetProperty("events", out var tmp))
                {
                    eventsArrayDiag = tmp;
                }
                else if (rootDiag.TryGetProperty("data", out var tmp2))
                {
                    eventsArrayDiag = tmp2;
                }
                else
                {
                    eventsArrayDiag = default;
                }

                if (eventsArrayDiag.ValueKind == JsonValueKind.Array)
                {
                    int sample = 0;
                    foreach (var ev in eventsArrayDiag.EnumerateArray())
                    {
                        try
                        {
                            if (sample++ >= 25) break;
                            var id = ev.TryGetProperty("id", out var idProp) ? idProp.ToString() : string.Empty;
                            var name = ev.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : string.Empty;
                            var starts = ev.TryGetProperty("starts_at", out var sProp) ? sProp.GetString() : (ev.TryGetProperty("datetime_start", out var dsProp) ? dsProp.GetString() : string.Empty);
                            var rrule = ev.TryGetProperty("rrule", out var rProp) ? rProp.GetString() : (ev.TryGetProperty("recurrence", out var recProp) ? recProp.GetRawText() : string.Empty);
                            var raw = ev.GetRawText();
                            var rawPreview = raw.Length > 400 ? raw.Substring(0, 400) : raw;

                            _logger.LogInformation("[ChurchSuite-DIAG] Event #{Index} id={Id} name={Name} starts_at={Starts} rrule={RRule} rawPreview={Raw}", sample, id, name, starts, rrule, rawPreview);
                        }
                        catch (Exception innerEx)
                        {
                            _logger.LogDebug(innerEx, "Failed to log diagnostic for event sample");
                        }
                    }
                }
                else
                {
                    _logger.LogInformation("[ChurchSuite-DIAG] Response did not contain an events/data array to sample.");
                }
            }
            catch (Exception exDiag)
            {
                _logger.LogDebug(exDiag, "Failed to parse response body for diagnostics");
            }

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            // Try parsing as direct list first
            try
            {
                var directList = JsonSerializer.Deserialize<List<ChurchSuiteEventDto>>(responseBody, options);
                if (directList != null && directList.Count > 0)
                {
                    _logger.LogInformation("Successfully parsed direct event list: {Count} events", directList.Count);
                    return MapEventsList(directList);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to parse as direct list, trying wrapped format...");
            }

            // Try parsing as wrapped response
            try
            {
                using var jsonDoc = JsonDocument.Parse(responseBody);
                var root = jsonDoc.RootElement;

                // Try "events" property
                if (root.TryGetProperty("events", out var eventsArray))
                {
                    var events = JsonSerializer.Deserialize<List<ChurchSuiteEventDto>>(eventsArray.GetRawText(), options);
                    _logger.LogInformation("Parsed wrapped response with 'events' property: {Count} events", events?.Count ?? 0);
                    return events != null ? MapEventsList(events) : new List<EventsModel>();
                }

                // Try root as array
                if (root.ValueKind == JsonValueKind.Array)
                {
                    var events = JsonSerializer.Deserialize<List<ChurchSuiteEventDto>>(responseBody, options);
                    _logger.LogInformation("Parsed root array: {Count} events", events?.Count ?? 0);
                    return events != null ? MapEventsList(events) : new List<EventsModel>();
                }

                _logger.LogWarning("Unexpected response structure. Root type: {Type}, Properties: {Properties}", 
                    root.ValueKind, string.Join(", ", root.EnumerateObject().Select(p => p.Name)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse wrapped response format");
            }

            _logger.LogWarning("Could not parse any events from response");
            return new List<EventsModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching calendar events via OAuth");
            return new List<EventsModel>();
        }
    }

    /// <summary>
    /// Maps a list of ChurchSuite event DTOs to EventsModel list.
    /// </summary>
    private List<EventsModel> MapEventsList(List<ChurchSuiteEventDto> churchSuiteEvents)
    {
        var mappedEvents = churchSuiteEvents
            .Select(e => {
                try
                {
                    return MapChurchSuiteEventToEventsModel(e);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to map event {EventId}: {EventName}", e.Id, e.Name);
                    return null;
                }
            })
            .Where(e => e != null)
            .ToList();

        _logger.LogInformation("Successfully mapped {MappedCount} of {TotalCount} events", mappedEvents.Count, churchSuiteEvents.Count);
        return mappedEvents;
    }

    public EventsModel MapChurchSuiteEventToEventsModel(ChurchSuiteEventDto e)
    {
        // Handle both v1 and v2 API date formats
        string startDateStr = !string.IsNullOrWhiteSpace(e.StartsAt) ? e.StartsAt : e.DateTimeStart;
        string endDateStr = !string.IsNullOrWhiteSpace(e.EndsAt) ? e.EndsAt : e.DateTimeEnd;

        // Parse dates safely with fallbacks
        var startParsed = DateTime.TryParse(startDateStr, out var start);
        var endParsed = DateTime.TryParse(endDateStr, out var end);

        // Default to today if parsing fails
        if (!startParsed)
        {
            start = DateTime.UtcNow;
            _logger.LogWarning("Failed to parse start date for event {EventName}: {RawDate}", e.Name, startDateStr);
        }

        if (!endParsed)
        {
            end = start.AddHours(1);
            _logger.LogWarning("Failed to parse end date for event {EventName}: {RawDate}", e.Name, endDateStr);
        }

        // Ensure end time is after start time
        if (end < start)
        {
            end = start.AddHours(1);
            _logger.LogWarning("End time before start time for event {EventName}, adjusting end time", e.Name);
        }

        // Extract address from LocationDto object
        string address = ExtractLocationString(e.Location);
        if (string.IsNullOrWhiteSpace(address))
        {
            address = !string.IsNullOrWhiteSpace(e.Venue) ? e.Venue : "TBA";
        }

        return new EventsModel
        {
            // Use ChurchSuite event ID as the primary identifier for matching/updates
            ID = e.Id?.ToString() ?? Guid.NewGuid().ToString(),
            Name = !string.IsNullOrWhiteSpace(e.Name) ? e.Name : "Untitled Event",
            Description = !string.IsNullOrWhiteSpace(e.Description) ? e.Description : "No description available",
            Address = address,
            Link = !string.IsNullOrWhiteSpace(e.Url) ? e.Url : string.Empty,
            Date = DateOnly.FromDateTime(start),
            StartTime = TimeOnly.FromDateTime(start),
            EndTime = TimeOnly.FromDateTime(end),
            // Initialize ImageArray to empty byte array (required by DB schema)
            ImageArray = new byte[0],
            // ImageBlobName will be populated during blob storage upload in sync handler
            ImageBlobName = string.Empty,
            // These will be populated by the sync handler, not from ChurchSuite
            User = string.Empty,
            Church = string.Empty,
            Repeats = string.Empty
        };
    }

    /// <summary>
    /// Extracts a string representation from the location object (which can be a string, LocationDto, or complex object).
    /// </summary>
    private string ExtractLocationString(LocationDto? location)
    {
        if (location == null)
            return string.Empty;

        // Prefer name first, then address, then return empty
        if (!string.IsNullOrWhiteSpace(location.Name))
            return location.Name;

        if (!string.IsNullOrWhiteSpace(location.Address))
            return location.Address;

        return string.Empty;
    }

    public async Task<ChurchSuiteCredential?> GetActiveCredentialForUserAsync(string userId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId))
                return null;

            var credential = await _context.ChurchSuiteCredentials
                .FirstOrDefaultAsync(c => c.UserId == userId && c.IsActive)
                .ConfigureAwait(true);

            return credential;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking ChurchSuite credentials for user {UserId}", userId);
            return null;
        }
    }

    private class TokenResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("access_token")]
        public string AccessToken { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    private class CalendarEventsResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("events")]
        public List<ChurchSuiteEventDto>? Events { get; set; }
    }
}
