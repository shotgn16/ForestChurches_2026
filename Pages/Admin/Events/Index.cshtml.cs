using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace ForestChurches.Pages.Admin.Events
{
    [Authorize(Policy = "EventManagement.Read")]
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private ForestChurchesContext _context;
        private readonly ForestChurches.Components.Security.Encoder _Encoder;

        internal List<EventsModel> Events;

        [TempData]
        public string StatusMessage { get; set; }

        public IndexModel(ILogger<IndexModel> logger, ForestChurchesContext context, Components.Security.Encoder encoder)
        {
            _logger = logger;
            _context = context;
            _Encoder = encoder;
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
