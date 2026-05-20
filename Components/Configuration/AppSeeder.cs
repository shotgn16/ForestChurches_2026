using ForestChurches.Components.Permissions.Logic;
using ForestChurches.Components.Roles;
using ForestChurches.Components.Roles.logic;
using ForestChurches.Components.Users;
using ForestChurches.Data;
using ForestChurches.Components.Storage;
using Microsoft.AspNetCore.Identity;
using System.Text;
using System.Text.Json;

namespace ForestChurches.Components.Configuration
{
    public class AppSeeder
    {
        private readonly UserInterface _userInterface;
        private readonly RolesInterface _rolesInterface;
        private readonly PermissionsInterface _permissionsInterface;

        private readonly UserManager<ChurchAccount> _userManager;
        private readonly RoleManager<ChurchRoles> _roleManager;
        private readonly ForestChurchesContext _context;
        private readonly ILogger<AppSeeder> _logger;
        private readonly Configuration _configuration;
        private readonly IBlobStorageService _blobStorageService;

        public AppSeeder(UserInterface userInterface, RolesInterface rolesInterface, ILogger<AppSeeder> logger, ForestChurchesContext context, PermissionsInterface permissionsInterface, UserManager<ChurchAccount> userManager, RoleManager<ChurchRoles> roleManager, Configuration configuration, IBlobStorageService blobStorageService)
        {
            _userInterface = userInterface;
            _rolesInterface = rolesInterface;
            _permissionsInterface = permissionsInterface;
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _blobStorageService = blobStorageService;
        }

        public void SeedData()
        {
            try
            {
                while (!_context.Database.CanConnect())
                {
                    _logger.LogInformation("Database not ready... Waiting...");
                    Thread.Sleep(5000); // Wait for 5 seconds before checking again
                }

                _logger.LogInformation("Database created successfully!");
                _logger.LogInformation("Starting to seed data...");

                _permissionsInterface.AddPermissionsAsync().Wait();
                _rolesInterface.SeedRolesAsync(_userManager, _roleManager).Wait();
                _userInterface.SeedSuperAdminAsync(_userManager, _roleManager).Wait();

                _logger.LogInformation("Seeding complete... No errors detected!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
        }
    }
}
