using ForestChurches.Components.Permissions;
using ForestChurches.Components.Users;
using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Security.Claims;

namespace ForestChurches.Components.Roles.logic
{
    public class RolesController : Controller, RolesInterface
    {
        private ForestChurchesContext _context;

        public RolesController(ForestChurchesContext context) => _context = context;

        public async Task SeedRolesAsync(UserManager<ChurchAccount> userManager, RoleManager<ChurchRoles> roleManager)
        {
            // METHOD GLOBAL VARIABLE
            List<string> RolePermissions;

            // Checking if SuperAdmin role exists...
            if (!await roleManager.RoleExistsAsync(Roles.SuperAdmin.ToString()))
            {
                await roleManager.CreateAsync(new ChurchRoles(Roles.SuperAdmin.ToString()));

                // Add Permissions to Super Admin
                RolePermissions = new();

                var baseType = typeof(AppPermissions);
                var assembly = baseType.Assembly;

                var nestedTypes = baseType.GetNestedTypes(BindingFlags.Public); // Retrieve all public nested types

                foreach (var nestedType in nestedTypes)
                {
                    var permissions = nestedType.GetFields(BindingFlags.Public | BindingFlags.Static)
                        .Where(f => f.FieldType == typeof(string))
                        .Select(f => (string)f.GetValue(null));

                    RolePermissions.AddRange(permissions);
                }

                var superAdminRole = await roleManager.FindByNameAsync(Roles.SuperAdmin.ToString());
                foreach (var permissionName in RolePermissions)
                {
                    var permission = await _context.Permissions.FirstOrDefaultAsync(p => p.Name == permissionName);
                    if (permission != null)
                    {
                        var rolePermission = new RolePermission
                        {
                            RoleId = superAdminRole.Id,
                            PermissionId = permission.Id,
                            PermissionName = permission.Name,
                        };

                        _context.RolePermissions.Add(rolePermission);
                    }
                }

                await _context.SaveChangesAsync();
            }

            // Checking if Admin role exists...
            if (!await roleManager.RoleExistsAsync(Roles.Admin.ToString()))
            {
                await roleManager.CreateAsync(new ChurchRoles(Roles.Admin.ToString()));

                RolePermissions = new();

                // Permissions for Admin Role
                var AdminPermissions = typeof(AppPermissions.Admin).GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Where(f => f.FieldType == typeof(string))
                    .Select(f => (string)f.GetValue(null));

                RolePermissions.AddRange(AdminPermissions);

                var AdminRole = await roleManager.FindByNameAsync(Roles.Admin.ToString());
                foreach (var permissionName in RolePermissions)
                {
                    var permission = await _context.Permissions.FirstOrDefaultAsync(p => p.Name == permissionName);
                    if (permission != null)
                    {
                        var rolePermission = new RolePermission
                        {
                            RoleId = AdminRole.Id,
                            PermissionId = permission.Id,
                            PermissionName = permission.Name,
                        };
                        _context.RolePermissions.Add(rolePermission);
                    }
                }

                await _context.SaveChangesAsync();
            }

            // Checking if AuthorizedChurch role exists...
            if (!await roleManager.RoleExistsAsync(Roles.AuthorizedChurch.ToString()))
            {
                await roleManager.CreateAsync(new ChurchRoles(Roles.AuthorizedChurch.ToString()));

                // Add Permissions to AuthorizedChurch Admin
                RolePermissions = new();

                // Directly access the EventManagement nested class
                var permissions = typeof(AppPermissions.UserEvents).GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Where(f => f.FieldType == typeof(string))
                    .Select(f => (string)f.GetValue(null));

                RolePermissions.AddRange(permissions);

                var authorizedChurchRole = await roleManager.FindByNameAsync(Roles.AuthorizedChurch.ToString());
                foreach (var permissionName in RolePermissions)
                {
                    var permission = await _context.Permissions.FirstOrDefaultAsync(p => p.Name == permissionName);
                    if (permission != null)
                    {
                        var rolePermission = new RolePermission
                        {
                            RoleId = authorizedChurchRole.Id,
                            PermissionId = permission.Id,
                            PermissionName = permission.Name,
                        };

                        _context.RolePermissions.Add(rolePermission);
                    }
                }

                await _context.SaveChangesAsync();
            }
        }
    }
}
