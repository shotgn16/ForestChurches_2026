// This is a personal academic project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++, C#, and Java: https://pvs-studio.com

using ForestChurches.Components.Http.Google;
using ForestChurches.Components.ImageHandler;
using ForestChurches.Components.Security;
using ForestChurches.Components.Users;
using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ForestChurches.Areas.Identity.Pages.Account.Manage
{
    public class ChurchProfileModel : PageModel
    {
        private readonly ILogger<ChurchProfileModel> _logger;
        private readonly UserManager<ChurchAccount> _userManager;
        private readonly ForestChurchesContext _context;
        private readonly GoogleInterface _googleAPI;
        private readonly ForestChurches.Components.Security.Encoder _Encoder;
        private readonly ImageInterface _imageInterface;

        public ChurchProfileModel(
            ForestChurches.Components.Security.Encoder Encoder,
            ILogger<ChurchProfileModel> logger,
            UserManager<ChurchAccount> userManager,
            ForestChurchesContext context,
            GoogleInterface googleAPI,
            ImageInterface imageInterface)
        {
            _Encoder = Encoder ?? throw new ArgumentNullException(nameof(Encoder));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _googleAPI = googleAPI ?? throw new ArgumentNullException(nameof(googleAPI));
            _imageInterface = imageInterface ?? throw new ArgumentNullException(nameof(imageInterface));
        }

        public ChurchAccount CurrentUser { get; set; }
        public ChurchInformation CurrentChurch { get; set; }
        public List<ServiceTimes> ServiceTimes { get; set; }

        [BindProperty]
        public ProfileUpdateModel SaveChangesModel { get; set; }

        [BindProperty]
        public IFormFile UnprocessedImage { get; set; }

        [BindProperty]
        public ServiceTimes ChurchService { get; set; }
        public string StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                CurrentUser = await _userManager.Users
                    .Include(u => u.ChurchInformation)
                    .FirstOrDefaultAsync(u => u.Id == _userManager.GetUserId(User));
                
                if (CurrentUser == null)
                {
                    throw new ArgumentNullException(nameof(CurrentUser), "CurrentUser is null");
                }

                CurrentChurch = await _context.ChurchInformation.FirstOrDefaultAsync(c => c.ChurchAccountId == CurrentUser.Id);
                if (CurrentChurch == null)
                {
                    throw new ArgumentNullException(nameof(CurrentChurch), "CurrentChurch is null");
                }

                PopulateSaveChangesModel();
                ServiceTimes = await _context.ServiceTimes.Where(c => c.ChurchInformationId == CurrentChurch.ID).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while loading church information.");
                StatusMessage = "An error occurred while loading your church information. Please try again later.";
            }

            return Page();
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostUpdateChurchProfileAsync()
        {
            try
            {
                ModelState.Remove(nameof(UnprocessedImage));
                ModelState.Remove(nameof(ChurchService.Note));
                ModelState.Remove(nameof(ChurchService.Time));

                // Remove validation for UnprocessedImage
                ModelState.Remove(nameof(UnprocessedImage));

                if (!ModelState.IsValid)
                {
                    LogModelErrors();
                    StatusMessage = "Error: Invalid data model...";
                    return RedirectToPage();
                }

                if (SaveChangesModel == null)
                {
                    StatusMessage = "Error: Unable to save changes. Please try again later.";
                    return Page();
                }

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    StatusMessage = "Error: Unable to find the current user.";
                    return Page();
                }

                CurrentChurch = await _context.ChurchInformation
                    .FirstOrDefaultAsync(c => c.ChurchAccountId == user.Id);
                if (CurrentChurch == null)
                {
                    StatusMessage = "Error: Church information is missing.";
                    return Page();
                }

                UpdateChurchInformation();
                await _context.SaveChangesAsync();

                StatusMessage = $"The Church: ({CurrentChurch.Name}) was successfully updated!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating church information.");
                StatusMessage = "An error occurred while updating church information. Please try again later.";
            }

            return Page();
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostRemoveServiceAsync(string id)
        {
            try
            {
                // Remove validation for UnprocessedImage
                ModelState.Remove(nameof(UnprocessedImage));

                var service = await _context.ServiceTimes.SingleOrDefaultAsync(a => a.Id == _Encoder.Unprotect<string>(id, "fc.data.manage.church"));
                if (service == null)
                {
                    StatusMessage = "Error: Service not found.";
                    return Page();
                }

                _context.ServiceTimes.Remove(service);
                await _context.SaveChangesAsync();
                StatusMessage = "Service successfully deleted!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while removing the service.");
                StatusMessage = "An error occurred while removing the service. Please try again later.";
            }

            return RedirectToPage();
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostAddServiceTimeAsync()
        {
            try
            {
                // Remove validation for UnprocessedImage
                ModelState.Remove(nameof(SaveChangesModel.Name));
                ModelState.Remove(nameof(SaveChangesModel.Description));
                ModelState.Remove(nameof(SaveChangesModel.Congregation));
                ModelState.Remove(nameof(SaveChangesModel.Denomination));
                ModelState.Remove(nameof(UnprocessedImage));

                CurrentUser = await _userManager.GetUserAsync(User);
                CurrentChurch = await _context.ChurchInformation
                    .FirstOrDefaultAsync(c => c.ChurchAccountId == CurrentUser.Id);

                if (!ModelState.IsValid)
                {
                    LogModelErrors();
                    StatusMessage = "Error: Invalid data model...";
                    return RedirectToPage();
                }

                var existingServices = await _context.ServiceTimes
                    .Where(c => c.ChurchInformationId == CurrentChurch.ID)
                    .ToListAsync();

                if (DoesDataExist(existingServices, ChurchService))
                {
                    StatusMessage = "Warning: Unable to add service. Your service already exists.";
                }
                else
                {
                    var newService = new ServiceTimes
                    {
                        ChurchInformationId = CurrentChurch.ID,
                        Note = ChurchService.Note,
                        Time = ChurchService.Time,
                        Id = Guid.NewGuid().ToString()
                    };
                    _context.ServiceTimes.Add(newService);
                    await _context.SaveChangesAsync();
                    StatusMessage = $"Service: '{ChurchService.Note} ({ChurchService.Time})' successfully added!";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while adding the service time.");
                StatusMessage = "An error occurred while adding the service time. Please try again later.";
            }

            return RedirectToPage();
        }

        private bool DoesDataExist(List<ServiceTimes> existingData, ServiceTimes newData)
        {
            return existingData.Any(data => data.Time == newData.Time && data.Note == newData.Note);
        }


        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnGetMarkChurchStatusAsync(string status)
        {
            try
            {
                // Remove validation for UnprocessedImage
                ModelState.Remove(nameof(UnprocessedImage));

                CurrentUser = await _userManager.GetUserAsync(User);
                CurrentChurch = await _context.ChurchInformation
                    .FirstOrDefaultAsync(c => c.ChurchAccountId == CurrentUser.Id);

                if (CurrentChurch != null)
                {
                    CurrentChurch.Status = status;
                    _context.Update(CurrentChurch);
                    await _context.SaveChangesAsync();
                    StatusMessage = $"The Church ({CurrentChurch.Name}) was successfully set to status ({status}).";
                }
                else
                {
                    StatusMessage = "Error: Unable to find the current church.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while changing the church status.");
                StatusMessage = "An error occurred while changing the church status. Please try again later.";
            }

            return RedirectToPage();
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostUpdateUserLogoAsync()
        {
            try
            {
                // Remove validation for unused entities
                ModelState.Remove(nameof(SaveChangesModel.Name));
                ModelState.Remove(nameof(SaveChangesModel.Description));
                ModelState.Remove(nameof(SaveChangesModel.Denomination));
                ModelState.Remove(nameof(SaveChangesModel.Congregation));
                ModelState.Remove(nameof(ChurchService.Note));
                ModelState.Remove(nameof(ChurchService.Time));

                if (!ModelState.IsValid)
                {
                    LogModelErrors();
                    StatusMessage = "Error: Invalid data model...";
                    return RedirectToPage();
                }

                CurrentUser = await _userManager.GetUserAsync(User);

                if (UnprocessedImage != null)
                {
                    // Upload to blob storage
                    var blobService = HttpContext.RequestServices.GetRequiredService<ForestChurches.Components.Storage.IBlobStorageService>();
                    var blobName = await blobService.UploadAsync(UnprocessedImage, null);

                    // Keep legacy ImageArray for compatibility but prefer blob reference
                    CurrentUser.ImageArray = await _imageInterface.ConvertToByteArray(UnprocessedImage);
                    CurrentUser.ImageBlobName = blobName;
                    _context.Update(CurrentUser);
                    await _context.SaveChangesAsync();
                    StatusMessage = "Logo successfully updated!";
                }
                else
                {
                    StatusMessage = "Error: No image file selected.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating the logo.");
                StatusMessage = "An error occurred while updating the logo. Please try again later.";
            }

            return RedirectToPage();
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnGetGetAddressAsync(double latitude, double longitude)
        {
            string address = string.Empty;

            try
            {
                address = await _googleAPI.ConvertToAddress(latitude, longitude);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving the address.");
            }

            return Content(address);
        }

        private void PopulateSaveChangesModel()
        {
            SaveChangesModel = new ProfileUpdateModel
            {
                ID = CurrentChurch.ID,
                Name = CurrentChurch.Name,
                Description = CurrentChurch.Description,
                Denomination = CurrentChurch.Denomination,
                Congregation = CurrentChurch.Congregation,
                Address = CurrentChurch.Address,
                Phone = CurrentChurch.Phone,
                Churchsuite = CurrentChurch.Churchsuite,
                Website = CurrentChurch.Website,
                Parking = CurrentChurch.Parking,
                Restrooms = CurrentChurch.Restrooms,
                WheelchairAccess = CurrentChurch.WheelchairAccess,
                Wifi = CurrentChurch.Wifi,
                Refreshments = CurrentChurch.Refreshments,
                HearingAidSupport = CurrentChurch.HearingAidSupport,
                Kitchen = CurrentChurch.Kitchen,
                MeetingHall = CurrentChurch.MeetingHall,
                Childcare = CurrentChurch.Childcare,
                OutdoorSpaces = CurrentChurch.OutdoorSpaces,
                WorshipHall = CurrentChurch.WorshipHall,
            };
        }

        private void UpdateChurchInformation()
        {
            CurrentChurch.Name = SaveChangesModel.Name;
            CurrentChurch.Description = SaveChangesModel.Description;
            CurrentChurch.Denomination = SaveChangesModel.Denomination;
            CurrentChurch.Congregation = SaveChangesModel.Congregation;
            CurrentChurch.Address = SaveChangesModel.Address;
            CurrentChurch.Phone = SaveChangesModel.Phone;
            CurrentChurch.Churchsuite = SaveChangesModel.Churchsuite;
            CurrentChurch.Website = SaveChangesModel.Website;
            CurrentChurch.Parking = SaveChangesModel.Parking;
            CurrentChurch.Restrooms = SaveChangesModel.Restrooms;
            CurrentChurch.WheelchairAccess = SaveChangesModel.WheelchairAccess;
            CurrentChurch.Wifi = SaveChangesModel.Wifi;
            CurrentChurch.Refreshments = SaveChangesModel.Refreshments;
            CurrentChurch.HearingAidSupport = SaveChangesModel.HearingAidSupport;
            CurrentChurch.Kitchen = SaveChangesModel.Kitchen;
            CurrentChurch.Childcare = SaveChangesModel.Childcare;
            CurrentChurch.OutdoorSpaces = SaveChangesModel.OutdoorSpaces;
            CurrentChurch.WorshipHall = SaveChangesModel.WorshipHall;
            CurrentChurch.MeetingHall = SaveChangesModel.MeetingHall;

            _context.Update(CurrentChurch);
        }

        private void LogModelErrors()
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors);
            foreach (var error in errors)
            {
                _logger.LogError(error.ErrorMessage);
            }
        }
    }
}
