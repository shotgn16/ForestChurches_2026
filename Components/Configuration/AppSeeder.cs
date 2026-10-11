using ForestChurches.Components.Permissions.Logic;
using ForestChurches.Components.Roles;
using ForestChurches.Components.Roles.logic;
using ForestChurches.Components.Users;
using ForestChurches.Data;
using ForestChurches.Components.Storage;
using Microsoft.EntityFrameworkCore;
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

                // Ensure bulletins table exists (non-invasive, uses IF NOT EXISTS)
                try
                {
                    var createSql = @"CREATE TABLE IF NOT EXISTS bulletins (
    id INT AUTO_INCREMENT PRIMARY KEY,
    title VARCHAR(255) NOT NULL DEFAULT 'Forest Churches Weekly Pew Sheet',
    parish_name VARCHAR(255) NOT NULL DEFAULT 'Forest of Dean Benefice',
    bulletin_date DATE NOT NULL,
    sunday_name VARCHAR(255) DEFAULT 'Sunday Gathering',
    collect_prayer TEXT,
    first_reading VARCHAR(255),
    gospel_reading VARCHAR(255),
    pastoral_note TEXT,
    hymns_list TEXT,
    notices_list TEXT,
    pdf_filename VARCHAR(255) DEFAULT NULL,
    is_active TINYINT(1) DEFAULT 1,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

                    _context.Database.ExecuteSqlRaw(createSql);
                    _logger.LogInformation("Ensured bulletins table exists.");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to ensure bulletins table exists.");
                }
                // Ensure the new 'type' column exists (safe no-op if already present).
                try
                {
                    var alterSql = "ALTER TABLE bulletins ADD COLUMN IF NOT EXISTS `type` VARCHAR(64) NOT NULL DEFAULT 'Bulletin';";
                    _context.Database.ExecuteSqlRaw(alterSql);
                    _logger.LogInformation("Ensured bulletins.type column exists.");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to ensure bulletins.type column exists (DB may not support IF NOT EXISTS ALTER TABLE). Please run migration to add the column.");
                }
                // Ensure the banner columns exist
                try
                {
                    var alterBanner1 = "ALTER TABLE bulletins ADD COLUMN IF NOT EXISTS `banner_visible` TINYINT(1) DEFAULT 0;";
                    var alterBanner2 = "ALTER TABLE bulletins ADD COLUMN IF NOT EXISTS `banner_expires_at` DATETIME NULL;";
                    _context.Database.ExecuteSqlRaw(alterBanner1);
                    _context.Database.ExecuteSqlRaw(alterBanner2);
                    _logger.LogInformation("Ensured bulletins.banner_visible and bulletins.banner_expires_at columns exist.");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to ensure bulletins banner columns exist. Consider adding them via migration.");
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
