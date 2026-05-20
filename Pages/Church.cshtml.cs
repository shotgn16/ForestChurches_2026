using ForestChurches.Components.Http.Google;
using ForestChurches.Components.Security;
using ForestChurches.Components.Users;
using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using System.Web.Mvc;

namespace ForestChurches.Pages
{
    public class ChurchModel : PageModel
    {
        private readonly ForestChurchesContext _context;
        private readonly UserManager<ChurchAccount> _userManager;
        private readonly ForestChurches.Components.Configuration.Configuration _configuration;
        private readonly ForestChurches.Components.Security.Encoder _Encoder;
        private readonly ILogger<ChurchModel> _logger;
        private readonly GoogleInterface _api;

        public ChurchModel(ForestChurchesContext context, UserManager<ChurchAccount> userManager, GoogleInterface api, Components.Security.Encoder Encoder, ForestChurches.Components.Configuration.Configuration configuration, ILogger<ChurchModel> logger)
        {
            this._api = api;
            this._context = context;
            this._userManager = userManager;
            this._Encoder = Encoder;
            this._configuration = configuration;
            this._logger = logger;
        }

        private string ChurchID { get; set; }
        public Models.ChurchInformation ChurchInformation { get; set; }
        public List<EventsModel> ChurchEvents { get; set; }
        public ChurchAccount AssociatedUser { get; set; }

        internal double Latitude { get; set; }
        internal double Longitude { get; set; }

        public async Task OnGetAsync(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                ChurchID = _Encoder.Unprotect<string>(id, "fc.data.church.information");

                ChurchInformation = await _context.ChurchInformation
                    .Include(s => s.ServiceTimes)
                    .Where(variable => variable.ID == ChurchID)
                    .FirstOrDefaultAsync();

                if (ChurchInformation != null)
                {
                    double[] tempCoordinates = await _api.ConvertToCoordinates(ChurchInformation.Address);
                    Latitude = tempCoordinates[0];
                    Longitude = tempCoordinates[1];

                    AssociatedUser = await _userManager.FindByIdAsync(ChurchInformation.ChurchAccountId.ToString());

                    ChurchEvents = await _context.Events
                        .Where(variable => variable.Church == ChurchInformation.Name)
                        .Where(variable => variable.User == AssociatedUser.UserName)
                        .ToListAsync();
                }
            }
        }
        public IActionResult OnGetGoogleMapsApiKey()
        {
            try
            {
                var apiKey = _configuration.Client.GetSecret("google-api-key").Value.Value;

                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogError("Google Maps API key is missing or invalid.");
                    return BadRequest(new { error = "API key not found." });
                }

                return new Microsoft.AspNetCore.Mvc.JsonResult(new { key = apiKey });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Google Maps API key.");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}
