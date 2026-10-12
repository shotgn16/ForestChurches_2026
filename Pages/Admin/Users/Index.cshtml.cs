using ForestChurches.Components.Roles;
using ForestChurches.Components.Users;
using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace ForestChurches.Pages.Admin.Users
{
    [Authorize(Policy = "UserManagement.Read")]
    public class IndexModel : PageModel
    {
        private readonly UserManager<ChurchAccount> _userManager;
        private readonly RoleManager<ChurchRoles> _roleManager;
        private readonly ForestChurchesContext _context;
        private readonly ILogger<IndexModel> _logger;

        private readonly ForestChurches.Components.Security.Encoder _Encoder;

        public List<ChurchAccount> Users { get; private set; }
        public Dictionary<string, List<string>> UserRoles { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public IndexModel(
            ForestChurchesContext context,
            UserManager<ChurchAccount> userManager,
            RoleManager<ChurchRoles> roleManager,
            ILogger<IndexModel> logger,
            ForestChurches.Components.Security.Encoder Encoder)
        {
            _context = context;
            _Encoder = Encoder;
            _logger = logger;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<IActionResult> OnGet()
        {
            Users = _userManager.Users.ToList();
            UserRoles = new Dictionary<string, List<string>>();

            foreach (var user in Users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                UserRoles[user.Id] = roles.ToList();
            }

            return Page();
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostDeleteUser(string value)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    _logger.LogError("Invalid model input...");
                    StatusMessage = "Invalid model input...";
                    return Page();
                }

                else if (ModelState.IsValid)
                {
                    var currentUser = await _userManager.GetUserAsync(User);
                    var isInRole = _userManager.IsInRoleAsync(currentUser, "SuperAdmin").Result;

                    if (!isInRole)
                    {
                        return Forbid("You do not have permission to delete this user...");
                    }

                    var userToDelete = await _context.Users.FirstOrDefaultAsync(a => a.Id == _Encoder.Unprotect<string>(value, "fc.data.admin.users"));
                    if (userToDelete == null)
                    {
                        _logger.LogError($"User ({value}) not found...");
                        StatusMessage = "Error: that user no longer exists.";
                        return RedirectToPage();
                    }

                    _context.Users.Remove(userToDelete);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"User ({value}) successfully deleted");
                    StatusMessage = $"User {userToDelete.UserName} was deleted.";
                }
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user...");
                StatusMessage = "Error: the user could not be deleted. Details have been logged.";
            }

            return RedirectToPage();
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostDownloadUser(string value)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    StatusMessage = "Error! The data model is an invalid format";
                    return Page();
                }

                var userId = _Encoder.Unprotect<string>(value, "fc.data.admin.users");
                var userToDownload = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

                if (userToDownload == null)
                {
                    _logger.LogError($"User ({userId}) not found...");
                    StatusMessage = "Error: that user no longer exists.";
                    return RedirectToPage();
                }

                var userData = new
                {
                    userToDownload.UserName,
                    userToDownload.Email,
                    userToDownload.PhoneNumber,
                    userToDownload.EmailConfirmed,
                    userToDownload.ImageArray,
                    Roles = await _userManager.GetRolesAsync(userToDownload)
                };

                var settings = new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    NullValueHandling = NullValueHandling.Ignore
                };

                var userToDownloadJson = JsonConvert.SerializeObject(userData, settings);
                var fileName = $"User-{userToDownload.UserName}.json";
                var fileBytes = System.Text.Encoding.UTF8.GetBytes(userToDownloadJson);
                return File(fileBytes, "application/json", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error downloading user: {ex.Message}");
                StatusMessage = "Error: the user's data could not be downloaded.";
            }

            return RedirectToPage();
        }
    }
}
