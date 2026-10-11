using System;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IO;
using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ForestChurches.Components.Users;
using ForestChurches.Components.Configuration;
using ForestChurches.Components.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;

namespace ForestChurches.Components.Http
{
    /// <summary>
    /// Service for syncing ChurchSuite calendar events with ForestChurches event database.
    /// Handles token refresh, event fetching, mapping, and per-user credential management.
    /// </summary>
    public interface IChurchSuiteEventSyncService
    {
        /// <summary>
        /// Syncs events from ChurchSuite for the authenticated user.
        /// </summary>
        /// <param name="userId">The ID of the authenticated user.</param>
        /// <returns>Result containing status, imported count, and error messages if any.</returns>
        Task<SyncEventResult> SyncEventsAsync(string userId);
    }

    public class SyncEventResult
    {
        public bool Success { get; set; }
        public int ImportedCount { get; set; }
        public string Message { get; set; }
        public string ErrorMessage { get; set; }
        public List<string> EventNames { get; set; } = new List<string>();
    }

    public class ChurchSuiteEventSyncService : IChurchSuiteEventSyncService
    {
        private readonly ForestChurchesContext _context;
        private readonly IChurchSuiteService _churchSuiteService;
        private readonly IChurchSuiteTokenService _tokenService;
        private readonly UserManager<ChurchAccount> _userManager;
        private readonly ILogger<ChurchSuiteEventSyncService> _logger;
        private readonly HttpClient _httpClient;
        private readonly IBlobStorageService _blobStorageService;

        public ChurchSuiteEventSyncService(
            ForestChurchesContext context,
            IChurchSuiteService churchSuiteService,
            IChurchSuiteTokenService tokenService,
            UserManager<ChurchAccount> userManager,
            ILogger<ChurchSuiteEventSyncService> logger,
            HttpClient httpClient,
            IBlobStorageService blobStorageService)
        {
            _context = context;
            _churchSuiteService = churchSuiteService;
            _tokenService = tokenService;
            _userManager = userManager;
            _logger = logger;
            _httpClient = httpClient;
            _blobStorageService = blobStorageService;
        }

        /// <summary>
        /// Syncs events from ChurchSuite for the given user ID.
        /// Uses centralized token service for automatic token refresh.
        /// </summary>
        public async Task<SyncEventResult> SyncEventsAsync(string userId)
        {
            try
            {
                _logger.LogInformation("[EventSync] Starting sync for user {UserId}", userId);

                // Step 1: Verify user exists
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("[EventSync] User {UserId} not found", userId);
                    return new SyncEventResult
                    {
                        Success = false,
                        ErrorMessage = "User not found."
                    };
                }

                // Step 2: Get or refresh ChurchSuite credentials for this user using centralized token service
                var credentials = await _tokenService.GetValidCredentialAsync(userId);
                if (credentials == null)
                {
                    _logger.LogWarning("[EventSync] No ChurchSuite credentials found for user {UserId}. User must authorize with ChurchSuite first.", userId);
                    return new SyncEventResult
                    {
                        Success = false,
                        ErrorMessage = "No ChurchSuite credentials found. Please authorize with ChurchSuite first.",
                        Message = "AUTHORIZE_REQUIRED"
                    };
                }

                _logger.LogInformation("[EventSync] Retrieved credentials for user {UserId} with subdomain {Subdomain}", userId, credentials.Subdomain);

                // Step 3: Fetch ChurchSuite profile to verify credentials are valid
                var profile = await _churchSuiteService.GetProfileFromOAuthAsync(credentials.AccessToken);
                if (profile?.Account == null)
                {
                    _logger.LogWarning("[EventSync] Failed to fetch profile for user {UserId}. Credentials may be invalid.", userId);
                    // Attempt token refresh using the token service
                    var refreshed = await _tokenService.RefreshTokenAsync(credentials);
                    if (!refreshed)
                    {
                        return new SyncEventResult
                        {
                            Success = false,
                            ErrorMessage = "ChurchSuite credentials are invalid or expired. Please re-authorize.",
                            Message = "AUTHORIZE_REQUIRED"
                        };
                    }
                    // Retry with refreshed token
                    profile = await _churchSuiteService.GetProfileFromOAuthAsync(credentials.AccessToken);
                    if (profile?.Account == null)
                    {
                        return new SyncEventResult
                        {
                            Success = false,
                            ErrorMessage = "Failed to fetch ChurchSuite profile even after token refresh."
                        };
                    }
                }

                _logger.LogInformation("[EventSync] Profile fetch successful for user {UserId}", userId);

                // Step 4: Fetch events from ChurchSuite with date range filter
                var now = DateTime.UtcNow;
                var until = now.AddDays(7);

                _logger.LogInformation("[EventSync] Requesting events with date range: {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}", now, until);
                var churchSuiteEvents = await FetchChurchSuiteEventsAsync(credentials.AccessToken, credentials.Subdomain, now, until);
                _logger.LogInformation("[EventSync] Fetched {EventCount} events from ChurchSuite for user {UserId}", churchSuiteEvents.Count, userId);

                // Filter events to those occurring in the next 7 days (calendar window) as additional safety check
                var filteredEvents = churchSuiteEvents.Where(e =>
                {
                    var startStr = !string.IsNullOrWhiteSpace(e.StartsAt) ? e.StartsAt : e.DateTimeStart;
                    if (string.IsNullOrWhiteSpace(startStr)) return false;
                    if (!DateTime.TryParse(startStr, out var start)) return false;
                    var startUtc = start.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(start, DateTimeKind.Utc) : start.ToUniversalTime();
                    return startUtc >= now && startUtc <= until;
                }).ToList();

                _logger.LogInformation("[EventSync] Filtered events to next 7 days: {FilteredCount} of {TotalCount}", filteredEvents.Count, churchSuiteEvents.Count);

                if (filteredEvents.Count == 0)
                {
                    return new SyncEventResult
                    {
                        Success = true,
                        ImportedCount = 0,
                        Message = "No upcoming events in ChurchSuite calendar for the next 7 days."
                    };
                }

                // Step 5: Get user's church information
                var church = await _context.ChurchInformation
                    .FirstOrDefaultAsync(c => c.ChurchAccountId == userId);

                if (church == null)
                {
                    _logger.LogWarning("[EventSync] No church information found for user {UserId}", userId);
                    return new SyncEventResult
                    {
                        Success = false,
                        ErrorMessage = "No church information found for this user."
                    };
                }

                // Step 6: Map and upsert events
                var importedCount = 0;
                var importedNames = new List<string>();

                foreach (var churchSuiteEvent in filteredEvents)
                {
                    try
                    {
                        // Map ChurchSuite event to EventsModel
                        var mappedEvent = _churchSuiteService.MapChurchSuiteEventToEventsModel(churchSuiteEvent);

                        // Set church-specific fields
                        mappedEvent.Church = church.Name;
                        mappedEvent.User = user.Email;

                        // Attempt to download and upload image from ChurchSuite (image URL from ImageDto)
                        try
                        {
                            if (string.IsNullOrWhiteSpace(mappedEvent.ImageBlobName))
                            {
                                // Extract image URL from ImageDto (prefer medium size, then small, then thumbnail)
                                var imageUrl = ExtractImageUrl(churchSuiteEvent.Image);
                                if (!string.IsNullOrWhiteSpace(imageUrl))
                                {
                                    try
                                    {
                                        var imgResp = await _httpClient.GetAsync(imageUrl);
                                        if (imgResp.IsSuccessStatusCode)
                                        {
                                            var bytes = await imgResp.Content.ReadAsByteArrayAsync();
                                            var contentType = imgResp.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";

                                            // Determine file extension
                                            string ext = Path.GetExtension(new Uri(imageUrl).AbsolutePath);
                                            if (string.IsNullOrEmpty(ext))
                                            {
                                                if (contentType == "image/png") ext = ".png";
                                                else if (contentType == "image/jpeg" || contentType == "image/jpg") ext = ".jpg";
                                                else if (contentType == "image/gif") ext = ".gif";
                                                else ext = ".img";
                                            }

                                            var blobName = $"images/{Guid.NewGuid()}{ext}";
                                            await _blobStorageService.UploadAsync(bytes, blobName, contentType);
                                            mappedEvent.ImageBlobName = blobName;
                                            _logger.LogDebug("[EventSync] Uploaded image for event {EventName} to blob {BlobName}", mappedEvent.Name, blobName);
                                        }
                                    }
                                    catch (Exception imgEx)
                                    {
                                        _logger.LogWarning(imgEx, "[EventSync] Failed to download/upload image for event {EventName}", mappedEvent.Name);
                                    }
                                }
                            }
                        }
                        catch (Exception) { /* non-fatal image handling */ }

                        // First, try to match by the remote ChurchSuite event ID (mapped into EventsModel.ID).
                        // This ensures idempotent updates when the external ID is already present locally.
                        var existingById = await _context.Events
                            .FirstOrDefaultAsync(e => e.ID == mappedEvent.ID);

                        if (existingById != null)
                        {
                            // Update the record found by ID
                            existingById.Name = mappedEvent.Name;
                            existingById.Description = mappedEvent.Description;
                            existingById.Address = mappedEvent.Address;
                            existingById.Link = mappedEvent.Link;
                            existingById.Date = mappedEvent.Date;
                            existingById.StartTime = mappedEvent.StartTime;
                            existingById.EndTime = mappedEvent.EndTime;
                            existingById.Repeats = mappedEvent.Repeats;

                            _context.Events.Update(existingById);
                            _logger.LogDebug("[EventSync] Updated existing event by ID: {EventName}", mappedEvent.Name);
                        }
                        else
                        {
                            // Fallback: try to match by name+date+church to avoid duplicates when remote ID is not present or changed
                            var existingEvent = await _context.Events
                                .FirstOrDefaultAsync(e => e.Name == mappedEvent.Name && 
                                    e.Date == mappedEvent.Date && 
                                    e.Church == church.Name);

                            if (existingEvent != null)
                            {
                                // Update existing event but keep its local ID unchanged
                                existingEvent.Name = mappedEvent.Name;
                                existingEvent.Description = mappedEvent.Description;
                                existingEvent.Address = mappedEvent.Address;
                                existingEvent.Link = mappedEvent.Link;
                                existingEvent.Date = mappedEvent.Date;
                                existingEvent.StartTime = mappedEvent.StartTime;
                                existingEvent.EndTime = mappedEvent.EndTime;
                                existingEvent.Repeats = mappedEvent.Repeats;

                                _context.Events.Update(existingEvent);
                                _logger.LogDebug("[EventSync] Updated existing event: {EventName}", mappedEvent.Name);
                            }
                            else
                            {
                                // Add new event - mappedEvent.ID is used as the primary key which is safe here because we already checked for its existence
                                await _context.Events.AddAsync(mappedEvent);
                                _logger.LogDebug("[EventSync] Added new event: {EventName}", mappedEvent.Name);
                            }
                        }

                        importedCount++;
                        importedNames.Add(mappedEvent.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[EventSync] Failed to map/upsert event: {EventName}", churchSuiteEvent.Name);
                        // Continue with next event instead of failing entirely
                    }
                }

                // Step 7: Save all changes to database
                await _context.SaveChangesAsync();
                _logger.LogInformation("[EventSync] Successfully imported {ImportedCount} events for user {UserId}", importedCount, userId);

                return new SyncEventResult
                {
                    Success = true,
                    ImportedCount = importedCount,
                    Message = $"Successfully imported {importedCount} event(s) from ChurchSuite.",
                    EventNames = importedNames
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EventSync] Unexpected error during sync for user {UserId}", userId);
                return new SyncEventResult
                {
                    Success = false,
                    ErrorMessage = "An unexpected error occurred during event sync."
                };
            }
        }

        /// <summary>
        /// Fetches calendar events from ChurchSuite API using OAuth token with optional date range filter and pagination.
        /// </summary>
        /// <param name="accessToken">The OAuth access token for ChurchSuite API authorization.</param>
        /// <param name="subdomain">The ChurchSuite account subdomain.</param>
        /// <param name="startDate">Optional start date (UTC) for event range filter. If provided, events must start on or after this date.</param>
        /// <param name="endDate">Optional end date (UTC) for event range filter. If provided, events must start on or before this date.</param>
        private async Task<List<ChurchSuiteEventDto>> FetchChurchSuiteEventsAsync(string accessToken, string subdomain, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                _logger.LogInformation("[EventSync] Fetching events from ChurchSuite with subdomain {Subdomain}", subdomain);

                var allEvents = new List<ChurchSuiteEventDto>();
                var baseUrl = "https://api.churchsuite.com/v2/calendar/events";
                var page = 1;
                const int perPage = 50;
                var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                // Pagination loop
                while (true)
                {
                    // Build query parameters
                    var queryParams = new List<string>
                    {
                        $"page={page}",
                        $"per_page={perPage}"
                    };

                    // Add date range parameters if provided
                    if (startDate.HasValue)
                    {
                        var startUtc = startDate.Value.Kind == DateTimeKind.Unspecified
                            ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc)
                            : startDate.Value.ToUniversalTime();
                        queryParams.Add($"starts_after={startUtc:yyyy-MM-dd}");
                    }

                    if (endDate.HasValue)
                    {
                        var endUtc = endDate.Value.Kind == DateTimeKind.Unspecified
                            ? DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc)
                            : endDate.Value.ToUniversalTime();
                        queryParams.Add($"starts_before={endUtc:yyyy-MM-dd}");
                    }

                    var url = $"{baseUrl}?{string.Join("&", queryParams)}";
                    _logger.LogInformation("[EventSync] Fetching page {Page} with URL: {Url}", page, url);

                    // Create HTTP request with HttpRequestMessage format
                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                    using var response = await _httpClient.SendAsync(request);
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("[EventSync] Failed to fetch events page {Page} from ChurchSuite. Status: {StatusCode}", page, response.StatusCode);
                        break;
                    }

                    var responseBody = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("[EventSync] Page {Page} response length: {Length} bytes", page, responseBody.Length);

                    // Parse the paginated response with "data" property and pagination info
                    try
                    {
                        using var jsonDoc = System.Text.Json.JsonDocument.Parse(responseBody);
                        var root = jsonDoc.RootElement;

                        if (root.TryGetProperty("data", out var dataArray))
                        {
                            var pageEvents = System.Text.Json.JsonSerializer.Deserialize<List<ChurchSuiteEventDto>>(dataArray.GetRawText(), options);
                            if (pageEvents != null && pageEvents.Count > 0)
                            {
                                _logger.LogInformation("[EventSync] Parsed page {Page}: {EventCount} events", page, pageEvents.Count);
                                allEvents.AddRange(pageEvents);
                            }
                            else
                            {
                                _logger.LogInformation("[EventSync] Page {Page} returned no events", page);
                                break;
                            }

                            // Check if there's a next page
                            if (root.TryGetProperty("pagination", out var paginationObj))
                            {
                                if (paginationObj.TryGetProperty("next_page", out var nextPageElement) && nextPageElement.ValueKind != System.Text.Json.JsonValueKind.Null)
                                {
                                    if (nextPageElement.TryGetInt32(out var nextPage))
                                    {
                                        _logger.LogInformation("[EventSync] Found next page: {NextPage}", nextPage);
                                        page = nextPage;
                                        continue;
                                    }
                                }
                            }

                            // No next page, stop pagination
                            _logger.LogInformation("[EventSync] Reached end of pagination");
                            break;
                        }
                        else
                        {
                            _logger.LogWarning("[EventSync] No 'data' property in response. Response properties: {Properties}", 
                                string.Join(", ", root.EnumerateObject().Select(p => p.Name)));
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[EventSync] Failed to parse response page {Page}", page);
                        break;
                    }
                }

                _logger.LogInformation("[EventSync] Fetched total of {EventCount} events across all pages", allEvents.Count);
                return allEvents;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EventSync] Exception while fetching events from ChurchSuite");
                return new List<ChurchSuiteEventDto>();
            }
        }

        /// <summary>
        /// Extracts the most appropriate image URL from an ImageDto object.
        /// Preference order: medium > small > thumbnail
        /// </summary>
        private string? ExtractImageUrl(ImageDto? image)
        {
            if (image == null)
                return null;

            // Prefer medium size, then small, then thumbnail
            if (!string.IsNullOrWhiteSpace(image.Medium))
                return image.Medium;
            if (!string.IsNullOrWhiteSpace(image.Small))
                return image.Small;
            if (!string.IsNullOrWhiteSpace(image.Thumbnail))
                return image.Thumbnail;

            return null;
        }

        /// <summary>
        /// DTO for ChurchSuite OAuth token refresh response.
        /// </summary>
        private class TokenRefreshResponse
        {
            public string AccessToken { get; set; }
            public string RefreshToken { get; set; }
            public int? ExpiresIn { get; set; }
            public string TokenType { get; set; }

            // Allow flexibility in JSON property names
            [System.Text.Json.Serialization.JsonPropertyName("access_token")]
            public string AccessTokenAlt { set { AccessToken = value; } }

            [System.Text.Json.Serialization.JsonPropertyName("refresh_token")]
            public string RefreshTokenAlt { set { RefreshToken = value; } }

            [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
            public int? ExpiresInAlt { set { ExpiresIn = value; } }

            [System.Text.Json.Serialization.JsonPropertyName("token_type")]
            public string TokenTypeAlt { set { TokenType = value; } }
        }
    }
}
