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

    public DbSet<Benefice> Benefices { get; set; }

    public DbSet<ServiceDate> ServiceDates { get; set; }

    // ChurchSuite OAuth credentials
    public DbSet<ChurchSuiteCredential> ChurchSuiteCredentials { get; set; }

    // Logs
    public DbSet<Models.LogEntry> system_logs { get; set; }

    // Bulletins (Weekly Pew Sheet & Benefice Bulletin)
    public DbSet<ForestChurches.Models.Bulletin> Bulletins { get; set; }

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

        builder.Entity<ChurchSuiteCredential>()
            .HasKey(pk => pk.Id);

        builder.Entity<ChurchSuiteCredential>()
            .HasIndex(c => new { c.UserId, c.Subdomain })
            .IsUnique();

        // Third-party credentials are encrypted at rest (see Components/Security/ProtectedColumn.cs).
        // The columns are longtext, so the longer ciphertext fits without a migration.
        builder.Entity<ChurchSuiteCredential>().Property(c => c.AccessToken).HasConversion(ForestChurches.Components.Security.ProtectedColumn.Converter);
        builder.Entity<ChurchSuiteCredential>().Property(c => c.RefreshToken).HasConversion(ForestChurches.Components.Security.ProtectedColumn.Converter);
        builder.Entity<ChurchSuiteCredential>().Property(c => c.TempCodeVerifier).HasConversion(ForestChurches.Components.Security.ProtectedColumn.Converter);
        builder.Entity<ChurchInformation>().Property(c => c.ChurchSuiteApiKey).HasConversion(ForestChurches.Components.Security.ProtectedColumn.Converter);

        builder.Entity<Models.LogEntry>()
            .HasKey(pk => pk.Id);

        // Bulletin entity mapping
        builder.Entity<ForestChurches.Models.Bulletin>()
            .HasKey(b => b.Id);


        builder.Entity<Models.LogEntry>()
            .Property(a => a.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("(UUID())");

        builder.Entity<Models.LogEntry>()
            .Property(a => a.Exception)
            .IsRequired(false);

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

        // An account manages one or more churches (a benefice can have several).
        builder.Entity<ChurchAccount>()
            .HasMany(ca => ca.Churches)
            .WithOne(ci => ci.ChurchAccount)
            .HasForeignKey(ci => ci.ChurchAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ChurchInformation>()
            .HasIndex(ci => ci.ChurchAccountId);

        // A church can belong to a benefice. Deleting a benefice leaves its churches in place.
        builder.Entity<Benefice>()
            .HasMany(b => b.Churches)
            .WithOne(ci => ci.Benefice)
            .HasForeignKey(ci => ci.BeneficeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Benefice>()
            .HasIndex(b => b.Name);

        // Services belong to a church...
        builder.Entity<ChurchInformation>()
            .HasMany(ci => ci.ServiceTimes)
            .WithOne(st => st.ChurchInformation)
            .HasForeignKey(st => st.ChurchInformationId)
            .OnDelete(DeleteBehavior.Cascade);

        // ...and can be held at another church on the same account. If that church is deleted, the
        // service simply falls back to its own church.
        builder.Entity<ServiceTimes>()
            .HasOne(st => st.LocationChurch)
            .WithMany()
            .HasForeignKey(st => st.LocationChurchId)
            .OnDelete(DeleteBehavior.SetNull);

        // Defaults for existing rows: every Sunday, weekly, at the church itself.
        builder.Entity<ServiceTimes>().Property(st => st.RepeatType).HasDefaultValue(ServiceRepeat.Weekly);
        builder.Entity<ServiceTimes>().Property(st => st.Interval).HasDefaultValue(1);
        builder.Entity<ServiceTimes>().Property(st => st.DayOfWeek).HasDefaultValue(0);
        builder.Entity<ServiceTimes>().Property(st => st.UseCustomLocation).HasDefaultValue(false);

        // Specific dates for "custom dates" services.
        builder.Entity<ServiceDate>()
            .HasOne(d => d.Service)
            .WithMany(st => st.Dates)
            .HasForeignKey(d => d.ServiceTimesId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ServiceDate>()
            .HasIndex(d => new { d.ServiceTimesId, d.Date });
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
    }
}