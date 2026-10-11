using ForestChurches.Components.Roles;
using ForestChurches.Components.Users;
using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ForestChurches.Pages.Admin.Users
{
    [Authorize(Policy = "UserManagement.Edit")]
    public class EditModel : PageModel
    {
        private readonly ILogger<EditModel> _logger;
        private readonly ForestChurchesContext _context;
        private readonly UserManager<ChurchAccount> _userManager;
        private readonly RoleManager<ChurchRoles> _roleManager;
        private readonly ForestChurches.Components.Security.Encoder _Encoder;

        public EditModel(
            UserManager<ChurchAccount> userManager,
            ForestChurchesContext context,
            ILogger<EditModel> logger,
            RoleManager<ChurchRoles> roleManager,
            ForestChurches.Components.Security.Encoder Encoder)
        {
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
            _Encoder = Encoder ?? throw new ArgumentNullException(nameof(Encoder));
        }

        public ChurchAccount CurrentUser { get; set; }
        public IList<ChurchRoles> Roles { get; set; }

        [BindProperty]
        public Profile Input { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            try
            {
                var currentUser = _userManager.GetUserAsync(User);
                bool isInRole = await _userManager.IsInRoleAsync(currentUser.Result, "SuperAdmin");
                if (!isInRole)
                {
                    return Forbid("You do not have permission to edit this user...");
                }

                if (string.IsNullOrEmpty(id))
                {
                    return NotFound("User ID is null or empty.");
                }

                CurrentUser = await _userManager.FindByIdAsync(_Encoder.Unprotect<string>(id, "fc.data.admin.users"));
                if (CurrentUser == null)
                {
                    return NotFound($"Unable to load user with ID '{_Encoder.Unprotect<string>(id, "fc.data.admin.users")}'.");
                }

                var userRoles = await _userManager.GetRolesAsync(CurrentUser);
                Input = new Profile
                {
                    Email = CurrentUser.Email,
                    Phone = CurrentUser.PhoneNumber ?? string.Empty,
                    Roles = userRoles.ToList(),
                    IsActive = CurrentUser.EmailConfirmed
                };

                Roles = await _roleManager.Roles.ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while loading user information.");
                StatusMessage = "An error occurred while loading user information. Please try again later.";
            }

            return Page();
        }

        [ValidateAntiForgeryToken] // 29.01.2025 - Add ModelState Validation
        public async Task<IActionResult> OnPostUpdateUserAsync(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(_Encoder.Unprotect<string>(id, "fc.data.admin.users"));
                if (user == null)
                {
                    StatusMessage = $"Error: Unable to find user with ID '{_Encoder.Unprotect<string>(id, "fc.data.admin.users")}'.";
                    return RedirectToPage("./Index");
                }

                var userRoles = await _userManager.GetRolesAsync(user);
                await _userManager.RemoveFromRolesAsync(user, userRoles);

                if (Input.Roles != null)
                {
                    var unprotectedRoles = Input.Roles.Select(role => _Encoder.Unprotect<string>(role, "fc.data.admin.users")).ToList();
                    await _userManager.AddToRolesAsync(user, unprotectedRoles);
                    user.Role = string.Join(", ", unprotectedRoles);

                    // If roles were assigned, mark the account as confirmed so the user is registered/confirmed immediately.
                    try
                    {
                        if (unprotectedRoles.Any())
                        {
                            // Some versions of Identity may not expose SetEmailConfirmedAsync; set the flag and update the user instead.
                            user.EmailConfirmed = true;
                            var updateResult = await _userManager.UpdateAsync(user).ConfigureAwait(true);
                            if (updateResult.Succeeded)
                            {
                                _logger.LogInformation("Marked user {UserId} email as confirmed because roles were assigned.", user.Id);
                            }
                            else
                            {
                                _logger.LogWarning("Failed to update user {UserId} after setting EmailConfirmed: {Errors}", user.Id, string.Join("; ", updateResult.Errors.Select(e => e.Description)));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Exception while setting EmailConfirmed for user {UserId}", user.Id);
                    }
                }

                user.Email = Input.Email ?? string.Empty;
                user.PhoneNumber = Input.Phone ?? string.Empty;

                if (!string.IsNullOrEmpty(Input.Password))
                {
                    user.PasswordHash = new PasswordHasher<ChurchAccount>().HashPassword(user, Input.Password);
                }

                await _userManager.UpdateAsync(user);
                StatusMessage = $"User '{user.Email}' successfully updated.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating user information.");
                StatusMessage = "An error occurred while updating user information. Please try again later.";
            }

            return RedirectToPage("./Index");
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
