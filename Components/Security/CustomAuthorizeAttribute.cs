using ForestChurches.Components.Users;
using ForestChurches.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

public class CustomAuthorizeAttribute : IAuthorizationRequirement
{
    public string Permission { get; }

    private PermissionAuthorizationHandler _permissionAuthorizationHandler;

    public CustomAuthorizeAttribute(string permission)
    {
        Permission = permission;
    }
}

public class PermissionAuthorizationHandler : AuthorizationHandler<CustomAuthorizeAttribute>
{
    private readonly ForestChurchesContext _context;
    private readonly UserManager<ChurchAccount> _userManager;

    public PermissionAuthorizationHandler(ForestChurchesContext context, UserManager<ChurchAccount> userManager)
    {
        _context = context;
        _userManager = userManager;

    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, CustomAuthorizeAttribute requirement)
    {
        var currentUser = await _userManager.GetUserAsync(context.User);
        if (currentUser == null)
        {
            return; // User not found, cannot proceed
        }

        // Get user's roles
        var userRoles = await _userManager.GetRolesAsync(currentUser);

        // Check if any of the user's roles have the required permission
        var hasPermission = await _context.RolePermissions
            .Include(rp => rp.Permission)
            .Include(rp => rp.Role)
            .AnyAsync(rp => userRoles.Contains(rp.Role.Name) && rp.Permission.Name.Equals(requirement.Permission, System.StringComparison.OrdinalIgnoreCase));

        if (hasPermission)
        {
            context.Succeed(requirement);
        }
    }
}
