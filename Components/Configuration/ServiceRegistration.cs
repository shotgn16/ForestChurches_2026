using ForestChurches.Components.AutoEvents;
using ForestChurches.Components.Email;
using ForestChurches.Components.FileManager;
using ForestChurches.Components.Http.Google;
using ForestChurches.Components.Http;
using ForestChurches.Components.ImageHandler;
using ForestChurches.Components.LogReader;
using ForestChurches.Components.Permissions.Logic;
using ForestChurches.Components.Roles.logic;
using ForestChurches.Components.UserRegistration;
using ForestChurches.Components.Users;
using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Authorization;
using ForestChurches.Components.Logging;
using ForestChurches.Components.Token;
using ForestChurches.Areas.Identity.Data;
using ForestChurches.Components.Performance;
using ForestChurches.Services;
using ForestChurches.Data;
using ForestChurches.Models;
using System.Text.Encodings.Web;
using ForestChurches.Components.Security;
using Microsoft.AspNetCore.DataProtection;
using ForestChurches.Components.Security.Token;

namespace ForestChurches.Components.Configuration
{
    public class ServiceRegistration
    {
        public static void RegisterServices(IServiceCollection Services)
        {
            // Registeration of application...
            Services.AddHttpClient();
            Services.AddSingleton<ILogger, ClassLogger>();
            Services.AddScoped<LogRetriver>();
            Services.AddTransient<AppSeeder>();
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

            // User, Role & Permission service registration
            Services.AddTransient<UserInterface, UsersController>();
            Services.AddTransient<RolesInterface, RolesController>();
            Services.AddTransient<PermissionsInterface, PermissionController>();
            Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            Services.AddTransient<iMailSender, MailSender>();
            Services.AddTransient<CallbackToken>();

            Services.AddTransient<IDatabaseService, DatabaseService>();
            Services.AddScoped<JwtTokenGenerator>();

            // Authoization service registration
            Services.AddTransient<IAuthorizationHandler, PermissionAuthorizationHandler>();

            // service used for verifying postcode input
            Services.AddTransient<PostcodeValidatorInterface, PostcodeValidatorController>();

            // file manager (for creating downloadable files) service registration
            Services.AddTransient<FileInterface, FileController>();

            Services.AddScoped<ForestChurchesContext>();

            // Http service registration (for handling api requests)
            Services.AddTransient<IHttpWrapper, HttpWrapper>();
            Services.AddTransient<IHttpMethods, Http.HttpMethods>();

            Services.AddSingleton<IProcessUsageService, ProcessUsageService>();

            // Google api service registration
            Services.AddScoped<GoogleInterface, GoogleController>();

            // Image controller, used for converting images to Binary64 & back
            Services.AddTransient<ImageInterface, ImageController>();

            // Event manager service (for handling existing events automatically during runtime)
            Services.AddTransient<EventInterface, EventContoller>();

            Services.AddTransient<iCryptoGraphic, Crypgraphic>();
            Services.AddTransient<iRegistrationGenerate, RegistrationGenerate>();
        }
    }
}
