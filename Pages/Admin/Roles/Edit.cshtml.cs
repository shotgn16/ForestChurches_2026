using ForestChurches.Components.Roles;
using ForestChurches.Data;
using ForestChurches.Models;
using Hangfire.Storage.Monitoring;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Shared;
using System.Security.Claims;

namespace ForestChurches.Pages.Admin.Roles
{
    [Authorize(Policy = "RoleManagement.Write")]
    public class EditModel : PageModel
    {
        private readonly RoleManager<ChurchRoles> _roleManager;
        private ForestChurchesContext _context;
        private readonly ForestChurches.Components.Security.Encoder _Encoder;

        public ChurchRoles Role { get; set; }
        public List<Permission> AllPermissions { get; set; }
        
        [BindProperty]
        public string[] SelectedPermissions { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public EditModel(RoleManager<ChurchRoles> roleManager, ForestChurchesContext context, Components.Security.Encoder Encoder)
        {
            _roleManager = roleManager;
            _context = context;
            _Encoder = Encoder;
        }

        public IActionResult OnGet(string roleId)
        {
            Role = _roleManager.Roles
                .Include(r => r.RolePermissions)
                .FirstOrDefault(r => r.Id == _Encoder.Unprotect<string>(roleId, "fc.data.admin.role"));

            if (Role == null)
            {
                return NotFound($"Unable to load role with ID '{_Encoder.Unprotect<string>(roleId, "fc.data.admin.role")}'...");
            }

            // Fetch all permissions
            AllPermissions = _context.Permissions.ToList();

            return Page();
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostAsync(string roleId)
        {
            var unprotectedRoleId = _Encoder.Unprotect<string>(roleId, "fc.data.admin.role");

            var role = await _roleManager.FindByIdAsync(unprotectedRoleId);
            if (role == null)
            {
                StatusMessage = "Error: Role not found!";
                return Page();
            }

            if (!ModelState.IsValid)
            {
                StatusMessage = "Error: Invalid input model!";
                return Page();
            }

            // Clear existing permissions
            var currentPermissions = _context.RolePermissions.Where(rp => rp.RoleId == unprotectedRoleId);
            _context.RolePermissions.RemoveRange(currentPermissions);

            // Add new permissions
            var newRolePermissions = new List<RolePermission>();
            foreach (var protectedPermissionId in SelectedPermissions)
            {
                var unprotectedPermissionId = _Encoder.Unprotect<string>(protectedPermissionId, "fc.data.admin.role");
                var permission = _context.Permissions.Find(unprotectedPermissionId);
                if (permission == null)
                {
                    StatusMessage = $"Error: Permission with ID '{unprotectedPermissionId}' not found!";
                    return Page();
                }

                var rolePermission = new RolePermission
                {
                    RoleId = unprotectedRoleId,
                    PermissionId = unprotectedPermissionId,
                    PermissionName = permission.Name
                };
                newRolePermissions.Add(rolePermission);
            }
            _context.RolePermissions.AddRange(newRolePermissions);
            await _context.SaveChangesAsync();

            StatusMessage = $"Role ({role.Name}) successfully updated!";
            return RedirectToPage("./Index");
        }
    }
}
