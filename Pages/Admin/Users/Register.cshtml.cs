using ForestChurches.Components.Email;
using ForestChurches.Components.ImageHandler;
using ForestChurches.Components.Roles;
using ForestChurches.Components.Users;
using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace ForestChurches.Pages.Admin.Users
{
    [Authorize(Policy = "UserManagement.Write")]
    public class RegisterModel : PageModel
    {
        private readonly ForestChurches.Components.Storage.IBlobUrlService _blobUrlService;
        private readonly SignInManager<ChurchAccount> _signInManager;
        private readonly UserManager<ChurchAccount> _userManager;
        private readonly IUserStore<ChurchAccount> _userStore;
        private readonly ForestChurches.Components.Security.Encoder _Encoder;
        private readonly IUserEmailStore<ChurchAccount> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private RoleManager<ChurchRoles> _roleManager;
        private readonly iMailSender _emailSender;
        private ImageInterface _iImage;
        private readonly ForestChurchesContext _context;

        [BindProperty]
        public Registration Input { get; set; }

        public List<ChurchRoles> Roles { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public RegisterModel(
            ForestChurches.Components.Storage.IBlobUrlService blobUrlService,
            UserManager<ChurchAccount> userManager,
            IUserStore<ChurchAccount> userStore,
            SignInManager<ChurchAccount> signInManager,
            ILogger<RegisterModel> logger,
            iMailSender emailSender,
            ImageInterface iImage,
            RoleManager<ChurchRoles> roleManager,
            ForestChurchesContext context,
            Components.Security.Encoder encoder)
        {
            _blobUrlService = blobUrlService;
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _iImage = iImage;
            _roleManager = roleManager;
            _context = context;
            _Encoder = encoder;

            Input = new Registration();
            _Encoder = encoder;
        }

        public async Task OnGet(string username)
        {
            Input.Email = username;
            Roles = await _roleManager.Roles.ToListAsync() ?? null;
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostRegisterUser(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            var passwordValidator = new PasswordValidator<ChurchAccount>();
            var passwordResult = await passwordValidator.ValidateAsync(_userManager, null, Input.Password);

            if (!ModelState.IsValid)
            {
                LogModelStateErrors();
                foreach (var error in ModelState.Values)
                {
                    foreach (var e in error.Errors)
                    {
                        _logger.LogError(e.ToString());
                    }
                }
                Roles = await _roleManager.Roles.ToListAsync();
                return Page();
            }

            if (!passwordResult.Succeeded)
            {
                StatusMessage = "Password does not meet the minimum requirements. Password Requirements\n\n* At least 1 numeric value\n* At least 1 symbol (NonAlphanumeric)\n* Minimum length 12 \n* The use of both upper and lower case";
                Roles = await _roleManager.Roles.ToListAsync();
                return Page();
            }

            var user = await CreateUserAsync();
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "User creation failed.");
                Roles = await _roleManager.Roles.ToListAsync();
                return Page();
            }

            var result = await _userManager.CreateAsync(user, Input.Password);
            if (!result.Succeeded)
            {
                AddErrorsToModelState(result.Errors);
                Roles = await _roleManager.Roles.ToListAsync();
                return Page();
            }

            _logger.LogInformation("User created a new account with password.");


            // Unprotect role names before adding them to the user
            var unprotectedRoles = Input.Roles.Select(role => _Encoder.Unprotect<string>(role, "fc.data.admin.users")).ToList();
            var roleResult = await _userManager.AddToRolesAsync(user, unprotectedRoles);
            if (!roleResult.Succeeded)
            {
                AddErrorsToModelState(roleResult.Errors);
                Roles = await _roleManager.Roles.ToListAsync();
                return Page();
            }

            _logger.LogInformation("User added to roles successfully.");

            // Confirm the email address on creation of Admin User & Send welcome email to user
            user.EmailConfirmed = true;
            _emailSender.SendEmailRegisteredUser(Input.Email, Input.Email, "/Account");

            await SaveChurchInformationAsync(user);

            return RedirectToPage("/Admin/Users/Index");
        }

        private async Task<ChurchAccount> CreateUserAsync()
        {
            var user = new ChurchAccount()
            {
                Id = Guid.NewGuid().ToString(),
                UserName = Input.Email,
                Email = Input.Email,
                CreatedDate = DateTime.UtcNow.ToString(),
                ChurchInformation = null
            };

            user.PasswordHash = new PasswordHasher<ChurchAccount>().HashPassword(user, Input.Password);
            user.ImageArray = await GetImageArrayAsync(Input.Image);

            await _context.SaveChangesAsync();

            return user;
        }

        private async Task<byte[]> GetImageArrayAsync(IFormFile image)
        {
            if (image != null)
            {
                return await _iImage.ConvertToByteArray(image);
            }

            _logger.LogWarning("No profile image provided. Switching to default image...");
            // IMGUR-1

            var sasUrl = _blobUrlService.GetReadSasUrl("IMGUR-1.png", TimeSpan.FromDays(1));
            return await _iImage.ConvertToByteArrayFromUrl(sasUrl);
        }

        private async Task SaveChurchInformationAsync(ChurchAccount user)
        {
            var churchInformation = new ChurchInformation();
            user.ChurchInformation = churchInformation;

            _context.ChurchInformation.Add(churchInformation);
            await _context.SaveChangesAsync();
        }

        private void LogModelStateErrors()
        {
            foreach (var state in ModelState)
            {
                foreach (var error in state.Value.Errors)
                {
                    _logger.LogError($"Property: {state.Key}, Error: {error.ErrorMessage}");
                }
            }
        }

        private void AddErrorsToModelState(IEnumerable<IdentityError> errors)
        {
            foreach (var error in errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
                StatusMessage = "ERROR: " + error.Description;
            }
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
    }
}
