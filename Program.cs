using Azure.Identity;
using Azure.Storage.Blobs;
using ElmahCore;
using ElmahCore.Mvc;
using ForestChurches;
using ForestChurches.Components.Configuration;
using ForestChurches.Components.Logging;
using ForestChurches.Components.Roles;
using ForestChurches.Components.Support;
using ForestChurches.Data;
using ForestChurches.Localization.Json;
using ForestChurches.Models;
using Hangfire;
using Hangfire.MySql;
using HangfireBasicAuthenticationFilter;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Globalization;
using System.Security.Claims;
using System.Text;

internal partial class Program
{
    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        string? connectionString = string.Empty;
        string? environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        if (!string.IsNullOrEmpty(environment) && environment != "Production")
        {
            configBuilder.AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true);
        }

        configBuilder.AddEnvironmentVariables();
        var config = configBuilder.Build();

        try
        {
            builder.Services.AddDistributedMemoryCache();

            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.IsEssential = true;
            });

            // Leave ResourcesPath empty when .resx files are in the project root
            builder.Services.AddLocalization(options => options.ResourcesPath = "");

            builder.Services.AddRazorPages()
                .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
                .AddDataAnnotationsLocalization(options =>
                {
                    options.DataAnnotationLocalizerProvider = (type, factory) =>
                        factory.Create(typeof(ForestChurches.SharedResources));
                });

            builder.Services.Configure<RequestLocalizationOptions>(options =>
            {
                // UI text comes from the neutral "en" resources, but dates and numbers are formatted the
                // British way (en-GB: 18/06/2026, 24-hour times) rather than the US default for "en".
                var supportedUiCultures = new[] { "en", "fa", "th", "uk", "fr", "de", "cs" };
                var supportedCultures = new[] { "en-GB", "fa", "th", "uk", "fr", "de", "cs" };
                options.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(culture: "en-GB", uiCulture: "en");
                options.AddSupportedCultures(supportedCultures)
                    .AddSupportedUICultures(supportedUiCultures);

                // Prioritize QueryString -> Cookie -> AcceptLanguageHeader
                options.RequestCultureProviders = new IRequestCultureProvider[]
                {
        new QueryStringRequestCultureProvider(),
        new CookieRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
                };
            });

            builder.Services
                .AddControllersWithViews(options =>
                {
                    options.Filters.Add(typeof(ErrorController));
                })
                .AddSessionStateTempDataProvider()
                .AddViewLocalization()
                .AddDataAnnotationsLocalization();

            builder.Services.AddRazorPages().AddViewLocalization().AddDataAnnotationsLocalization();

            // Non-generic IStringLocalizer used in _ViewImports.cshtml; register to use SharedResources marker to avoid
            // forcing Program as the resource base name which caused duplicate AppText keys.
            builder.Services.AddScoped(typeof(IStringLocalizer), provider =>
            {
                var factory = provider.GetRequiredService<IStringLocalizerFactory>();
                return factory.Create(typeof(ForestChurches.SharedResources));
            });

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

            LoggingConfiguration.ConfigureLogging(builder.Services, connectionString, secretClient.GetSecret("seq-key").Value.Value, secretClient.GetSecret("seq-link").Value.Value, secretClient.GetSecret("lokikey").Value.Value);
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

            try
            {
                var blobConn = secretClient.GetSecret("blob-connection-string").Value.Value;
                var blobContainer = secretClient.GetSecret("blob-container-name").Value.Value;
                var blobFile = secretClient.GetSecret("blob-file-name").Value.Value;

                var keyIdentifier = secretClient.GetSecret("data-key").Value?.Value;
                var keyVaultCredential = new EnvironmentCredential();

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

                    if (!string.IsNullOrWhiteSpace(keyIdentifier))
                    {
                        try
                        {
                            dp.ProtectKeysWithAzureKeyVault(new Uri(keyIdentifier), keyVaultCredential);
                            Log.Information("DataProtection: Configured Azure Key Vault protector.");
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "DataProtection: Failed to configure Key Vault protector. Continuing with Blob-only key persistence.");
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
                options.Cookie.SameSite = SameSiteMode.None;
            });

            builder.Services.ConfigureExternalCookie(options =>
            {
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.None;
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

            builder.Services.AddHsts(options =>
            {
                options.MaxAge = TimeSpan.FromDays(365);
                options.IncludeSubDomains = true;
                options.Preload = true;
            });

            if (!Directory.Exists(Environment.CurrentDirectory + "logs"))
            {
                Directory.CreateDirectory(Environment.CurrentDirectory + "logs");
            }

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
                            TransactionTimeout = TimeSpan.FromMinutes(5),
                            TablesPrefix = "Hangfire",
                        })
                    )
                );

            builder.Services.AddHangfireServer(options => options.WorkerCount = Math.Min(Environment.ProcessorCount * 5, 20));
            builder.Services.AddLogging();
            ServiceRegistration.RegisterServices(builder.Services);

            builder.Services.AddSignalR();
            builder.Services.AddHostedService<ForestChurches.Tools.ImageMigrationHostedService>();

            BlobServiceClient blobServiceClient = new BlobServiceClient(
                secretClient.GetSecret("blob-connection-string").Value.Value);

            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(
                secretClient.GetSecret("blob-container-name").Value.Value);

            await containerClient.CreateIfNotExistsAsync();

            var storageConnectionString = secretClient.GetSecret("blob-connection-string").Value.Value;
            var storageAccountName = secretClient.GetSecret("storage-account-name").Value.Value;
            var storageAccountKey = secretClient.GetSecret("storage-account-key").Value.Value;
            var imagesContainerName = secretClient.GetSecret("storage-container-name").Value.Value;

            builder.Services.AddSingleton<ForestChurches.Components.Storage.IBlobUrlService>(sp =>
            {
                return new ForestChurches.Components.Storage.BlobUrlService(storageConnectionString, storageAccountName, storageAccountKey, imagesContainerName);
            });

            builder.Services.AddSingleton<ForestChurches.Components.Storage.IBlobStorageService>(sp =>
            {
                return new ForestChurches.Components.Storage.BlobStorageService(storageConnectionString, imagesContainerName);
            });

            var jwtSecret = secretClient.GetSecret("jwt-token-securityKey").Value.Value;
            var jwtIssuer = secretClient.GetSecret("jwt-token-issuer").Value.Value;
            var jwtAudience = secretClient.GetSecret("jwt-token-audience").Value.Value;

            ForestChurches.Components.Configuration.ChurchSuiteOAuthSettings csSettings = null;
            try
            {
                csSettings = ForestChurches.Components.Configuration.ChurchSuiteOAuthConfiguration.GetOAuthSettings(builder.Configuration);
                if (csSettings != null)
                {
                    Log.Information("ChurchSuite OAuth configuration loaded. CallbackUrl: {CallbackUrl}", csSettings.CallbackUrl);
                }
                else
                {
                    Log.Information("ChurchSuite OAuth configuration not provided. ChurchSuite integration will be disabled.");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load ChurchSuite OAuth settings. ChurchSuite integration will be disabled.");
                csSettings = null;
            }

            var authBuilder = builder.Services.AddAuthentication();

            if (!string.IsNullOrWhiteSpace(csSettings?.ClientId))
            {
                authBuilder.AddOAuth("ChurchSuite", options =>
                {
                    options.ClientId = csSettings.ClientId;
                    if (!string.IsNullOrWhiteSpace(csSettings.ClientSecret))
                    {
                        options.ClientSecret = csSettings.ClientSecret;
                    }

                    options.CallbackPath = ForestChurches.Components.Configuration.ChurchSuiteOAuthConfiguration.GetCallbackPath(csSettings.CallbackUrl);

                    options.AuthorizationEndpoint = csSettings.AuthorizationEndpoint;
                    options.TokenEndpoint = csSettings.TokenEndpoint;
                    options.UserInformationEndpoint = csSettings.UserInformationEndpoint;

                    options.Scope.Clear();
                    options.Scope.Add("full_access");
                    options.Scope.Add("offline_access");

                    options.SaveTokens = true;

                    try
                    {
                        var usePkceProp = options.GetType().GetProperty("UsePkce");
                        if (usePkceProp != null && usePkceProp.CanWrite)
                        {
                            usePkceProp.SetValue(options, true);
                            Log.Information("Enabled PKCE for ChurchSuite OAuth via reflection.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to enable PKCE via reflection.");
                    }

                    options.Events = new Microsoft.AspNetCore.Authentication.OAuth.OAuthEvents
                    {
                        OnCreatingTicket = async context =>
                        {
                            Log.Information("ChurchSuite OAuth ticket created for user {NameIdentifier}",
                                context.Principal?.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier) ?? "unknown");
                        },
                        OnRemoteFailure = context =>
                        {
                            Log.Error("ChurchSuite OAuth remote failure: {Failure} | Exception: {Exception}",
                                context.Failure?.Message, context.Failure?.GetBaseException()?.ToString());

                            context.HandleResponse();
                            context.Response.Redirect("/Identity/Account/Login?error=ChurchSuite+OAuth+failed");
                            return Task.CompletedTask;
                        },
                        OnAccessDenied = context =>
                        {
                            Log.Warning("ChurchSuite OAuth access denied by user");
                            context.HandleResponse();
                            context.Response.Redirect("/Identity/Account/Login?error=Access+denied");
                            return Task.CompletedTask;
                        },
                    };

                    options.Events.OnTicketReceived = async context =>
                    {
                        Log.Information("[OnTicketReceived] ChurchSuite OAuth callback invoked");
                        try
                        {
                            var principal = context.Principal;
                            var incomingRedirect = context.Properties?.RedirectUri ?? "/";

                            string accessToken = null;
                            string refreshToken = null;
                            string expiresAtString = null;

                            if (context.Properties?.Items != null)
                            {
                                context.Properties.Items.TryGetValue(".Token.access_token", out accessToken);
                                context.Properties.Items.TryGetValue(".Token.refresh_token", out refreshToken);
                                context.Properties.Items.TryGetValue(".Token.expires_at", out expiresAtString);
                            }

                            var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ForestChurches.Components.Users.ChurchAccount>>();
                            var dbContext = context.HttpContext.RequestServices.GetRequiredService<ForestChurchesContext>();
                            var signInManager = context.HttpContext.RequestServices.GetRequiredService<SignInManager<ForestChurches.Components.Users.ChurchAccount>>();

                            string userIdFromChallenge = null;
                            string redirectUriFromChallenge = null;
                            if (context.Properties?.Items != null)
                            {
                                context.Properties.Items.TryGetValue("user_id", out userIdFromChallenge);
                                context.Properties.Items.TryGetValue("redirect_uri", out redirectUriFromChallenge);
                            }

                            var authenticatedUser = await userManager.GetUserAsync(context.HttpContext.User);
                            if (authenticatedUser == null && !string.IsNullOrEmpty(userIdFromChallenge))
                            {
                                authenticatedUser = await userManager.FindByIdAsync(userIdFromChallenge);
                            }

                            if (authenticatedUser != null)
                            {
                                try
                                {
                                    var subdomain = "unknown";
                                    if (DateTime.TryParse(expiresAtString, out var expiresAt))
                                    {
                                        expiresAt = DateTime.SpecifyKind(expiresAt, DateTimeKind.Utc);
                                    }
                                    else
                                    {
                                        expiresAt = DateTime.UtcNow.AddHours(1);
                                    }

                                    var credential = await dbContext.ChurchSuiteCredentials
                                        .FirstOrDefaultAsync(c => c.UserId == authenticatedUser.Id);

                                    if (credential != null)
                                    {
                                        credential.AccessToken = accessToken;
                                        credential.RefreshToken = refreshToken;
                                        credential.ExpiresAt = expiresAt;
                                        credential.UpdatedAt = DateTime.UtcNow;
                                        credential.LastRefreshedAt = DateTime.UtcNow;
                                        credential.IsActive = true;
                                        dbContext.ChurchSuiteCredentials.Update(credential);
                                    }
                                    else
                                    {
                                        var newCredential = new ChurchSuiteCredential
                                        {
                                            UserId = authenticatedUser.Id,
                                            Subdomain = subdomain,
                                            AccessToken = accessToken,
                                            RefreshToken = refreshToken,
                                            ExpiresAt = expiresAt,
                                            CreatedAt = DateTime.UtcNow,
                                            UpdatedAt = DateTime.UtcNow,
                                            LastRefreshedAt = DateTime.UtcNow,
                                            IsActive = true
                                        };
                                        await dbContext.ChurchSuiteCredentials.AddAsync(newCredential);
                                    }

                                    await dbContext.SaveChangesAsync();
                                }
                                catch (Exception ex)
                                {
                                    Log.Error(ex, "[OnTicketReceived] Error saving credentials for user {UserId}", authenticatedUser.Id);
                                }

                                await signInManager.SignInAsync(authenticatedUser, isPersistent: true);
                                context.HandleResponse();
                                context.Response.Redirect(incomingRedirect);
                                return;
                            }

                            var providerKey = principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                                              ?? principal?.FindFirst("sub")?.Value
                                              ?? principal?.FindFirst("id")?.Value;

                            var existingUser = (string.IsNullOrWhiteSpace(providerKey))
                                ? null
                                : await userManager.FindByLoginAsync("ChurchSuite", providerKey).ConfigureAwait(false);

                            if (existingUser != null)
                            {
                                return;
                            }

                            var token = Guid.NewGuid().ToString("N");
                            await context.HttpContext.Session.LoadAsync();

                            var externalLoginDto = new
                            {
                                Provider = "ChurchSuite",
                                ProviderKey = providerKey,
                                DisplayName = principal?.FindFirst(System.Security.Claims.ClaimTypes.GivenName)?.Value
                                              ?? principal?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                                              ?? "Unknown",
                                AccessToken = accessToken,
                                RefreshToken = refreshToken,
                                Claims = principal?.Claims.Select(c => new { c.Type, c.Value }).ToList()
                            };

                            var sessionKey = $"ExternalLoginTemp:{token}";
                            var jsonData = System.Text.Json.JsonSerializer.Serialize(externalLoginDto);

                            try
                            {
                                context.HttpContext.Session.SetString(sessionKey, jsonData);
                                await context.HttpContext.Session.CommitAsync();
                            }
                            catch (Exception ex)
                            {
                                Log.Warning(ex, "[OnTicketReceived] Failed to store session for new user registration");
                            }

                            var callbackUrl = $"/Identity/Account/ExternalLogin?handler=Callback&token={token}&returnUrl={Uri.EscapeDataString(incomingRedirect)}";
                            context.Response.Redirect(callbackUrl);
                            context.HandleResponse();
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "[OnTicketReceived] Unexpected error in ChurchSuite OAuth callback");
                        }
                    };
                });
            }

            authBuilder.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
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
            });

            var app = builder.Build();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.Use(async (context, next) =>
            {
                context.Response.Headers.Remove("Server");
                context.Response.Headers.Remove("X-Powered-By");
                context.Response.Headers.Add("Strict-Transport-Security", "max-age=31536000; includeSubDomains; preload");
                await next();
            });

            app.UseHttpsRedirection();
            app.UseSerilogRequestLogging();

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

            app.UseStaticFiles();
            app.UseRouting();

            // --- CULTURE MIDDLEWARE (allow culture via query parameter) ---
            app.Use(async (context, next) =>
            {
                if (context.Request.Query.TryGetValue("culture", out var culture))
                {
                    var cookieOptions = new Microsoft.AspNetCore.Http.CookieOptions
                    {
                        Expires = DateTimeOffset.UtcNow.AddMonths(1),
                        IsEssential = true,
                        HttpOnly = false,
                        SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax
                    };
                    context.Response.Cookies.Append(
                        CookieRequestCultureProvider.DefaultCookieName,
                        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                        cookieOptions
                    );
                }
                await next(context);
            });

            app.UseRequestLocalization();
            app.UseSession();

            app.UseAuthentication();
            app.UseAuthorization();
            app.UseElmah();

            // --- BYPASS FILE CHECKER FOR DIST ASSETS ---
             app.Use(async (context, next) =>
            {
                var path = context.Request.Path.Value ?? string.Empty;

                if (path.StartsWith("/dist", StringComparison.OrdinalIgnoreCase))
                {
                    await next();
                    return;
                }

                var blockedExtensions = new[] { ".json", ".config", ".env", ".ini", ".exe", ".pfx", ".cshtml", ".cshtml.cs", ".cs", ".dll", ".user" };
                var allowedExtensions = new[] { ".css", ".js", ".png", ".jpg", ".jpeg", ".gif", ".svg", ".woff", ".woff2", ".ttf", ".eot" };

                if (context.Request.Path.HasValue && context.Request.Path.Value.Contains("."))
                {
                    var extension = System.IO.Path.GetExtension(context.Request.Path.Value);
                    var filePath = context.Request.Path.Value;

                    if (filePath.Contains("support/SupportData-") && extension == ".zip")
                    {
                        await next();
                        return;
                    }

                    if (blockedExtensions.Any(ext => ext.Equals(extension, StringComparison.OrdinalIgnoreCase)) ||
                        !allowedExtensions.Contains(extension))
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsync("Access to file downloads is forbidden.");
                        return;
                    }
                }

                await next();
            });

            // --- CUSTOM ANTIFORGERY MIDDLEWARE (ENFORCES CSRF EXCEPT FOR EXCLUDED PATHS) ---
            app.Use(next => context =>
            {
                var path = context.Request.Path;
                var isIdentityEndpoint = path.StartsWithSegments("/Identity/Account/Login", StringComparison.OrdinalIgnoreCase)
                                         || path.StartsWithSegments("/Identity/Account/Register", StringComparison.OrdinalIgnoreCase)
                                         || path.StartsWithSegments("/Identity/Account/Logout", StringComparison.OrdinalIgnoreCase)
                                         || path.StartsWithSegments("/Identity/Account/ExternalLogin", StringComparison.OrdinalIgnoreCase);

                var isExcludedEndpoint = path.StartsWithSegments("/dist", StringComparison.OrdinalIgnoreCase) ||
                        path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);

                if ((string.Equals(context.Request.Method, "POST", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(context.Request.Method, "PUT", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(context.Request.Method, "DELETE", StringComparison.OrdinalIgnoreCase))
                     && !isIdentityEndpoint && !isExcludedEndpoint)
                {
                    var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
                    antiforgery.ValidateRequestAsync(context).Wait();
                }

                return next(context);
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
                endpoints.MapHub<ForestChurches.Hubs.LogsHub>("/logsHub");
            });

            using (var scope = app.Services.CreateScope())
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                try
                {
                    var db = scope.ServiceProvider.GetRequiredService<ForestChurchesContext>();
                    var pendingMigrations = db.Database.GetPendingMigrations();
                    if (pendingMigrations.Any())
                    {
                        db.Database.Migrate();
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