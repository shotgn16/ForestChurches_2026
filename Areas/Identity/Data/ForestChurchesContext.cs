using ForestChurches.Components.Roles;
using ForestChurches.Components.Users;
using ForestChurches.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace ForestChurches.Data;

public class ForestChurchesContext : IdentityDbContext<ChurchAccount, ChurchRoles, string>
{
    public ForestChurchesContext(DbContextOptions<ForestChurchesContext> options) : base(options)
    {
        try
        {
            var databaseCreator = Database.GetService<IDatabaseCreator>() as RelationalDatabaseCreator;
            if (databaseCreator != null && !databaseCreator.CanConnect())
            {
                databaseCreator.Create();
            }
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"Invalid Operation: {ex.Message}");
        }
        catch (DbUpdateException ex)
        {
            Console.WriteLine($"Database Update Exception: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"General Exception: {ex.Message}");
        }
    }
    public DbSet<ChurchAccount> ChurchAccount { get; set; }
    public DbSet<ChurchRoles> ChurchRoles { get; set; }
    public DbSet<EventsModel> Events { get; set; }

    // Service Times 
    public DbSet<ServiceTimes> ServiceTimes { get; set; }

    // Implementation of role permissions
    public DbSet<Permission> Permissions { get; set; }
    public DbSet<RolePermission> RolePermissions { get; set; }

    // Implementation of Church Information and Whitelisted users
    public DbSet<WhitelistedUsers> WhitelistedUsers { get; set; }
    public DbSet<ChurchInformation> ChurchInformation { get; set; }

    // Logs
    public DbSet<Models.LogEntry> system_logs { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        base.OnModelCreating(builder);

        builder.Entity<ServiceTimes>()
            .HasKey(pk => pk.Id);

        builder.Entity<EventsModel>()
            .HasKey(pk => pk.ID);

        builder.Entity<ChurchRoles>()
            .HasKey(pk => pk.Id);

        builder.Entity<ChurchAccount>()
            .Property(b => b.Role)
            .IsRequired(false);

        builder.Entity<WhitelistedUsers>()
            .HasKey(pk => pk.ID);

        builder.Entity<Models.LogEntry>()
            .HasKey(pk => pk.Id);

        builder.Entity<Models.LogEntry>()
            .Property(a => a.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("(UUID())");

        builder.Entity<Models.LogEntry>()
            .Property(a => a.Exception)
            .IsRequired(false);

        builder.Entity<Models.LogEntry>()
            .Property(a => a.Url)
            .HasDefaultValue("Url");

        builder.Entity<Models.LogEntry>()
            .Property(a => a.Action)
            .HasDefaultValue("Action");

        builder.Entity<Models.LogEntry>()
            .Property(a => a.Level)
            .HasDefaultValue("Level");

        builder.Entity<Models.LogEntry>()
            .Property(a => a.Message)
            .HasDefaultValue("Message");

        builder.Entity<Models.LogEntry>()
            .Property(a => a.Properties)
            .HasDefaultValue("Properties");

        builder.Entity<Models.LogEntry>()
            .Property(a => a.Template)
            .HasDefaultValue("Template");

        builder.Entity<Models.LogEntry>()
            .Property(a => a.Timestamp)
            .HasDefaultValue("Timestamp");

        // Implementation of role permissions
        builder.Entity<RolePermission>()
            .HasKey(rp => new { rp.RoleId, rp.PermissionId });

        builder.Entity<RolePermission>()
            .HasOne(rp => rp.Role)
            .WithMany(r => r.RolePermissions)
            .HasForeignKey(rp => rp.RoleId);

        builder.Entity<RolePermission>()
            .HasOne(rp => rp.Permission)
            .WithMany(p => p.RolePermissions)
            .HasForeignKey(rp => rp.PermissionId);

        // Assigning Church Information table to users table 
        builder.Entity<ChurchAccount>()
            .HasOne(ca => ca.ChurchInformation)
            .WithOne(ci => ci.ChurchAccount)
            .HasForeignKey<ChurchInformation>(ci => ci.ChurchAccountId);

        builder.Entity<ChurchInformation>()
            .HasIndex(ci => ci.ChurchAccountId)
            .IsUnique();

        // Assigning 'ServiceTimes' to 'ChurchInformation'
        builder.Entity<ChurchInformation>()
            .HasMany(ci => ci.ServiceTimes)
            .WithOne(st => st.ChurchInformation)
            .HasForeignKey(st => st.ChurchInformationId);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
    }
}