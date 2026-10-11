using Microsoft.AspNetCore.Hosting; // Make sure you have this using statement
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages;
using Serilog;
using Serilog.Core;
using Serilog.Sinks.Grafana.Loki;
using Serilog.Sinks.MariaDB; // Add this for the MariaDB sink
using Serilog.Sinks.MariaDB.Extensions; // Add this for IWebHostEnvironment in .NET 6+
using System;
using System.IO;

namespace ForestChurches.Components.Logging
{
    public class LoggingConfiguration
    {
        public static void ConfigureLogging(IServiceCollection services, string connectionString, string seq_key, string seq_link, string loki_key)
        {
            var serviceProvider = services.BuildServiceProvider();

            var configuration = serviceProvider.GetService<IConfiguration>();
            if (configuration == null)
            {
                throw new InvalidOperationException("Configuration service is not available.");
            }

            // Use IWebHostEnvironment or IHostEnvironment depending on your .NET version
            IWebHostEnvironment environment = serviceProvider.GetService<IWebHostEnvironment>();
            if (environment == null)
            {
                // For .NET 6 and later:
                IHostEnvironment hostEnvironment = serviceProvider.GetService<IHostEnvironment>();
                if (hostEnvironment == null)
                {
                    throw new InvalidOperationException("IHostEnvironment service is not available.");
                }
                environment = new WebHostEnvironmentWrapper(hostEnvironment); // Wrap IHostEnvironment for compatibility
            }


            var levelSwitch = new LoggingLevelSwitch();
            configuration.GetSection("Serilog:MinimumLevel").Bind(levelSwitch);

            try
            {
                var contentRootPath = environment.ContentRootPath;

                // 1. Get the full, absolute path and handle potential exceptions.
                string fullContentRootPath;
                try
                {
                    fullContentRootPath = Path.GetFullPath(contentRootPath);
                }
                catch (Exception ex) // Catch potential exceptions
                {
                    Console.WriteLine($"Error getting full path: {ex.Message}");
                    return; // Or throw an exception if logging setup is critical.
                }

                // 2. Define the allowed base directory.  This is crucial!
                string allowedBaseDirectory = AppContext.BaseDirectory; // Or another appropriate, safe location.

                // 3. Check if the ContentRootPath is within the allowed directory.
                //if (!fullContentRootPath.StartsWith(allowedBaseDirectory, StringComparison.OrdinalIgnoreCase))
                //{
                //    Console.WriteLine($"Invalid ContentRootPath: {contentRootPath}. It must be within: {allowedBaseDirectory}");
                //    return; // Or throw an exception.
                //}

                var directory = Path.Combine(fullContentRootPath, "logs"); // Use the validated path.


                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var logFileName = $"ForestChurches-{DateTime.UtcNow:yyyyMMdd}.log";

                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.ControlledBy(levelSwitch)
                    .WriteTo.GrafanaLoki(
                        "https://logs-prod-035.grafana.net", 
                         labels: [new LokiLabel { Key = "ForestChurches", Value = "web_app" }],
                         propertiesAsLabels: ["RequestPath"],
                         credentials: new LokiCredentials { Login = "1773779", Password = loki_key },
                         traceIdMode: LokiFieldDestination.StructuredMetadata,
                         queueLimit: 100_000,
                         period: TimeSpan.FromSeconds(2))
                    .WriteTo.MariaDB(connectionString, tableName: "applicationlogs", autoCreateTable: true, useBulkInsert: false)
                    //.WriteTo.Console(Serilog.Events.LogEventLevel.Debug)
                    .CreateLogger();
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while configuring logging: " + ex.Message);
            }
        }
    }

    // Wrapper class for IHostEnvironment to work with the old interface.
    // This is only needed if you are using .NET 6 or later.
    public class WebHostEnvironmentWrapper : IWebHostEnvironment
    {
        private readonly IHostEnvironment _hostEnvironment;

        public WebHostEnvironmentWrapper(IHostEnvironment hostEnvironment)
        {
            _hostEnvironment = hostEnvironment;
        }

        public string ApplicationName { get => _hostEnvironment.ApplicationName; set => _hostEnvironment.ApplicationName = value; }
        public string ContentRootPath { get => _hostEnvironment.ContentRootPath; set => _hostEnvironment.ContentRootPath = value; }
        public string EnvironmentName { get => _hostEnvironment.EnvironmentName; set => _hostEnvironment.EnvironmentName = value; }
        public string WebRootPath { get => null; set { } } // Not used in this context.

        public IFileProvider WebRootFileProvider { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public IFileProvider ContentRootFileProvider { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    }
}