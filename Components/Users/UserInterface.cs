using ForestChurches.Components.Roles;
using Microsoft.AspNetCore.Identity;

namespace ForestChurches.Components.Users
{
    public interface UserInterface
    {
        Task SeedSuperAdminAsync(UserManager<ChurchAccount> userManager, RoleManager<ChurchRoles> roleManager);
    }
}
