using ForestChurches.Areas.Identity.Data;
using ForestChurches.Components.AutoEvents;
using ForestChurches.Components.Email;
using ForestChurches.Components.FileManager;
using ForestChurches.Components.Http;
using ForestChurches.Components.Http.Google;
using ForestChurches.Components.ImageHandler;
using ForestChurches.Components.Logging;
using ForestChurches.Components.LogReader;
using ForestChurches.Components.Performance;
using ForestChurches.Components.Permissions.Logic;
using ForestChurches.Components.Roles.logic;
using ForestChurches.Components.Security;
using ForestChurches.Components.Token;
using ForestChurches.Components.UserRegistration;
using ForestChurches.Components.Users;
using ForestChurches.Data;
using ForestChurches.Models;
using ForestChurches.Services;
using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using System.Text.Encodings.Web;

namespace ForestChurches.Components.Configuration
{
    public class ServiceRegistration
    {
        public static void RegisterServices(IServiceCollection Services)
        {
            // Registration of application...
            Services.AddHttpClient();
            Services.AddSingleton<ILogger, ClassLogger>();
            Services.AddScoped<LogRetriver>();
            Services.AddSingleton<ForestChurches.Components.Logging.SseManager>();
            Services.AddSignalR();
            Services.AddTransient<AppSeeder>();
            // Background service to watch DB for new logs and broadcast via SignalR
            Services.AddHostedService<ForestChurches.Components.Logging.LogWatcherService>();
            Services.AddSingleton<UniqueCode>();
            Services.AddSingleton<Encoder>(provider =>
            {
                var dataProtectionProvider = provider.GetRequiredService<IDataProtectionProvider>();
                var htmlEncoder = provider.GetRequiredService<HtmlEncoder>();
                var urlEncoder = provider.GetRequiredService<UrlEncoder>();
                return new Encoder(dataProtectionProvider, htmlEncoder, urlEncoder);
            });

            // Configuration service registration
            Services.AddScoped<Configuration>();
            Services.AddScoped<ContextConfiguration>();
            Services.AddScoped<PolicyConfiguration>();

            // Mail service registration
            Services.AddTransient<ISmtpClient, SmtpClient>();
            Services.AddTransient<iEmail, MailRepository>();
            Services.AddSingleton<EmailDeliveryTracker>();   // recent email hand-overs, shown in Admin → Email delivery
            Services.AddScoped<EmailServiceMonitor>();       // checks the email app's health and per-email status

            // User, Role & Permission service registration
            Services.AddTransient<UserInterface, UsersController>();
            Services.AddTransient<RolesInterface, RolesController>();
            Services.AddTransient<PermissionsInterface, PermissionController>();
            Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            Services.AddTransient<iMailSender, MailSender>();
            Services.AddTransient<CallbackToken>();

            Services.AddTransient<IDatabaseService, DatabaseService>();
            Services.AddScoped<JwtTokenGenerator>();

            // Authorization service registration
            Services.AddTransient<IAuthorizationHandler, PermissionAuthorizationHandler>();

            // service used for verifying postcode input
            Services.AddTransient<PostcodeValidatorInterface, PostcodeValidatorController>();

            // file manager (for creating downloadable files) service registration
            Services.AddTransient<FileInterface, FileController>();

            Services.AddScoped<ForestChurchesContext>();

            // Http service registration (for handling api requests)
            Services.AddTransient<IHttpWrapper, HttpWrapper>();
            Services.AddTransient<IHttpMethods, Http.HttpMethods>();
            Services.AddHttpClient<IChurchSuiteService, ChurchSuiteService>();
            Services.AddScoped<Pages.ChurchSuite.CallbackModel>();
            Services.AddHttpClient<IChurchSuiteEventSyncService, ChurchSuiteEventSyncService>();

            // Ensure blob storage service is available for event image uploads
            Services.AddSingleton<ForestChurches.Components.Storage.IBlobStorageService>(sp =>
            {
                var config = sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
                var conn = config["BlobStorage:ConnectionString"] ?? string.Empty;
                var container = config["BlobStorage:Container"] ?? "images";
                return new ForestChurches.Components.Storage.BlobStorageService(conn, container);
            });

            // ChurchSuite OAuth token and authentication services
            Services.AddHttpClient<IChurchSuiteTokenService, ChurchSuiteTokenService>();
            Services.AddScoped<IChurchSuiteAuthenticationService, ChurchSuiteAuthenticationService>();

            Services.AddSingleton<IProcessUsageService, ProcessUsageService>();

            // Google api service registration
            Services.AddScoped<GoogleInterface, GoogleController>();

            // Bulletin service
            Services.AddScoped<ForestChurches.Components.Bulletins.IBulletinService, ForestChurches.Components.Bulletins.BulletinService>();

            // Image controller, used for converting images to Binary64 & back
            Services.AddTransient<ImageInterface, ImageController>();

            // Event manager service (for handling existing events automatically during runtime)
            Services.AddTransient<EventInterface, EventContoller>();

            Services.AddTransient<iCryptoGraphic, Crypgraphic>();
            Services.AddTransient<iRegistrationGenerate, RegistrationGenerate>();
        }
    }
}