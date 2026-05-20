using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ForestChurches.Components.Users;
using Newtonsoft.Json;
using System.Linq;

namespace ForestChurches.Pages.Admin.Church
{
    public class IndexModel : PageModel
    {
        private ForestChurchesContext _context;
        private readonly ForestChurches.Components.Security.Encoder _Encoder;
        private ILogger<IndexModel> _logger;

        public List<ChurchInformation> Churches { get; private set; }
        public List<ChurchAccount> ChurchUsers { get; private set; }
        
        [TempData]
        public string StatusMessage { get; set; }

        public IndexModel(
            ForestChurchesContext context,
            ILogger<IndexModel> logger, ForestChurches.Components.Security.Encoder Encoder)
        {
            _context = context;
            _Encoder = Encoder;
            _logger = logger;
        }

        public IActionResult OnGet()
        {
            Churches = _context.ChurchInformation.ToList();

            ChurchUsers = _context.Users.ToList();

            return Page();
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostDeleteChurch(string value)
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
                    var churchToReset = await _context.ChurchInformation.FirstOrDefaultAsync(a => a.ID == _Encoder.Unprotect<string>(value, "fc.data.admin.church"));
                    if (churchToReset == null)
                    {
                        _logger.LogError($"Church ({_Encoder.Unprotect<string>(value, "fc.data.admin.church")}) not found...");
                        StatusMessage = $"Error! We we're unable to load the ChurchID ({value})";
                        return RedirectToPage();
                    }

                    churchToReset.Name = "Default Church Name";
                    churchToReset.Denomination = "Default Denomination";
                    churchToReset.Website = new Uri("https://forestchurches.co.uk/");
                    churchToReset.Status = "NotCreated";
                    churchToReset.Address = "Default Address";
                    churchToReset.Description = "Default Description";
                    churchToReset.Congregation = "Default Congregation";
                    churchToReset.Phone = "Default Phone";
                    churchToReset.Churchsuite = new Uri("https://churchsuite.com/");
                    churchToReset.Parking = true;
                    churchToReset.Restrooms = true;
                    churchToReset.WheelchairAccess = true;
                    churchToReset.Wifi = true;
                    churchToReset.Refreshments = true;

                    _context.ChurchInformation.Update(churchToReset);
                    await _context.SaveChangesAsync().ConfigureAwait(true);

                    StatusMessage = $"Success! The Church ({value}) was successfully reset";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error resetting church: {ex.Message}");
                StatusMessage = $"Error! We are unable to reset the church to default";
            }

            return RedirectToPage();
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostDownloadChurch(string value)
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
                    var churchId = _Encoder.Unprotect<string>(value, "fc.data.admin.church");
                    var churchToDownload = await _context.ChurchInformation.FirstOrDefaultAsync(a => a.ID == churchId);
                    var serviceTimes = await _context.ServiceTimes.Where(a => a.ChurchInformationId == churchId).ToListAsync();

                    if (churchToDownload == null)
                    {
                        _logger.LogError($"Church ({churchId}) not found...");
                        StatusMessage = $"Error! We we're unable to load the ChurchID ({value})";
                        return RedirectToPage();
                    }

                    var churchData = new
                    {
                        Church = new
                        {
                            churchToDownload.Name,
                            churchToDownload.Description,
                            churchToDownload.Denomination,
                            churchToDownload.Congregation,
                            churchToDownload.Address,
                            churchToDownload.Phone,
                            churchToDownload.Churchsuite,
                            churchToDownload.Website,
                            churchToDownload.Parking,
                            churchToDownload.Restrooms,
                            churchToDownload.WheelchairAccess,
                            churchToDownload.Wifi,
                            churchToDownload.Refreshments,
                            churchToDownload.WorshipHall,
                            churchToDownload.Childcare,
                            churchToDownload.MeetingHall,
                            churchToDownload.Kitchen,
                            churchToDownload.OutdoorSpaces,
                            churchToDownload.HearingAidSupport
                        },
                        ServiceTimes = serviceTimes.Select(st => new
                        {
                            st.Time,
                            st.Note
                        })
                    };

                    var settings = new JsonSerializerSettings
                    {
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                        NullValueHandling = NullValueHandling.Ignore
                    };

                    var churchToDownloadJson = JsonConvert.SerializeObject(churchData, settings);
                    var fileName = $"Church-{churchToDownload.Name}.json";
                    var fileBytes = System.Text.Encoding.UTF8.GetBytes(churchToDownloadJson);
                    return File(fileBytes, "application/json", fileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error downloading church: {ex.Message}");
                StatusMessage = $"Error! We are unable to download the church";
            }
            return RedirectToPage();
        }
    }
}
