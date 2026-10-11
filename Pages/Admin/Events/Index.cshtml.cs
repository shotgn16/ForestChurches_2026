using ForestChurches.Data;
using ForestChurches.Models;
using ForestChurches.Components.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Identity;

namespace ForestChurches.Pages.Admin.Events
{
    [Authorize(Policy = "EventManagement.Read")]
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private ForestChurchesContext _context;
        private readonly ForestChurches.Components.Security.Encoder _Encoder;
        private readonly IChurchSuiteService _churchSuiteService;
        private readonly IChurchSuiteTokenService _tokenService;
        private readonly UserManager<ForestChurches.Components.Users.ChurchAccount> _userManager;

        internal List<EventsModel> Events;

        [TempData]
        public string StatusMessage { get; set; }

        public IndexModel(ILogger<IndexModel> logger, ForestChurchesContext context, Components.Security.Encoder encoder, IChurchSuiteService churchSuiteService, IChurchSuiteTokenService tokenService, UserManager<ForestChurches.Components.Users.ChurchAccount> userManager)
        {
            _logger = logger;
            _context = context;
            _Encoder = encoder;
            _churchSuiteService = churchSuiteService;
            _tokenService = tokenService;
            _userManager = userManager;
        }

        public void OnGet()
        {
            LoadAsync();
        }

        private async Task<IActionResult> LoadAsync()
        {
            Events = _context.Events.ToList();

            return RedirectToPage();
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostSyncEventsFromChurchSuiteOAuthAsync()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    StatusMessage = "Error: Unable to find the current user.";
                    return RedirectToPage();
                }

                _logger.LogInformation("[EventSync-Admin] Sync requested for user {UserId}", user.Id);

                // Check if user has active ChurchSuite credentials
                var credentials = await _churchSuiteService.GetActiveCredentialForUserAsync(user.Id).ConfigureAwait(true);
                if (credentials == null)
                {
                    // If the user hasn't configured a per-user client id, show helpful message
                    if (string.IsNullOrWhiteSpace(user.ClientId))
                    {
                        StatusMessage = "ChurchSuite is not connected for your account. Configure your Client ID on Manage -> External Logins and then authorize.";
                        _logger.LogInformation("[EventSync-Admin] No credentials and no client id for user {UserId}.", user.Id);
                        return RedirectToPage();
                    }

                    _logger.LogInformation("[EventSync-Admin] No credentials found. Redirecting to authorize.", user.Id);
                    StatusMessage = "Please authorize with ChurchSuite first.";
                    return Redirect($"/Identity/Account/Manage/ChurchProfile?handler=SyncChurchSuiteOAuth");
                }

                _logger.LogInformation("[EventSync-Admin] Found credentials. Token expires: {ExpiresAt}", credentials.ExpiresAt);

                // Ensure the token is valid or attempt refresh
                var validCredential = await _tokenService.GetValidCredentialAsync(user.Id).ConfigureAwait(true);
                if (validCredential == null)
                {
                    StatusMessage = "Your ChurchSuite credentials are invalid or expired. Please reconnect via Manage -> External Logins.";
                    _logger.LogWarning("[EventSync-Admin] Token invalid and refresh failed for user {UserId}.", user.Id);
                    return RedirectToPage();
                }

                // Fetch profile from ChurchSuite to verify credentials and get subdomain
                var profile = await _churchSuiteService.GetProfileFromOAuthAsync(User).ConfigureAwait(true);

                if (profile?.Account == null)
                {
                    StatusMessage = "Error: Could not connect to ChurchSuite. Credentials may be invalid or expired.";
                    _logger.LogWarning("[EventSync-Admin] Failed to fetch profile for user {UserId}. Credentials may need refresh.", user.Id);
                    return RedirectToPage();
                }

                _logger.LogInformation("[EventSync-Admin] Profile fetched successfully. Account: {AccountName}, Subdomain: {Subdomain}", 
                    profile.Account.Name, profile.Account.Subdomain);

                // Extract subdomain or create from account name
                var subdomain = profile.Account.Subdomain ?? profile.Account.Name?.ToLower()?.Replace(" ", "-") ?? "unknown";
                var churchName = profile.Account.Name ?? "Unknown Church";

                // Fetch all events from ChurchSuite using OAuth token
                _logger.LogInformation("[EventSync-Admin] Fetching events from ChurchSuite for subdomain {Subdomain}", subdomain);
                var churchSuiteEvents = await _churchSuiteService.GetCalendarEventsFromOAuthAsync(credentials.AccessToken, subdomain).ConfigureAwait(true);

                if (churchSuiteEvents == null || churchSuiteEvents.Count == 0)
                {
                    StatusMessage = "No events found on ChurchSuite to sync.";
                    _logger.LogInformation("[EventSync-Admin] No events returned from ChurchSuite");
                    return RedirectToPage();
                }

                _logger.LogInformation("[EventSync-Admin] Retrieved {EventCount} events from ChurchSuite", churchSuiteEvents.Count);

                // Sync events to database - upsert pattern (insert new, update existing)
                int addedCount = 0;
                int updatedCount = 0;

                foreach (var evt in churchSuiteEvents)
                {
                    try
                    {
                        // Use ChurchSuite ID for matching (most reliable)
                        var existingEvent = await _context.Events
                            .FirstOrDefaultAsync(e => e.ID == evt.ID)
                            .ConfigureAwait(true);

                        if (existingEvent == null)
                        {
                            // New event - add it (manual mapping from ChurchSuite DTO to EventsModel)
                            var newEvent = new EventsModel
                            {
                                ID = evt.ID,
                                Name = evt.Name,
                                Description = string.IsNullOrEmpty(evt.Description) ? "No description available" : evt.Description,
                                Address = evt.Address,
                                Link = evt.Link,
                                Date = evt.Date,
                                StartTime = evt.StartTime,
                                EndTime = evt.EndTime,
                                Church = churchName,
                                User = user.Email ?? user.UserName,
                                Repeats = evt.Repeats ?? "Unknown",
                                ImageBlobName = evt.ImageBlobName,
                                ImageArray = evt.ImageArray
                            };

                            await _context.Events.AddAsync(newEvent);
                            addedCount++;
                            _logger.LogDebug("[EventSync-Admin] Added new event: {EventName} on {Date}", newEvent.Name, newEvent.Date);
                        }
                        else
                        {
                            // Existing event - update it with fresh data from ChurchSuite
                            existingEvent.Name = evt.Name;
                            existingEvent.Description = evt.Description ?? "No description available";
                            existingEvent.Address = evt.Address;
                            existingEvent.Link = evt.Link;
                            existingEvent.Date = evt.Date;
                            existingEvent.StartTime = evt.StartTime;
                            existingEvent.EndTime = evt.EndTime;
                            existingEvent.Church = churchName;
                            existingEvent.User = user.Email ?? user.UserName;

                            _context.Events.Update(existingEvent);
                            updatedCount++;
                            _logger.LogDebug("[EventSync-Admin] Updated existing event: {EventName} on {Date}", evt.Name, evt.Date);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[EventSync-Admin] Failed to sync event: {EventName}", evt.Name);
                        // Continue with next event instead of failing entirely
                    }
                }

                // Save all changes to database
                if (addedCount > 0 || updatedCount > 0)
                {
                    await _context.SaveChangesAsync().ConfigureAwait(true);
                    _logger.LogInformation("[EventSync-Admin] Sync completed for user {UserId}. Added: {Added}, Updated: {Updated}", 
                        user.Id, addedCount, updatedCount);

                    StatusMessage = $"✓ Sync completed! Added {addedCount} new event(s), Updated {updatedCount} existing event(s).";
                }
                else
                {
                    StatusMessage = "No new or updated events to sync.";
                }

                return RedirectToPage();
            }
            catch (HttpRequestException ex)
            {
                StatusMessage = "Error: Unable to connect to ChurchSuite. Please check your internet connection and try again.";
                _logger.LogError(ex, "[EventSync-Admin] HTTP error while syncing events");
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                StatusMessage = "Error: An unexpected error occurred while syncing events. Please try again.";
                _logger.LogError(ex, "[EventSync-Admin] Unexpected error while syncing events");
                return RedirectToPage();
            }
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostDeleteEvent(string value)

        {
            try
            {
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state.");
                    StatusMessage = "Invalid model.";
                    return RedirectToPage();
                }

                else if (ModelState.IsValid)
                {
                    var eventToDelete = await _context.Events
                    .FirstOrDefaultAsync(e => e.ID == _Encoder.Unprotect<string>(value, "fc.data.admin.events"));

                    if (eventToDelete == null)
                    {
                        _logger.LogWarning($"Event with ID '{_Encoder.Unprotect<string>(value, "fc.data.admin.events")}' not found.");
                        StatusMessage = $"Event with ID '{_Encoder.Unprotect<string>(value, "fc.data.admin.events")}' not found.";
                        return RedirectToPage();
                    }

                    _context.Events.Remove(eventToDelete);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"Event: '{eventToDelete.Name}' successfully deleted!");
                    StatusMessage = $"Event '{eventToDelete.Name}' successfully deleted!";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting event.");
                StatusMessage = "Error deleting event.";
            }

            return RedirectToPage();
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostDownloadEvent(string value)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    StatusMessage = "Error! The data model is an invalid format";
                    return Page();
                }
                else if (ModelState.IsValid)
                {
                    var eventId = _Encoder.Unprotect<string>(value, "fc.data.admin.events");
                    var eventToDownload = await _context.Events.FirstOrDefaultAsync(e => e.ID == eventId);

                    if (eventToDownload == null)
                    {
                        _logger.LogError($"Event ({eventId}) not found...");
                        StatusMessage = $"Error! We we're unable to load the EventID ({value})";
                        return RedirectToPage();
                    }

                    var eventData = new
                    {
                        eventToDownload.Name,
                        eventToDownload.Date,
                        eventToDownload.StartTime,
                        eventToDownload.EndTime,
                        eventToDownload.Description,
                        eventToDownload.Address,
                        eventToDownload.Link,
                        eventToDownload.User,
                        eventToDownload.Church
                    };

                    var settings = new JsonSerializerSettings
                    {
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                        NullValueHandling = NullValueHandling.Ignore
                    };

                    var eventToDownloadJson = JsonConvert.SerializeObject(eventData, settings);
                    var fileName = $"Event-{eventToDownload.Name}.json";
                    var fileBytes = System.Text.Encoding.UTF8.GetBytes(eventToDownloadJson);
                    return File(fileBytes, "application/json", fileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error downloading event: {ex.Message}");
                StatusMessage = $"Error! We are unable to download the event";
            }
            return RedirectToPage();
        }
    }
}
