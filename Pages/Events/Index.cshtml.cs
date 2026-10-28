using ForestChurches.Components.Email;
using ForestChurches.Components.Http;
using ForestChurches.Components.Users;
using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ForestChurches.Pages.Events
{
    [Authorize(Policy = "UserEvents.View")]
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private UserManager<ChurchAccount> _userManager;
        private ForestChurchesContext _context;
        private readonly iEmail _mailRepository;
        private readonly ForestChurches.Components.Security.Encoder _Encoder;
        private readonly iMailSender _mailSender;
        private readonly IChurchSuiteService _churchSuiteService;
        private readonly IChurchSuiteEventSyncService _eventSyncService;

        internal List<EventsModel> Events;
        internal ChurchAccount CurrentUser { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        private readonly ForestChurches.Components.Users.IManagedChurches _churches;

        public IndexModel(
            UserManager<ChurchAccount> userManager, 
            ForestChurchesContext context, 
            ILogger<IndexModel> logger, 
            iEmail mailRepository, 
            iMailSender mailSender,
            ForestChurches.Components.Security.Encoder Encoder,
            IChurchSuiteService churchSuiteService,
            IChurchSuiteEventSyncService eventSyncService,
            ForestChurches.Components.Users.IManagedChurches churches)
        {
            _churches = churches;
            _mailRepository = mailRepository;
            _userManager = userManager;
            _mailSender = mailSender;
            _context = context;
            _Encoder = Encoder;
            _logger = logger;
            _churchSuiteService = churchSuiteService;
            _eventSyncService = eventSyncService;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            // Previously LoadAsync() was not awaited, so the page could render before Events was loaded.
            return await LoadAsync();
        }

        private async Task<IActionResult> LoadAsync()
        {
            CurrentUser = await _userManager.GetUserAsync(User);
            Events = _context.Events.Where(a => a.User == CurrentUser.Email).ToList();

            return Page();
        }

        /// <summary>
        /// Handler for syncing events from ChurchSuite using stored per-user OAuth credentials.
        /// If user hasn't authorized with ChurchSuite, returns a message prompting authorization.
        /// </summary>
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostSyncEventsAsync()
        {
            try
            {
                _logger.LogInformation("[EventSync-UI] Sync requested");

                CurrentUser = await _userManager.GetUserAsync(User);
                if (CurrentUser == null)
                {
                    StatusMessage = "Error: Unable to find the current user.";
                    _logger.LogWarning("[EventSync-UI] Current user not found");
                    return RedirectToPage();
                }

                _logger.LogInformation("[EventSync-UI] Syncing events for user {UserId}", CurrentUser.Id);

                // Call the sync service
                // Events are imported for the church the user is working on (accounts can manage several).
                var targetChurch = await _churches.CurrentAsync(HttpContext, CurrentUser.Id);
                var result = await _eventSyncService.SyncEventsAsync(CurrentUser.Id, targetChurch?.ID);

                if (result.Success)
                {
                    StatusMessage = $"Success: {result.Message}";
                    _logger.LogInformation("[EventSync-UI] Sync successful. Imported {Count} events", result.ImportedCount);
                }
                else
                {
                    // Check if authorization is required
                    if (result.Message == "AUTHORIZE_REQUIRED")
                    {
                        StatusMessage = "Error: Please authorize with ChurchSuite to sync events. Redirecting to authorization...";
                        _logger.LogInformation("[EventSync-UI] Authorization required for user {UserId}", CurrentUser.Id);
                        // Redirect to external login with ChurchSuite provider
                        return RedirectToPage("/Identity/Account/ExternalLogin", new { area = "Identity", provider = "ChurchSuite", returnUrl = "/Events" });
                    }
                    else
                    {
                        StatusMessage = $"Error: {result.ErrorMessage}";
                        _logger.LogWarning("[EventSync-UI] Sync failed: {Error}", result.ErrorMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EventSync-UI] Unexpected error during event sync");
                StatusMessage = "Error: An unexpected error occurred during event sync.";
            }

            return RedirectToPage();
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostSyncFromChurchSuiteAsync(string churchSuiteAccountId, string churchSuiteApiKey)
        {
            try
            {
                if (string.IsNullOrEmpty(churchSuiteAccountId) || string.IsNullOrEmpty(churchSuiteApiKey))
                {
                    StatusMessage = "Error: ChurchSuite account ID and API key are required.";
                    return RedirectToPage();
                }

                CurrentUser = await _userManager.GetUserAsync(User);
                if (CurrentUser == null)
                {
                    StatusMessage = "Error: Unable to find the current user.";
                    return RedirectToPage();
                }

                // Fetch events from ChurchSuite
                var churchSuiteEvents = await _churchSuiteService.GetCalendarEventsAsync(churchSuiteAccountId, churchSuiteApiKey);

                if (churchSuiteEvents == null || churchSuiteEvents.Count == 0)
                {
                    StatusMessage = "No events found in ChurchSuite or sync completed with no new events.";
                    return RedirectToPage();
                }

                int importedCount = 0;
                int skippedCount = 0;

                // Get the church name for the current user
                var churchInfo = await _churches.CurrentAsync(HttpContext, CurrentUser.Id);
                var churchName = churchInfo?.Name ?? CurrentUser.Email; // Fallback to email if church not found

                foreach (var churchSuiteEvent in churchSuiteEvents)
                {
                    try
                    {
                        // Check if event already exists to avoid duplicates
                        var existingEvent = _context.Events.FirstOrDefault(e => 
                            e.Name == churchSuiteEvent.Name && 
                            e.Date == churchSuiteEvent.Date && 
                            e.User == CurrentUser.Email);

                        if (existingEvent == null)
                        {
                            // Set user to current user email and church to church name
                            churchSuiteEvent.User = CurrentUser.Email;
                            churchSuiteEvent.Church = churchName;

                            _context.Events.Add(churchSuiteEvent);
                            importedCount++;
                        }
                        else
                        {
                            skippedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to import event {EventName} from ChurchSuite.", churchSuiteEvent.Name);
                        skippedCount++;
                    }
                }

                if (importedCount > 0)
                {
                    await _context.SaveChangesAsync();
                }

                StatusMessage = $"ChurchSuite sync completed. Imported {importedCount} events, skipped {skippedCount} duplicate(s).";
                _logger.LogInformation("User {UserId} successfully synced {Count} events from ChurchSuite.", CurrentUser.Id, importedCount);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error while syncing events from ChurchSuite.");
                StatusMessage = "Error: Failed to connect to ChurchSuite. Please check your credentials and try again.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while syncing events from ChurchSuite.");
                StatusMessage = $"Error: An unexpected error occurred during sync. {ex.Message}";
            }

            return RedirectToPage();
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostDeleteEvent(string eventId)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Page();
                }

                else if (ModelState.IsValid)
                {
                    // Fix this when you get back  Currently no ID in the 'value'
                    var eventToDelete = _context.Events.Where(a => a.ID == _Encoder.Unprotect<string>(eventId, "fc.data.user.events")).FirstOrDefault();
                    var eventDeleted = _context.Events.Remove(eventToDelete);

                    _context.SaveChangesAsync();

                    if (eventDeleted.State == Microsoft.EntityFrameworkCore.EntityState.Deleted)
                    {
                        _logger.LogInformation($"Event ({eventToDelete.Name}) successfully deleted");

                        _mailSender.SendEmailEventDeleted(_userManager.GetUserName(User), _userManager.GetUserName(User), "/Events", eventToDelete);
                    }
                }
            }

            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                throw ex;
            }

            return RedirectToPage();
        }
    }
}
