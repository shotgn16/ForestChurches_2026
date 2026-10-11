// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using System.Diagnostics.CodeAnalysis;
using ForestChurches.Components.ImageHandler;
using ForestChurches.Components.Email;
using Newtonsoft.Json;
using ForestChurches.Components.Configuration;
using ForestChurches.Components.UserRegistration;
using ForestChurches.Components.Users;
using ForestChurches.Data;
using Microsoft.EntityFrameworkCore;

namespace ForestChurches.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<ChurchAccount> _signInManager;
        ForestChurches.Components.Storage.IBlobUrlService _blobUrlService;
        private readonly UserManager<ChurchAccount> _userManager;
        private readonly IUserStore<ChurchAccount> _userStore;
        private readonly IUserEmailStore<ChurchAccount> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;
        private readonly ForestChurches.Components.Security.Encoder _Encoder;
        private readonly ForestChurchesContext _context;
        private readonly iEmail _mailRepository;
        private readonly iMailSender _mailSender;
        internal readonly Configuration _configuration;
        private readonly iRegistrationGenerate _registrationGenerator;
        private readonly IChurchSuiteService _churchSuiteService;

        private ImageInterface _iImage;

        public RegisterModel(
            ForestChurches.Components.Storage.IBlobUrlService blobUrlService,
            UserManager<ChurchAccount> userManager,
            IUserStore<ChurchAccount> userStore,
            SignInManager<ChurchAccount> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            iMailSender mailSender,
            ImageInterface iImage,
            iEmail mailRepository,
            ForestChurchesContext context,
            Configuration configuration,
            iRegistrationGenerate registrationGenerator,
            Components.Security.Encoder Encoder,
            IChurchSuiteService churchSuiteService)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _mailSender = mailSender;
            _emailSender = emailSender;
            _iImage = iImage;
            _mailRepository = mailRepository;
            _configuration = configuration;
            _configuration = configuration;
            _blobUrlService = blobUrlService;
            _context = context;
            _registrationGenerator = registrationGenerator;
            _Encoder = Encoder;
            _churchSuiteService = churchSuiteService;
        }

        [TempData]
        public string StatusMessage { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string ReturnUrl { get; set; }
        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public List<string> whitelistedUsers { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public IList<AuthenticationScheme> ExternalLogins { get; set; }
        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>        
        public bool isTokenValid { get; set; } = true;
        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }

            // Custom Parameters
            [AllowNull]
            [PersonalData]
            public byte[] ImageArray { get; set; }

            [AllowNull]
            [PersonalData]
            public IFormFile Image { get; set; }

            // ChurchSuite Integration Parameters (Optional)
            [AllowNull]
            [Display(Name = "ChurchSuite Account ID")]
            public string ChurchSuiteAccountId { get; set; }

            [AllowNull]
            [DataType(DataType.Password)]
            [Display(Name = "ChurchSuite API Key")]
            public string ChurchSuiteApiKey { get; set; }

            // Per-user client id (single per user)
            [AllowNull]
            [Display(Name = "Client ID")]
            public string ClientId { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null, string email = "")
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            Input = new InputModel();
            Input.Email = email;

            // If an existing identity user exists for this email, prefill the ClientId so the UI can render it readonly
            if (!string.IsNullOrWhiteSpace(email))
            {
                var existingUser = await _userManager.FindByEmailAsync(email);
                if (existingUser != null && !string.IsNullOrWhiteSpace(existingUser.ClientId))
                {
                    Input.ClientId = existingUser.ClientId;
                }
            }
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            try
            {
                returnUrl = _Encoder.HtmlDecode(returnUrl);
                whitelistedUsers = await _context.WhitelistedUsers.Select(a => a.Email).ToListAsync();

                returnUrl ??= Url.Content("~/");
                ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
                if (ModelState.IsValid && _registrationGenerator.ValidateEmail(Input.Email) == true && whitelistedUsers.Contains(Input.Email))
                {
                    var passwordValidator = new PasswordValidator<ChurchAccount>();
                    var passwordResult = await passwordValidator.ValidateAsync(_userManager, null, Input.Password);

                    if (!passwordResult.Succeeded)
                    {
                        StatusMessage = "Password does not meet the minimum requirements. " +
                            "Password Requirements\r\n* At least 1 numeric value\r\n* At least 1 symbol (NonAlphanumeric)\r\n* Minimum length 12 \r\n* The use of both upper and lower case";
                        return Page();
                    }

                    var user = CreateUser();

                    if (user.Id == Guid.Empty.ToString() || string.IsNullOrEmpty(user.Id))
                    {
                        user.Id = Guid.NewGuid().ToString();
                    }

                    user.CreatedDate = DateTime.UtcNow.ToString();

                    // Store provided ClientId on the identity user if present and user does not already have one set
                    if (!string.IsNullOrWhiteSpace(Input.ClientId) && string.IsNullOrWhiteSpace(user.ClientId))
                    {
                        user.ClientId = Input.ClientId;
                    }

                    if (Input.Image == null)
                    {
                        // IMGUR-1
                        var sasUrl = _blobUrlService.GetReadSasUrl("IMGUR-1.png", TimeSpan.FromDays(1));
                        user.ImageArray = await _iImage.ConvertToByteArrayFromUrl(sasUrl);
                    }

                    else if (Input.Image != null)
                    {
                        user.ImageArray = await _iImage.ConvertToByteArray(Input.Image);
                    }

                    // Defer creation of ChurchInformation until after the Identity user is created so it can be linked by user.Id

                    await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
                    await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);
                    var result = await _userManager.CreateAsync(user, Input.Password);

                    if (result.Succeeded)
                    {
                        _logger.LogInformation("User created a new account with password.");
                        StatusMessage = $"User '{user.UserName}' successfully registered!";

                        // Create and persist ChurchInformation linked to the newly created Identity user
                        var churchInfo = new Models.ChurchInformation
                        {
                            ChurchAccountId = user.Id,
                            ChurchAccount = user,
                            Name = "Default Church",
                            Description = "Some church description. (This is not an actual church.)",
                            Status = "Open",
                            Denomination = "Anglican",
                            Congregation = "20-50",
                            Address = "TBA",
                            Phone = "000-000-0000",
                            Churchsuite = Uri.TryCreate(!string.IsNullOrEmpty(Input.ChurchSuiteAccountId) ? $"https://{Input.ChurchSuiteAccountId}.churchsuite.com/" : "https://churchsuite.com/", UriKind.Absolute, out var csUri) ? csUri : new Uri("https://churchsuite.com/"),
                            Website = new Uri("https://forestchurches.co.uk/"),
                            Parking = true,
                            Restrooms = true,
                            WheelchairAccess = true,
                            Wifi = true,
                            Refreshments = true,
                            WorshipHall = true,
                            Childcare = true,
                            MeetingHall = true,
                            Kitchen = true,
                            OutdoorSpaces = true,
                            HearingAidSupport = true,
                            ChurchSuiteApiKey = string.IsNullOrEmpty(Input.ChurchSuiteApiKey) ? null : Input.ChurchSuiteApiKey,
                            ChurchSuiteSubdomain = string.IsNullOrEmpty(Input.ChurchSuiteAccountId) ? null : Input.ChurchSuiteAccountId
                        };

                        if (!string.IsNullOrEmpty(Input.ChurchSuiteAccountId) && !string.IsNullOrEmpty(Input.ChurchSuiteApiKey))
                        {
                            try
                            {
                                var profileResponse = await _churchSuiteService.GetProfileAsync(Input.ChurchSuiteAccountId, Input.ChurchSuiteApiKey);
                                if (profileResponse?.Account != null)
                                {
                                    if (!string.IsNullOrEmpty(profileResponse.Account.Name))
                                    {
                                        churchInfo.Name = profileResponse.Account.Name;
                                    }
                                    if (!string.IsNullOrEmpty(profileResponse.Account.Url) && Uri.TryCreate(profileResponse.Account.Url, UriKind.Absolute, out var websiteUri))
                                    {
                                        churchInfo.Website = websiteUri;
                                    }
                                    if (!string.IsNullOrEmpty(profileResponse.Account.Phone))
                                    {
                                        churchInfo.Phone = profileResponse.Account.Phone;
                                    }
                                    if (!string.IsNullOrEmpty(profileResponse.Account.Description))
                                    {
                                        churchInfo.Description = profileResponse.Account.Description;
                                    }
                                    if (!string.IsNullOrEmpty(profileResponse.Account.Subdomain))
                                    {
                                        churchInfo.Churchsuite = new Uri("https://" + profileResponse.Account.Subdomain + ".churchsuite.com");
                                    }

                                    // Persist churchsuite API details for future syncs
                                    churchInfo.ChurchSuiteApiKey = Input.ChurchSuiteApiKey;
                                    churchInfo.ChurchSuiteSubdomain = Input.ChurchSuiteAccountId;

                                    // If ChurchSuite provides a canonical URL for the account, prefer it for the Churchsuite link
                                    if (!string.IsNullOrEmpty(profileResponse.Account.Url) && Uri.TryCreate(profileResponse.Account.Url, UriKind.Absolute, out var csCanonical))
                                    {
                                        churchInfo.Churchsuite = csCanonical;
                                    }

                                    _logger.LogInformation("Successfully retrieved ChurchSuite profile for user {UserId}.", user.Id);

                                    // If the ChurchSuite profile contains an email, update the user's email on the identity record
                                    if (!string.IsNullOrEmpty(profileResponse.Account.Email))
                                    {
                                        try
                                        {
                                            await _emailStore.SetEmailAsync(user, profileResponse.Account.Email, CancellationToken.None);
                                            await _userManager.UpdateAsync(user);
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogWarning(ex, "Failed to update user email from ChurchSuite for user {UserId}.", user.Id);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to fetch ChurchSuite profile for user {UserId}. User registration continues without ChurchSuite data.", user.Id);
                                // Don't fail registration if ChurchSuite API fails - just log the warning
                            }
                        }

                        // Persist the ChurchInformation row linked to this user
                        bool churchCreatedSuccessfully = false;
                        try
                        {
                            await _context.ChurchInformation.AddAsync(churchInfo);
                            await _context.SaveChangesAsync();
                            _logger.LogInformation("Created ChurchInformation for user {UserId} with Church ID {ChurchId}.", user.Id, churchInfo.ID);
                            churchCreatedSuccessfully = true;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to create ChurchInformation for user {UserId}. Attempting to delete created user account.", user.Id);

                            // Attempt to delete the user since we couldn't create the required church information
                            try
                            {
                                await _userManager.DeleteAsync(user);
                                _logger.LogInformation("Deleted user {UserId} due to church creation failure.", user.Id);
                            }
                            catch (Exception deleteEx)
                            {
                                _logger.LogError(deleteEx, "Failed to delete user {UserId} after church creation failure.", user.Id);
                            }

                            ModelState.AddModelError(string.Empty, "An error occurred while setting up your church profile. Please try registering again.");
                            return Page();
                        }

                        if (!churchCreatedSuccessfully)
                        {
                            ModelState.AddModelError(string.Empty, "Failed to create church profile. Please try again.");
                            return Page();
                        }

                        var userId = await _userManager.GetUserIdAsync(user);
                        var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                        var callbackUrl = Url.Page(
                            "/Account/ConfirmEmail",
                            pageHandler: null,
                            values: new { area = "Identity", userId = _Encoder.Protect(userId, "fc.data.url.email"), code = _Encoder.Protect(code, "fc.data.url.email"), returnUrl = returnUrl },
                            protocol: Request.Scheme);

                        // If the user has a ClientId set on the identity record, persist it explicitly now
                        if (!string.IsNullOrWhiteSpace(user.ClientId))
                        {
                            try
                            {
                                await _userManager.UpdateAsync(user);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to persist ClientId for user {UserId}", user.Id);
                            }
                        }

                        // Mark regisration as a success.
                        _registrationGenerator.MarkAsCompleted(Input.Email, DateTime.UtcNow);
                        _mailSender.SendEmailConfirmAccount(Input.Email, callbackUrl);

                        if (_userManager.Options.SignIn.RequireConfirmedAccount)
                        {
                            return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl = returnUrl });
                        }
                        else
                        {
                            await _signInManager.SignInAsync(user, isPersistent: false);
                            return LocalRedirect(returnUrl);
                        }
                    }
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }

                else if (_registrationGenerator.ValidateEmail(Input.Email) == false)
                {
                    ModelState.AddModelError("Email", "Email is already registered or the registration has expired...");
                    StatusMessage = "Error: Email is already registered or the registration has expired...";
                    return Page();
                }

                else if (!whitelistedUsers.Contains(Input.Email))
                {
                    throw new Exception("Error! Unknown email address. Please try again or contact us for further information using the contact forum or email us at 'info@forestchurches.co.uk'.");
                }
            }

            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());

                StatusMessage = ex.Message;
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }

        private ChurchAccount CreateUser()
        {
            try
            {
                return Activator.CreateInstance<ChurchAccount>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(ChurchAccount)}'. " +
                    $"Ensure that '{nameof(ChurchAccount)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                    $"override the register page in /Areas/Identity/Pages/Account/Register.cshtml");
            }
        }

        private IUserEmailStore<ChurchAccount> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<ChurchAccount>)_userStore;
        }

        private bool HashExpired(DateTime timestamp, int timeoutMinutes)
        {
            return DateTime.UtcNow > timestamp.AddMinutes(timeoutMinutes);
        }

    }
}
