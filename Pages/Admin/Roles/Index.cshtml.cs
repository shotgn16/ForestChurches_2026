using ForestChurches.Components.Roles;
using ForestChurches.Components.Users;
using ForestChurches.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ForestChurches.Pages.Admin.Roles
{
    [Authorize(Policy = "RoleManagement.Read")]
    public class IndexModel : PageModel
    {
        private readonly RoleManager<ChurchRoles> _roleManager;
        private readonly UserManager<ChurchAccount> _userManager;
        private readonly ILogger<IndexModel> _logger;
        private readonly ForestChurches.Components.Security.Encoder _Encoder;

        public IList<ChurchRoles> Roles;

        [TempData]
        public string StatusMessage { get; set; }

        public IndexModel(RoleManager<ChurchRoles> roleManager, ILogger<IndexModel> logger, UserManager<ChurchAccount> userManager, Components.Security.Encoder Encoder)
        {
            _roleManager = roleManager;
            _userManager = userManager;
            _logger = logger;
            _Encoder = Encoder;
        }

        public void OnGet()
        {
            Roles = GetRoles().Result;
        }

        public async Task<IList<ChurchRoles>> GetRoles()
        {
            var roles = await _roleManager.Roles
                .Include(r => r.RolePermissions)
                    .ToListAsync()
                        .ConfigureAwait(true);

            return roles;
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostCreateRole(string roleName)
        {
            IdentityResult actionResult = new();

            try
            {
                if (!ModelState.IsValid)
                {
                    StatusMessage = "Invalid input model...";
                    return Page();
                }

                else if (ModelState.IsValid)
                {
                    bool x = await _roleManager.RoleExistsAsync(roleName)
                    .ConfigureAwait(true);

                    if (!x)
                    {
                        var role = new ChurchRoles() 
                        {
                            Name = roleName
                        };
                        actionResult = await _roleManager.CreateAsync(role).ConfigureAwait(true);
                    }

                    Roles = await GetRoles().ConfigureAwait(true);
                }
            }

            catch (Exception ex)
            {
                _logger.LogError("Error! " + ex.Message, ex);
            }

            StatusMessage = actionResult.Succeeded == true ? "Role successfully created!" : "An error occurred while creating the role.";
            return Page();
        }

        [ValidateAntiForgeryToken]
        public IActionResult OnPostDeleteRole(string value)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    StatusMessage = "Invalid input model...";
                    return Page();
                }

                else if (ModelState.IsValid)
                {
                    var role = _roleManager.FindByIdAsync(_Encoder.Unprotect<string>(value, "fc.data.admin.role")).Result;
                    var result = _roleManager.DeleteAsync(role).Result;

                    var usersInRole = _userManager.GetUsersInRoleAsync(role.Name).Result;

                    if (usersInRole.Count > 0)
                    {
                        RedirectToPage("./Index");
                        StatusMessage = $"Role '{role.Name}' is in use by {usersInRole.Count} user(s) and cannot be deleted...";
                    }

                    if (result.Succeeded)
                    {
                        _logger.LogInformation($"Role '{role.Name}' successfully deleted...");
                        StatusMessage = $"Role '{role.Name}' successfully deleted...";
                    }
                }
            }

            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
            }

            return RedirectToPage();
        }
    }
}
