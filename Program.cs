using Azure.Identity;
using Azure.Storage.Blobs;
using ElmahCore;
using ElmahCore.Mvc;
using ForestChurches;
using ForestChurches.Components.AutoEvents;
using ForestChurches.Components.Configuration;
using ForestChurches.Components.Logging;
using ForestChurches.Components.Roles;
using ForestChurches.Components.Security.Token;
using ForestChurches.Components.Support;
using ForestChurches.Data;
using Microsoft.AspNetCore.DataProtection;
using Hangfire;
using Hangfire.MySql;
using HangfireBasicAuthenticationFilter;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        string? connectionString = string.Empty;
        string? environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);


        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            configBuilder.AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true);
        }
        var config = configBuilder.Build();

        try
        {
            // Add services to the container
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.IsEssential = true;
            });

            builder.Services.AddControllersWithViews(options =>
            {
                options.Filters.Add(new Microsoft.AspNetCore.Mvc.ValidateAntiForgeryTokenAttribute());
                options.Filters.Add(typeof(ErrorController));
                options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
            }).AddSessionStateTempDataProvider();

            builder.Services.AddHttpLogging(x =>
            {
                x.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.All;
            });

            var secretClient = ConfigureAzureServices.ConfigureAzureKeyVault(builder, environment, config);

            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                connectionString = config["ConnectionStrings:Development"] ??
                    throw new InvalidOperationException("Cannot load connection string for development environment");
            }
            else if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production")
            {
                connectionString = secretClient.GetSecret("db-connection-live").Value.Value ??
                    throw new InvalidOperationException("Production connection string not found.");
            }

            // Move log builder to below DB because db is needed for writing logs to MariaDB.
            // If logging is configured before DB, it may throw errors when trying to log before DB is set up.
            LoggingConfiguration.ConfigureLogging(builder.Services, connectionString, secretClient.GetSecret("seq-key").Value.Value, secretClient.GetSecret("seq-link").Value.Value);
            builder.Host.UseSerilog();

            ContextConfiguration.ConfigureDatabaseContext(builder.Services, connectionString);
            PolicyConfiguration.AddPolicies(builder.Services);
            PolicyConfiguration.ConfigurePasswordPolicy(builder.Services);

            builder.Services.AddHttpContextAccessor();

            builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
                options.TokenLifespan = TimeSpan.FromHours(2));

            builder.Services.AddAntiforgery(options =>
            {
                options.Cookie.Name = "X-CSRF-TOKEN-COOKIE";
                options.HeaderName = "X-CSRF-TOKEN-HEADER";
            });

            // Persist data protection keys to Azure Blob Storage.
            // Previously the code protected keys with a local certificate (.pfx). Switch to storing keys in Blob Storage
            // and use Key Vault or platform protection if you require an additional layer of protection.
            // using Azure.Identity; // already present
            // using System;

            try
            {
                var blobConn = secretClient.GetSecret("blob-connection-string").Value.Value;
                var blobContainer = secretClient.GetSecret("blob-container-name").Value.Value;
                var blobFile = secretClient.GetSecret("blob-file-name").Value.Value;

                // Full Key Vault key identifier (store this in Key Vault or appsettings)
                // e.g. https://<vault>.vault.azure.net/keys/<keyName>/<version>
                var keyIdentifier = secretClient.GetSecret("data-key").Value?.Value;

                // Use EnvironmentCredential (reads AZURE_TENANT_ID, AZURE_CLIENT_ID, AZURE_CLIENT_SECRET)
                var keyVaultCredential = new EnvironmentCredential();

                // In Development keep keys local to avoid attempting to decrypt keys stored in production blob/keyvault
                if (string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
                {
                    var keysFolder = Path.Combine(Environment.CurrentDirectory, "DataProtectionKeys");
                    Directory.CreateDirectory(keysFolder);

                    builder.Services.AddDataProtection()
                        .PersistKeysToFileSystem(new System.IO.DirectoryInfo(keysFolder))
                        .SetDefaultKeyLifetime(TimeSpan.FromDays(7))
                        .SetApplicationName("ForestChurches-App");

                    Log.Information("DataProtection: Using file system key persistence for Development at {Path}", keysFolder);
                }
                else
                {
                    var dp = builder.Services.AddDataProtection()
                        .PersistKeysToAzureBlobStorage(blobConn, blobContainer, blobFile)
                        .SetDefaultKeyLifetime(TimeSpan.FromDays(7))
                        .SetApplicationName("ForestChurches-App");

                    // Protect with Key Vault only when a key identifier is present. If protection fails, fall back to unprotected blob keys
                    if (!string.IsNullOrWhiteSpace(keyIdentifier))
                    {
                        try
                        {
                            dp.ProtectKeysWithAzureKeyVault(new Uri(keyIdentifier), keyVaultCredential);
                            Log.Information("DataProtection: Configured Azure Key Vault protector.");
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "DataProtection: Failed to configure Key Vault protector. Continuing with Blob-only key persistence (keys may be unreadable if previously protected).");
                        }
                    }
                    else
                    {
                        Log.Warning("DataProtection: No Key Vault key identifier configured; using Blob-only key persistence.");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to configure Data Protection persistence to Azure Blob Storage / file system.");
            }
            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.SlidingExpiration = false;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            });

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowSpecificOrigin", builder => 
                {
                    builder.WithOrigins("https://forestchurches.co.uk")
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                });
            });

            // Add HSTS options
            builder.Services.AddHsts(options =>
            {
                options.MaxAge = TimeSpan.FromDays(365);
                options.IncludeSubDomains = true;
                options.Preload = true;
            });

            // Create the 'logs' directory if it dosen't already exist...
            if (!Directory.Exists(Environment.CurrentDirectory + "logs"))
            {
                Directory.CreateDirectory(Environment.CurrentDirectory + "logs");
            }

            // Configure ElmahCore to log to an XML file and restrict access to superUser role
            builder.Services.AddElmah<XmlFileErrorLog>(options =>
            {
                options.LogPath = "~/logs";
                options.OnPermissionCheck = context => context.User.Identity.IsAuthenticated && context.User.IsInRole(Roles.SuperAdmin.ToString());
                options.LogPath = Path.Combine(Environment.CurrentDirectory, "logs");
            });

            builder.Services.AddHangfire(configuration => configuration
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseStorage(
                    new MySqlStorage(
                        connectionString,
                        new MySqlStorageOptions
                        {
                            QueuePollInterval = TimeSpan.FromSeconds(15),
                            JobExpirationCheckInterval = TimeSpan.FromHours(1),
                            CountersAggregateInterval = TimeSpan.FromMinutes(5),
                            PrepareSchemaIfNecessary = true,
                            DashboardJobListLimit = 25000,
                            // Increase transaction timeout to reduce "Command Timeout expired" errors
                            // during longer Hangfire storage operations.
                            TransactionTimeout = TimeSpan.FromMinutes(5),
                            TablesPrefix = "Hangfire",
                        })
                    )
                );

            builder.Services.AddHangfireServer(options => options.WorkerCount = Math.Min(Environment.ProcessorCount * 5, 20));
            builder.Services.AddLogging();
            ServiceRegistration.RegisterServices(builder.Services);

            // Optionally register the image migration hosted service (runs only if config flag enabled)
            builder.Services.AddHostedService<ForestChurches.Tools.ImageMigrationHostedService>();


            BlobServiceClient blobServiceClient = new BlobServiceClient(
                secretClient.GetSecret("blob-connection-string").Value.Value);

            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(
                secretClient.GetSecret("blob-container-name").Value.Value);

            await containerClient.CreateIfNotExistsAsync();

            // Register BlobUrlService for generating SAS URLs to private image container
            var storageConnectionString = secretClient.GetSecret("blob-connection-string").Value.Value;
            var storageAccountName = secretClient.GetSecret("storage-account-name").Value.Value;
            var storageAccountKey = secretClient.GetSecret("storage-account-key").Value.Value;
            var imagesContainerName = secretClient.GetSecret("storage-container-name").Value.Value;

            // Register a singleton service that other parts of the app can inject
            builder.Services.AddSingleton<ForestChurches.Components.Storage.IBlobUrlService>(sp =>
            {
                return new ForestChurches.Components.Storage.BlobUrlService(storageConnectionString, storageAccountName, storageAccountKey, imagesContainerName);
            });

            // Register blob storage upload service for application data/images container
            builder.Services.AddSingleton<ForestChurches.Components.Storage.IBlobStorageService>(sp =>
            {
                return new ForestChurches.Components.Storage.BlobStorageService(storageConnectionString, imagesContainerName);
            });

            // DataProtection certificate-based protection removed. Keys persisted to Blob Storage earlier.

            var jwtSecret = secretClient.GetSecret("jwt-token-securityKey").Value.Value;
            var jwtIssuer = secretClient.GetSecret("jwt-token-issuer").Value.Value;
            var jwtAudience = secretClient.GetSecret("jwt-token-audience").Value.Value;

            // 3) Add JWT separately (Since Identity dosen't add JWT by default)
            builder.Services.AddAuthentication()
                .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.RequireHttpsMetadata = true;
                    options.SaveToken = true;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwtIssuer,
                        ValidateAudience = true,
                        ValidAudience = jwtAudience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromSeconds(30)
                    };

                    options.Events = new JwtBearerEvents
                    {
                        OnAuthenticationFailed = ctx =>
                        {
                            Log.Warning(ctx.Exception, "JWT authentication failed");
                            return Task.CompletedTask;
                        },
                        OnTokenValidated = ctx =>
                        {
                            Log.Debug("JWT token validated for {sub}", ctx.Principal?.Identity?.Name ?? "unknown");
                            return Task.CompletedTask;
                        }
                    };
                });

            // MiniProfiler

            //builder.Services.AddMiniProfiler(options =>
            //{
            //    options.RouteBasePath = "/profiler";
            //    options.ResultsAuthorize = request =>
            //        request.HttpContext.User.IsInRole(Roles.SuperAdmin.ToString());

            //}).AddEntityFramework();

            var app = builder.Build();

            // Configure the HTTP request pipeline
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts(); // Use HSTS without arguments
            }

            app.Use(async (context, next) =>
            {
                context.Response.Headers.Remove("Server");
                context.Response.Headers.Remove("X-Powered-By");
                context.Response.Headers.Add("Strict-Transport-Security", "max-age=31536000; includeSubDomains; preload");
                await next();
            });

            app.UseHttpsRedirection();

            // Apply Content Security Policy early so it covers static file responses (images, scripts, etc.)
            app.UseContentSecurityPolicy(options =>
            {
                options.ScriptHashes = secretClient.GetSecret("script-hash").Value.Value;
            });

            app.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = ctx =>
                {
                    if (ctx.File.Name.EndsWith(".js"))
                    {
                        ctx.Context.Response.Headers.Append("Content-Type", "application/javascript");
                    }
                }
            });

            app.UseSerilogRequestLogging();
            app.UseRouting();
            // Add MiniProfiler middleware here
            //app.UseMiniProfiler();
            app.UseSession(); // Ensure this is before UseAuthentication and UseAuthorization
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseElmah();

            // Configure headers & barred file types...
            app.Use(async (context, next) =>
            {
                var blockedExtensions = new[] { ".json", ".config", ".env", ".ini", ".exe", ".pfx", ".cshtml", ".cshtml.cs", ".cs", ".dll", ".user" };
                var allowedExtensions = new[] { ".css", ".js", ".png", ".jpg", ".jpeg", ".gif", ".svg", ".woff", ".woff2", ".ttf", ".eot" };

                if (context.Request.Path.HasValue && context.Request.Path.Value.Contains("."))
                {
                    var extension = System.IO.Path.GetExtension(context.Request.Path.Value);
                    var filePath = context.Request.Path.Value;

                    Log.Information("Download Requested: " +
                        "\nFile: {FilePath}" +
                        "\nExtension: {Extension}", filePath, extension);

                    if (filePath.Contains("support/SupportData-") && extension == ".zip")
                    {
                        Log.Information("Allowing access to support data file: {FilePath}", filePath);
                        await next();
                        return;
                    }

                    if (blockedExtensions.Any(ext => ext.Equals(extension, StringComparison.OrdinalIgnoreCase)) ||
                        !allowedExtensions.Contains(extension))
                    {
                        Log.Information("Blocking access to file: {FilePath}", filePath);
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsync("Access to file downloads is forbidden.");
                        return;
                    }
                }

                await next();
            });

            // Add Content Security Policy (CSP) middleware
            app.Use(async (context, next) =>
            {
                // Use a referrer policy that still sends an origin for cross-origin requests so
                // tile providers (for example OpenStreetMap) receive a Referer header as required
                // by their tile usage policy. Avoid "no-referrer" which strips the Referer header.
                context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
                context.Response.Headers.Add("X-Frame-Options", "SAMEORIGIN");
                await next();
            });

            app.Use(next => context =>
            {
                // Skip antiforgery validation for certain Identity endpoints (login/register/logout)
                var path = context.Request.Path;
                var isIdentityEndpoint = path.StartsWithSegments("/Identity/Account/Login", StringComparison.OrdinalIgnoreCase)
                                         || path.StartsWithSegments("/Identity/Account/Register", StringComparison.OrdinalIgnoreCase)
                                         || path.StartsWithSegments("/Identity/Account/Logout", StringComparison.OrdinalIgnoreCase);

                if ((string.Equals(context.Request.Method, "POST", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(context.Request.Method, "PUT", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(context.Request.Method, "DELETE", StringComparison.OrdinalIgnoreCase)) && !isIdentityEndpoint)
                {
                    var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
                    antiforgery.ValidateRequestAsync(context).Wait();
                }

                return next(context);
            });

            app.Use(async (context, next) =>
            {
                if (context.Request.Path.StartsWithSegments("/lib"))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden; // Forbidden
                    await context.Response.WriteAsync("Access Denied");
                    return;
                }

                await next();
            });

            app.UseHangfireDashboard("/hangfire", new DashboardOptions
            {
                DashboardTitle = "Forest Church Task Scheduler",
                Authorization = new[] { 
                    new HangfireCustomBasicAuthenticationFilter 
                    { 
                        User = secretClient.GetSecret("sys-admin-username").Value.Value, 
                        Pass = secretClient.GetSecret("sys-admin-password").Value.Value 
                    } 
                }
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");

                endpoints.MapRazorPages();
            });



            using (var scope = app.Services.CreateScope())
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

                try
                {
                    var db = scope.ServiceProvider.GetRequiredService<ForestChurchesContext>();
                    var appliedMigrations = db.Database.GetAppliedMigrations();
                    var allMigrations = db.Database.GetMigrations();
                    var pendingMigrations = db.Database.GetPendingMigrations();

                    logger.LogInformation("Applied Migrations: {AppliedMigrations}", string.Join(", ", appliedMigrations));
                    logger.LogInformation("All Migrations: {AllMigrations}", string.Join(", ", allMigrations));
                    logger.LogInformation("Pending Migrations: {PendingMigrations}", string.Join(", ", pendingMigrations));

                    if (pendingMigrations.Any())
                    {
                        db.Database.Migrate();
                        logger.LogInformation("Migrations applied successfully.");
                    }
                    else
                    {
                        logger.LogInformation("No pending migrations to apply.");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while migrating the database.");
                }
            }

            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var seeder = services.GetRequiredService<AppSeeder>();
                seeder.SeedData();
            }

            // Ensure ImageBlobName columns exist in database tables (avoid runtime errors when deploying new model)
            using (var scope = app.Services.CreateScope())
            {
                try
                {
                    var db = scope.ServiceProvider.GetRequiredService<ForestChurchesContext>();
                    var conn = db.Database.GetDbConnection();
                    await conn.OpenAsync();

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'AspNetUsers' AND COLUMN_NAME = 'ImageBlobName'";
                        var obj = await cmd.ExecuteScalarAsync();
                        var count = Convert.ToInt32(obj);
                        if (count == 0)
                        {
                            await db.Database.ExecuteSqlRawAsync("ALTER TABLE `AspNetUsers` ADD COLUMN `ImageBlobName` longtext NULL;");
                        }
                    }

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Events' AND COLUMN_NAME = 'ImageBlobName'";
                        var obj = await cmd.ExecuteScalarAsync();
                        var count = Convert.ToInt32(obj);
                        if (count == 0)
                        {
                            await db.Database.ExecuteSqlRawAsync("ALTER TABLE `Events` ADD COLUMN `ImageBlobName` longtext NULL;");
                        }
                    }

                    await conn.CloseAsync();
                }
                catch (Exception ex)
                {
                    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                    logger.LogWarning(ex, "Could not ensure ImageBlobName columns exist");
                }
            }

            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var loggerFactory = services.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger("Event Logger");

                try
                {
                    var iEvents = services.GetRequiredService<EventInterface>();
                    var recurringJobManager = services.GetRequiredService<IRecurringJobManager>();

                    recurringJobManager.AddOrUpdate("GetEvents", () => iEvents.GetEvents(), Cron.MinuteInterval(59));
                    recurringJobManager.AddOrUpdate("GetRepeatedEvents", () => iEvents.GetRepeatedEvents(), Cron.MinuteInterval(59));
                    recurringJobManager.AddOrUpdate("SortAndDeleteEvents", () => iEvents.SortAndDeleteEvents(), Cron.Hourly);
                    recurringJobManager.AddOrUpdate("SortAndDeleteRepeatedEvents", () => iEvents.CheckRepeatedEvents(), Cron.Hourly);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "An error occurred while setting up the event management background tasks.");
                }
            }

            app.Run();
        }
        catch (Exception ex)
        {
            SupportData.LogError(ex);
            SupportData.CreateSupportFile();
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}