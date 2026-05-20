using ForestChurches.Components.Permissions;
using ForestChurches.Components.Roles;
using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace ForestChurches.Components.Permissions.Logic
{
    public class PermissionController : PermissionsInterface
    {
        private readonly ForestChurchesContext _context;
        private readonly RoleManager<ChurchRoles> _roleManager;

        public PermissionController(ForestChurchesContext context, RoleManager<ChurchRoles> roleManager)
        {
            _context = context;
            _roleManager = roleManager;
        }

        public async Task AddPermissionsAsync()
        {
            List<string> appPermissions = new();

            var baseType = typeof(AppPermissions);
            var assembly = baseType.Assembly;

            var nestedTypes = baseType.GetNestedTypes(BindingFlags.Public); // Retrieve all public nested types

            foreach (var nestedType in nestedTypes)
            {
                var permissions = nestedType.GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Where(f => f.FieldType == typeof(string))
                    .Select(f => (string)f.GetValue(null));

                appPermissions.AddRange(permissions);
            }

            // Retrieve all existing permissions asynchronously
            var permissionsInDatabase = await _context.Permissions.ToListAsync();

            foreach (var appPermissionName in appPermissions)
            {
                // Check if permission already exists
                if (permissionsInDatabase.All(p => p.Name != appPermissionName))
                {
                    // Add new permission
                    _context.Permissions.Add(new Permission
                    {
                        Name = appPermissionName,
                        Id = Guid.NewGuid().ToString(),
                        Description = appPermissionName
                    });
                }
            }

            // Save changes asynchronously
            await _context.SaveChangesAsync();
        }

    }
}
