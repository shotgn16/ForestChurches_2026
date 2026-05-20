using ForestChurches.Components.Users;
using Microsoft.AspNetCore.Identity;

namespace ForestChurches.Components.Roles.logic
{
    public interface RolesInterface
    {
        Task SeedRolesAsync(UserManager<ChurchAccount> userManager, RoleManager<ChurchRoles> roleManager);
    }
}
