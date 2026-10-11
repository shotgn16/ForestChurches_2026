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
using Microsoft.AspNetCore.Authentication;

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
        private readonly IChurchSuiteService _churchSuiteService;

        public ChurchProfileModel(
            ForestChurches.Components.Security.Encoder Encoder,
            ILogger<ChurchProfileModel> logger,
            UserManager<ChurchAccount> userManager,
            ForestChurchesContext context,
            GoogleInterface googleAPI,
            ImageInterface imageInterface,
            IChurchSuiteService churchSuiteService)
        {
            _Encoder = Encoder ?? throw new ArgumentNullException(nameof(Encoder));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _googleAPI = googleAPI ?? throw new ArgumentNullException(nameof(googleAPI));
            _imageInterface = imageInterface ?? throw new ArgumentNullException(nameof(imageInterface));
            _churchSuiteService = churchSuiteService ?? throw new ArgumentNullException(nameof(churchSuiteService));
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
                // Get current user first to load church data
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

                // Ensure SaveChangesModel.Parking reflects the actual posted value — model binding can fail with duplicate inputs.
                bool parsedParking = SaveChangesModel?.Parking ?? false;
                try
                {
                    if (Request?.Form != null)
                    {
                        if (Request.Form.TryGetValue("SaveChangesModel.Parking", out var pv) && pv.Count > 0)
                        {
                            foreach (var val in pv)
                            {
                                var parsed = val.ToString();
                                if (string.Equals(parsed, "on", StringComparison.OrdinalIgnoreCase) || string.Equals(parsed, "true", StringComparison.OrdinalIgnoreCase) || parsed == "1")
                                {
                                    parsedParking = true;
                                    break;
                                }
                                if (string.Equals(parsed, "false", StringComparison.OrdinalIgnoreCase) || parsed == "0")
                                {
                                    parsedParking = false;
                                }
                            }
                        }
                        else if (Request.Form.TryGetValue("Parking", out var pv2) && pv2.Count > 0)
                        {
                            foreach (var val in pv2)
                            {
                                var parsed = val.ToString();
                                if (string.Equals(parsed, "on", StringComparison.OrdinalIgnoreCase) || string.Equals(parsed, "true", StringComparison.OrdinalIgnoreCase) || parsed == "1")
                                {
                                    parsedParking = true;
                                    break;
                                }
                                if (string.Equals(parsed, "false", StringComparison.OrdinalIgnoreCase) || parsed == "0")
                                {
                                    parsedParking = false;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to parse Parking value from form; leaving bound value.");
                }

                if (SaveChangesModel == null)
                {
                    SaveChangesModel = new ProfileUpdateModel();
                }

                // Only populate missing required fields from CurrentChurch without overwriting user's posted values.
                // If key values are empty we populate them individually to avoid clobbering boolean flags like Parking.
                if (string.IsNullOrWhiteSpace(SaveChangesModel.Name)) SaveChangesModel.Name = CurrentChurch?.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(SaveChangesModel.Description)) SaveChangesModel.Description = CurrentChurch?.Description ?? string.Empty;
                if (string.IsNullOrWhiteSpace(SaveChangesModel.Denomination)) SaveChangesModel.Denomination = CurrentChurch?.Denomination ?? string.Empty;
                if (string.IsNullOrWhiteSpace(SaveChangesModel.Congregation)) SaveChangesModel.Congregation = CurrentChurch?.Congregation ?? string.Empty;

                // Restore parsed parking into the model and ensure modelstate reflects the new value.
                SaveChangesModel.Parking = parsedParking;
                ModelState.Remove("SaveChangesModel.Parking");

                // If SaveChangesModel is null or has empty required fields, populate from CurrentChurch
                if (SaveChangesModel == null || 
                    string.IsNullOrWhiteSpace(SaveChangesModel.Name) ||
                    string.IsNullOrWhiteSpace(SaveChangesModel.Description) ||
                    string.IsNullOrWhiteSpace(SaveChangesModel.Denomination) ||
                    string.IsNullOrWhiteSpace(SaveChangesModel.Congregation))
                {
                    _logger.LogWarning("SaveChangesModel has empty required fields. Reloading from CurrentChurch before validation.");
                    PopulateSaveChangesModel();
                }

                // Remove model state entries that shouldn't be validated on this POST
                ModelState.Remove(nameof(UnprocessedImage));
                ModelState.Remove(nameof(ChurchService.Note));
                ModelState.Remove(nameof(ChurchService.Time));
                // Uri binding sometimes fails for SaveChangesModel.Churchsuite/Website when users provide unexpected values; don't block Parking updates for that.
                ModelState.Remove("SaveChangesModel.Churchsuite");
                ModelState.Remove("SaveChangesModel.Website");

                if (!ModelState.IsValid)
                {
                    // If the only model errors are for Churchsuite/Website conversion, allow update to proceed so booleans like Parking can be saved.
                    var otherErrors = ModelState.Where(kvp => kvp.Value.Errors.Count > 0 && kvp.Key != "SaveChangesModel.Churchsuite" && kvp.Key != "SaveChangesModel.Website").ToList();
                    if (otherErrors.Count > 0)
                    {
                        LogModelErrors();
                        StatusMessage = "Error: Invalid data model. Please ensure all required fields are filled.";
                        return Page();
                    }
                }

                if (SaveChangesModel == null)
                {
                    StatusMessage = "Error: Unable to save changes. Please try again later.";
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

            // Handle Parking specially to be robust against duplicate or mismatched form inputs (select + checkbox)
            bool finalParking = SaveChangesModel.Parking;
            try
            {
                if (Request?.Form != null)
                {
                    // Prefer explicit SaveChangesModel.Parking form key if present, else try 'Parking'.
                    if (Request.Form.TryGetValue("SaveChangesModel.Parking", out var pv) && pv.Count > 0)
                    {
                        // If multiple values exist (checkbox + select), consider any value that indicates true.
                        foreach (var val in pv)
                        {
                            var parsed = val.ToString();
                            if (string.Equals(parsed, "on", StringComparison.OrdinalIgnoreCase) || string.Equals(parsed, "true", StringComparison.OrdinalIgnoreCase) || parsed == "1")
                            {
                                finalParking = true;
                                break;
                            }
                            if (string.Equals(parsed, "false", StringComparison.OrdinalIgnoreCase) || parsed == "0")
                            {
                                finalParking = false;
                                // continue checking in case a later value indicates true (checkbox + select)
                            }
                        }
                    }
                    else if (Request.Form.TryGetValue("Parking", out var pv2) && pv2.Count > 0)
                    {
                        foreach (var val in pv2)
                        {
                            var parsed = val.ToString();
                            if (string.Equals(parsed, "on", StringComparison.OrdinalIgnoreCase) || string.Equals(parsed, "true", StringComparison.OrdinalIgnoreCase) || parsed == "1")
                            {
                                finalParking = true;
                                break;
                            }
                            if (string.Equals(parsed, "false", StringComparison.OrdinalIgnoreCase) || parsed == "0")
                            {
                                finalParking = false;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to parse Parking value from form; falling back to bound value.");
            }

            CurrentChurch.Parking = finalParking;
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

        /// <summary>
        /// Handler to sync church profile from ChurchSuite using OAuth token.
        /// Checks for existing credentials and token validity before requiring re-authentication.
        /// Attempts silent refresh if token expired; only redirects to auth if no credentials or refresh fails.
        /// </summary>
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostSyncChurchSuiteOAuthAsync()
        {
            try
            {
                // Remove validation for form properties that aren't part of this sync form
                var keysToRemove = ModelState.Keys
                    .Where(k => k.StartsWith("SaveChangesModel") || k.StartsWith("ChurchService") || k == nameof(UnprocessedImage))
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    ModelState.Remove(key);
                }

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    _logger.LogWarning("[ChurchProfile-Sync] ModelState invalid after cleanup. Remaining errors: {Errors}", string.Join("; ", errors));
                }

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    StatusMessage = "Error: Unable to find the current user.";
                    return Page();
                }

                _logger.LogInformation("[ChurchProfile-Sync] Sync requested for user {UserId}", user.Id);

                // Step 1: Check if user has active ChurchSuite credentials already stored
                var credentials = await _churchSuiteService.GetActiveCredentialForUserAsync(user.Id).ConfigureAwait(true);
                if (credentials == null)
                {
                    _logger.LogInformation("[ChurchProfile-Sync] No stored credentials found. Starting PKCE authorize flow for user {UserId}", user.Id);
                    StatusMessage = "Redirecting to ChurchSuite to authorize access...";
                    var redirectUri = "/Identity/Account/Manage/ChurchProfile?handler=SyncChurchSuiteOAuth";
                    var url = $"/churchsuite/authorize?user_id={Uri.EscapeDataString(user.Id)}&redirect_uri={Uri.EscapeDataString(redirectUri)}";
                    return Redirect(url);
                }

                _logger.LogInformation("[ChurchProfile-Sync] Found stored credentials for user {UserId}. Token expires: {ExpiresAt}", 
                    user.Id, credentials.ExpiresAt?.ToString("O") ?? "unknown");

                // Step 2: Check if token is expired or about to expire (within 1 minute)
                var isTokenExpired = credentials.ExpiresAt == null || credentials.ExpiresAt <= DateTime.UtcNow.AddMinutes(1);
                if (isTokenExpired)
                {
                    _logger.LogInformation("[ChurchProfile-Sync] Token expired or expiring soon for user {UserId}. Attempting refresh.", user.Id);

                    // Attempt to refresh via the service (which uses the ClaimsPrincipal to trigger refresh logic if available)
                    var refreshedProfile = await _churchSuiteService.GetProfileFromOAuthAsync(User).ConfigureAwait(true);
                    if (refreshedProfile?.Account == null)
                    {
                        _logger.LogWarning("[ChurchProfile-Sync] Token refresh failed for user {UserId}. Starting PKCE re-authorize.", user.Id);
                        StatusMessage = "Your authorization with ChurchSuite has expired. Please authorize again.";
                        var redirectUri = "/Identity/Account/Manage/ChurchProfile?handler=SyncChurchSuiteOAuth";
                        var url = $"/churchsuite/authorize?user_id={Uri.EscapeDataString(user.Id)}&redirect_uri={Uri.EscapeDataString(redirectUri)}";
                        return Redirect(url);
                    }

                    _logger.LogInformation("[ChurchProfile-Sync] Token refresh succeeded for user {UserId}", user.Id);
                }

                // Step 3: Fetch profile from ChurchSuite (uses stored token, which has been refreshed if needed)
                var profile = await _churchSuiteService.GetProfileFromOAuthAsync(User).ConfigureAwait(true);

                if (profile?.Account == null)
                {
                    StatusMessage = "Error: Could not fetch profile from ChurchSuite. Please ensure you have valid permissions.";
                    _logger.LogWarning("[ChurchProfile-Sync] Failed to fetch ChurchSuite profile for user {UserId}.", user.Id);
                    return Page();
                }

                _logger.LogInformation("[ChurchProfile-Sync] Successfully fetched profile from ChurchSuite for user {UserId}. Account: {AccountName}", 
                    user.Id, profile.Account.Name);

                // Step 4: Update church information from ChurchSuite profile
                CurrentChurch = await _context.ChurchInformation
                    .FirstOrDefaultAsync(c => c.ChurchAccountId == user.Id)
                    .ConfigureAwait(true);

                if (CurrentChurch == null)
                {
                    CurrentChurch = new ChurchInformation
                    {
                        ChurchAccountId = user.Id
                    };
                    await _context.ChurchInformation.AddAsync(CurrentChurch).ConfigureAwait(true);
                    _logger.LogInformation("[ChurchProfile-Sync] Created new ChurchInformation record for user {UserId}", user.Id);
                }

                // Update church fields from profile
                if (!string.IsNullOrEmpty(profile.Account.Name))
                {
                    CurrentChurch.Name = profile.Account.Name;
                }

                if (!string.IsNullOrEmpty(profile.Account.Url) && Uri.TryCreate(profile.Account.Url, UriKind.Absolute, out var siteUri))
                {
                    CurrentChurch.Website = siteUri;
                }

                // Update user email if available
                if (!string.IsNullOrEmpty(profile.Account.Email))
                {
                    user.Email = profile.Account.Email;
                    user.NormalizedEmail = _userManager.NormalizeEmail(profile.Account.Email);
                    await _userManager.UpdateAsync(user).ConfigureAwait(true);
                    _logger.LogInformation("[ChurchProfile-Sync] Updated user email from ChurchSuite profile");
                }

                await _context.SaveChangesAsync().ConfigureAwait(true);

                StatusMessage = $"✓ Successfully synced from ChurchSuite! Updated: {CurrentChurch.Name}";
                _logger.LogInformation("[ChurchProfile-Sync] Successfully synced ChurchSuite profile for user {UserId}.", user.Id);

                return RedirectToPage();
            }
            catch (HttpRequestException ex)
            {
                StatusMessage = "Error: Unable to connect to ChurchSuite. Please check your internet connection and try again.";
                _logger.LogError(ex, "[ChurchProfile-Sync] HTTP error while syncing ChurchSuite profile for user {UserId}.", _userManager.GetUserId(User));
                return Page();
            }
            catch (Exception ex)
            {
                StatusMessage = "Error: An unexpected error occurred while syncing your ChurchSuite profile.";
                _logger.LogError(ex, "[ChurchProfile-Sync] Unexpected error while syncing ChurchSuite profile for user {UserId}.", _userManager.GetUserId(User));
                return Page();
            }
        }
    }
}
