using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using ForestChurches.Components.Email;
using ForestChurches.Components.FileManager;
using ForestChurches.Components.Http.Google;
using ForestChurches.Components.Ui;
using ForestChurches.Components.Users;
using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ForestChurches.Pages
{
    public class ChurchModel : PageModel
    {
        private const string ChurchProtector = "fc.data.church.information";

        private readonly ForestChurchesContext _context;
        private readonly UserManager<ChurchAccount> _userManager;
        private readonly ForestChurches.Components.Configuration.Configuration _configuration;
        private readonly ForestChurches.Components.Security.Encoder _Encoder;
        private readonly ILogger<ChurchModel> _logger;
        private readonly GoogleInterface _api;
        private readonly FileInterface _fileInterface;
        private readonly iMailSender _mailSender;

        public ChurchModel(
            ForestChurchesContext context,
            UserManager<ChurchAccount> userManager,
            GoogleInterface api,
            Components.Security.Encoder Encoder,
            ForestChurches.Components.Configuration.Configuration configuration,
            ILogger<ChurchModel> logger,
            FileInterface fileInterface,
            iMailSender mailSender)
        {
            _api = api;
            _context = context;
            _userManager = userManager;
            _Encoder = Encoder;
            _configuration = configuration;
            _logger = logger;
            _fileInterface = fileInterface;
            _mailSender = mailSender;
        }

        public Models.ChurchInformation ChurchInformation { get; set; }
        public List<EventsModel> ChurchEvents { get; set; } = new();
        public ChurchAccount AssociatedUser { get; set; }

        /// <summary>The protected church id from the URL, reused by the page's forms.</summary>
        public string ProtectedId { get; set; } = string.Empty;

        /// <summary>Email to pre-fill in "Email me the details" for signed-in users.</summary>
        public string? SignedInEmail { get; set; }

        [TempData]
        public string? StatusMessage { get; set; }

        internal double Latitude { get; set; }
        internal double Longitude { get; set; }
        internal bool HasLocation { get; set; }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            ProtectedId = id ?? string.Empty;
            ChurchInformation = await FindChurchAsync(id);
            if (ChurchInformation == null)
            {
                // Unknown or expired link: render the friendly "not found" state.
                Response.StatusCode = StatusCodes.Status404NotFound;
                return Page();
            }

            try
            {
                double[] coordinates = await _api.ConvertToCoordinates(ChurchInformation.Address);
                if (coordinates is { Length: >= 2 } && !(coordinates[0] == 0 && coordinates[1] == 0))
                {
                    Latitude = coordinates[0];
                    Longitude = coordinates[1];
                    HasLocation = true;
                }
            }
            catch (Exception ex)
            {
                // The page is still useful without a map.
                _logger.LogWarning(ex, "Unable to geocode church {ChurchId}", ChurchInformation.ID);
            }

            AssociatedUser = await _userManager.FindByIdAsync(ChurchInformation.ChurchAccountId);
            if (AssociatedUser != null)
            {
                ChurchEvents = await _context.Events
                    .Where(e => e.Church == ChurchInformation.Name)
                    .Where(e => e.User == AssociatedUser.UserName)
                    .ToListAsync();
            }

            if (User?.Identity?.IsAuthenticated == true)
            {
                SignedInEmail = (await _userManager.GetUserAsync(User))?.Email;
            }

            return Page();
        }

        /// <summary>Downloads the church's details as a text file.</summary>
        public async Task<IActionResult> OnPostDownloadInformationAsync(string id)
        {
            var church = await FindChurchAsync(id);
            if (church == null)
            {
                return NotFound();
            }

            return await _fileInterface.CreateFile(church, "text/plain", "Church Information");
        }

        /// <summary>Emails the church's details (address, service times, contact link) to the visitor.</summary>
        public async Task<IActionResult> OnPostSendInformationAsync(string id, string email)
        {
            var church = await FindChurchAsync(id);
            if (church == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email.Trim()))
            {
                StatusMessage = "Error: Please enter a valid email address.";
                return RedirectToPage("/Church", null, new { id }, "save");
            }

            try
            {
                var services = string.Join("<br/>", (church.ServiceTimes ?? new List<ServiceTimes>())
                    .OrderBy(s => FcText.ServiceTimeSortKey(s.Time))
                    .Select(s => System.Net.WebUtility.HtmlEncode($"{FcText.ServiceTime(s.Time)} – {FcText.PlainText(s.Note)}")));

                _mailSender.SendEmailChurchInformation(email.Trim(), church, string.IsNullOrEmpty(services) ? "Not available" : services);
                StatusMessage = $"Sent: we've emailed the details to {email.Trim()}.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to email church information for {ChurchId}", church.ID);
                StatusMessage = "Error: Sorry, we couldn't send the email. Please try again later.";
            }

            return RedirectToPage("/Church", null, new { id }, "save");
        }

        // The Google API key is only used server-side. (A handler that returned it to any browser was removed.)
        private async Task<Models.ChurchInformation?> FindChurchAsync(string? protectedId)
        {
            if (string.IsNullOrEmpty(protectedId))
            {
                return null;
            }

            string churchId;
            try
            {
                churchId = _Encoder.Unprotect<string>(protectedId, ChurchProtector);
            }
            catch (CryptographicException)
            {
                return null;
            }

            return await _context.ChurchInformation
                .Include(s => s.ServiceTimes)
                .FirstOrDefaultAsync(c => c.ID == churchId);
        }
    }
}
