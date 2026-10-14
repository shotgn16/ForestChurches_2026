using ForestChurches.Components.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ForestChurches.Pages.Admin
{
    /// <summary>
    /// Admin → Email delivery: shows whether each recent email reached the email app
    /// (api.forestchurches.co.uk) and whether it has been sent, plus a health check of the email app.
    /// </summary>
    [Authorize(Policy = "Admin.Read")]
    public class EmailDeliveryModel : PageModel
    {
        private readonly EmailDeliveryTracker _tracker;
        private readonly EmailServiceMonitor _monitor;
        private readonly ILogger<EmailDeliveryModel> _logger;

        public EmailDeliveryModel(EmailDeliveryTracker tracker, EmailServiceMonitor monitor, ILogger<EmailDeliveryModel> logger)
        {
            _tracker = tracker;
            _monitor = monitor;
            _logger = logger;
        }

        public IReadOnlyList<EmailDeliveryRecord> Emails { get; private set; } = new List<EmailDeliveryRecord>();
        public EmailServiceHealth? Health { get; private set; }
        public DateTime TrackingSinceUtc { get; private set; }

        [TempData]
        public string? StatusMessage { get; set; }

        public async Task OnGetAsync()
        {
            try
            {
                Health = await _monitor.CheckHealthAsync(HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Email app health check failed on the Email delivery page");
                Health = new EmailServiceHealth { Error = ex.Message };
            }

            Load();
        }

        public async Task<IActionResult> OnPostCheckEmailAsync(string id)
        {
            var record = string.IsNullOrWhiteSpace(id) ? null : await _monitor.CheckEmailAsync(id, HttpContext.RequestAborted);
            StatusMessage = record == null
                ? "Error: that email is no longer in the list."
                : record.RemoteError != null && record.RemoteStatus is null or "Unknown"
                    ? $"Warning: {record.RemoteError}"
                    : $"Info: the email app says the email to {record.Recipient} is {record.RemoteStatus}.";
            return RedirectToPage(null, null, null, "emails");
        }

        public async Task<IActionResult> OnPostCheckAllAsync()
        {
            var checkedCount = await _monitor.CheckUnfinishedAsync(cancellationToken: HttpContext.RequestAborted);
            StatusMessage = checkedCount == 0
                ? "Info: there are no unfinished emails to check."
                : $"Info: checked {checkedCount} unfinished email(s) with the email app.";
            return RedirectToPage(null, null, null, "emails");
        }

        private void Load()
        {
            Emails = _tracker.Recent(200);
            TrackingSinceUtc = _tracker.TrackingSinceUtc;
        }
    }
}
