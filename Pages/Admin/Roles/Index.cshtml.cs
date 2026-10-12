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
            roleName = roleName?.Trim();
            if (string.IsNullOrWhiteSpace(roleName))
            {
                StatusMessage = "Error: please enter a role name.";
                return RedirectToPage();
            }

            try
            {
                if (await _roleManager.RoleExistsAsync(roleName).ConfigureAwait(true))
                {
                    StatusMessage = $"Error: a role called '{roleName}' already exists.";
                    return RedirectToPage();
                }

                var result = await _roleManager.CreateAsync(new ChurchRoles { Name = roleName }).ConfigureAwait(true);
                StatusMessage = result.Succeeded
                    ? $"Saved: the role '{roleName}' was created."
                    : "Error: " + string.Join(" ", result.Errors.Select(e => e.Description));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating role {RoleName}", roleName);
                StatusMessage = "Error: the role couldn't be created.";
            }

            return RedirectToPage();
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostDeleteRole(string value)
        {
            try
            {
                var role = await _roleManager.FindByIdAsync(_Encoder.Unprotect<string>(value, "fc.data.admin.role")).ConfigureAwait(true);
                if (role == null)
                {
                    StatusMessage = "Error: that role no longer exists.";
                    return RedirectToPage();
                }

                // Check before deleting: previously the role was removed first and the check came too late.
                var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name).ConfigureAwait(true);
                if (usersInRole.Count > 0)
                {
                    StatusMessage = $"Error: the role '{role.Name}' is used by {usersInRole.Count} user(s), so it can't be deleted.";
                    return RedirectToPage();
                }

                var result = await _roleManager.DeleteAsync(role).ConfigureAwait(true);
                if (result.Succeeded)
                {
                    _logger.LogInformation("Role {RoleName} deleted", role.Name);
                    StatusMessage = $"Saved: the role '{role.Name}' was deleted.";
                }
                else
                {
                    StatusMessage = "Error: " + string.Join(" ", result.Errors.Select(e => e.Description));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting role");
                StatusMessage = "Error: the role couldn't be deleted.";
            }

            return RedirectToPage();
        }
    }
}
