using ForestChurches.Components.Users;
using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ForestChurches.Pages.Admin.Church
{
    public class EditModel : PageModel
    {
        private readonly ForestChurchesContext _context;
        private readonly ILogger<EditModel> _logger;
        private readonly UserManager<ChurchAccount> _userManager;
        private readonly ForestChurches.Components.Security.Encoder _Encoder;

        public EditModel(
            ForestChurchesContext context,
            ILogger<EditModel> logger, UserManager<ChurchAccount> userManager, Components.Security.Encoder Encoder)
        {
            _userManager = userManager;
            _context = context;
            _logger = logger;
            _Encoder = Encoder;
        }

        [BindProperty]
        public ProfileUpdateModel SaveChangesModel { get; set; }

        public ChurchInformation Church { get; private set; }

        [TempData]
        internal string ChurchID { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public IActionResult OnGet(string id)
        {
            try
            {
                Church = _context.ChurchInformation.Where(c => c.ID == _Encoder.Unprotect<string>(id, "fc.data.admin.church")).FirstOrDefault();
                ChurchID = _Encoder.Unprotect<string>(id, "fc.data.admin.church");

                if (Church == null)
                {
                    StatusMessage = "Error: Church not found.";
                    return Page();
                }

                SaveChangesModel = new ProfileUpdateModel
                {
                    ID = Church.ID,
                    Name = Church.Name,
                    Description = Church.Description,
                    Denomination = Church.Denomination,
                    Congregation = Church.Congregation,
                    Address = Church.Address,
                    Phone = Church.Phone,
                    Churchsuite = Church.Churchsuite,
                    Website = Church.Website,
                    Parking = Church.Parking,
                    Restrooms = Church.Restrooms,
                    WheelchairAccess = Church.WheelchairAccess,
                    Wifi = Church.Wifi,
                    Refreshments = Church.Refreshments,
                    HearingAidSupport = Church.HearingAidSupport,
                    Kitchen = Church.Kitchen,
                    MeetingHall = Church.MeetingHall,
                    Childcare = Church.Childcare,
                    OutdoorSpaces = Church.OutdoorSpaces,
                    WorshipHall = Church.WorshipHall
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while retrieving ({_Encoder.Unprotect<string>(id, "fc.data.admin.church")}).");
            }

            return Page();
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostUpdateChurch()
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    StatusMessage = "Error: Invalid model state.";
                    return Page();
                }

                if (SaveChangesModel == null)
                {
                    StatusMessage = "Error: Unable to save changes. Please try again later.";
                    return Page();
                }

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    StatusMessage = "Error: Unable to retrieve user information.";
                    return Page();
                }

                var church = await _context.ChurchInformation
                    .FirstOrDefaultAsync(c => c.ChurchAccountId == user.Id);
                if (church == null)
                {
                    StatusMessage = "Error: Church information is not found.";
                    return Page();
                }

                // Log the current state of the church object
                _logger.LogInformation("Updating church information for Church ID: {ChurchID}", church.ID);

                // Determine final parking value robustly (checkbox may not post when unchecked)
                bool finalParking = SaveChangesModel.Parking;
                try
                {
                    if (Request?.Form != null)
                    {
                        if (Request.Form.TryGetValue("SaveChangesModel.Parking", out var pv) && pv.Count > 0)
                        {
                            var parsed = pv[0].ToString();
                            if (string.Equals(parsed, "on", StringComparison.OrdinalIgnoreCase) || string.Equals(parsed, "true", StringComparison.OrdinalIgnoreCase) || parsed == "1")
                                finalParking = true;
                            else if (string.Equals(parsed, "false", StringComparison.OrdinalIgnoreCase) || parsed == "0")
                                finalParking = false;
                        }
                        else if (Request.Form.TryGetValue("Parking", out var pv2) && pv2.Count > 0)
                        {
                            var parsed = pv2[0].ToString();
                            if (string.Equals(parsed, "on", StringComparison.OrdinalIgnoreCase) || string.Equals(parsed, "true", StringComparison.OrdinalIgnoreCase) || parsed == "1")
                                finalParking = true;
                            else if (string.Equals(parsed, "false", StringComparison.OrdinalIgnoreCase) || parsed == "0")
                                finalParking = false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to parse Parking value from form; falling back to bound value.");
                }

                await _context.ChurchInformation
                    .Where(a => a.ID == church.ID)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.Name, SaveChangesModel.Name)
                        .SetProperty(p => p.Description, SaveChangesModel.Description)
                        .SetProperty(p => p.Denomination, SaveChangesModel.Denomination)
                        .SetProperty(p => p.Congregation, SaveChangesModel.Congregation)
                        .SetProperty(p => p.Address, SaveChangesModel.Address)
                        .SetProperty(p => p.Phone, SaveChangesModel.Phone)
                        .SetProperty(p => p.Churchsuite, SaveChangesModel.Churchsuite)
                        .SetProperty(p => p.Website, SaveChangesModel.Website)
                        .SetProperty(p => p.Parking, finalParking)
                        .SetProperty(p => p.Restrooms, SaveChangesModel.Restrooms)
                        .SetProperty(p => p.WheelchairAccess, SaveChangesModel.WheelchairAccess)
                        .SetProperty(p => p.Wifi, SaveChangesModel.Wifi)
                        .SetProperty(p => p.Refreshments, SaveChangesModel.Refreshments)
                        .SetProperty(p => p.HearingAidSupport, SaveChangesModel.HearingAidSupport)
                        .SetProperty(p => p.Kitchen, SaveChangesModel.Kitchen)
                        .SetProperty(p => p.Childcare, SaveChangesModel.Childcare)
                        .SetProperty(p => p.MeetingHall, SaveChangesModel.MeetingHall)
                        .SetProperty(p => p.OutdoorSpaces, SaveChangesModel.OutdoorSpaces)
                        .SetProperty(p => p.WorshipHall, SaveChangesModel.WorshipHall)
                    );

                await _context.SaveChangesAsync();

                StatusMessage = $"Your Church ({SaveChangesModel.Name}) was successfully updated!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while updating ({SaveChangesModel?.Name}).");
                StatusMessage = "Error: An unexpected error occurred. Please try again later.";
                return Page();
            }

            return RedirectToPage("./Index");
        }
    }
}