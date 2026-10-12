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
            string unprotectedRoleId;
            try
            {
                unprotectedRoleId = _Encoder.Unprotect<string>(roleId, "fc.data.admin.role");
            }
            catch (Exception)
            {
                StatusMessage = "Error: that link is invalid or has expired.";
                return RedirectToPage("./Index");
            }

            Role = _roleManager.Roles
                .Include(r => r.RolePermissions)
                .FirstOrDefault(r => r.Id == unprotectedRoleId);

            if (Role == null)
            {
                StatusMessage = "Error: that role no longer exists.";
                return RedirectToPage("./Index");
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
                StatusMessage = "Error: that role no longer exists.";
                return RedirectToPage("./Index");
            }

            if (!ModelState.IsValid)
            {
                StatusMessage = "Error: the permissions couldn't be read. Please try again.";
                return RedirectToPage(new { roleId });
            }

            // Clear existing permissions
            var currentPermissions = _context.RolePermissions.Where(rp => rp.RoleId == unprotectedRoleId);
            _context.RolePermissions.RemoveRange(currentPermissions);

            // Add new permissions
            var newRolePermissions = new List<RolePermission>();
            foreach (var protectedPermissionId in SelectedPermissions ?? Array.Empty<string>())
            {
                var unprotectedPermissionId = _Encoder.Unprotect<string>(protectedPermissionId, "fc.data.admin.role");
                var permission = _context.Permissions.Find(unprotectedPermissionId);
                if (permission == null)
                {
                    StatusMessage = $"Error: permission '{unprotectedPermissionId}' wasn't found.";
                    return RedirectToPage(new { roleId });
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

            StatusMessage = $"Saved: permissions for {role.Name} were updated.";
            return RedirectToPage("./Index");
        }
    }
}
