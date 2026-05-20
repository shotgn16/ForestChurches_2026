using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ForestChurches.Components.Security.Token
{
    public class CSPNonceMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ContentSecurityPolicyOptions _options;
        private readonly Microsoft.Extensions.Logging.ILogger<CSPNonceMiddleware> _logger;
        private const int NonceLength = 16;

        public CSPNonceMiddleware(RequestDelegate next, ContentSecurityPolicyOptions options, Microsoft.Extensions.Logging.ILogger<CSPNonceMiddleware> logger)
        {
            _next = next;
            _options = options ?? new ContentSecurityPolicyOptions();
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Generate nonces for this request
            var scriptNonce = GenerateNonce();
            var styleNonce = GenerateNonce();

            // Store nonces in HttpContext.Items for use in views
            context.Items["CSP-Script-Nonce"] = scriptNonce;
            context.Items["CSP-Style-Nonce"] = styleNonce;

            // Build the CSP
            var csp = BuildCSP(scriptNonce, styleNonce); 

            // Ensure CSP header is set just before the response is sent. Use OnStarting so we override any earlier
            // partial CSP headers and guarantee the final header includes our directives.
            context.Response.OnStarting(state =>   
            {
                var httpContext = (HttpContext)state!;
                // Overwrite any existing CSP header so the full policy is present
                httpContext.Response.Headers["Content-Security-Policy"] = csp;
                // Do not add a report-only header to avoid duplicate/multi policies confusing the browser
                // Add a debug header so the final CSP can be inspected easily in browser devtools
                httpContext.Response.Headers["X-CSP-Debug"] = csp;
                try
                {
                    _logger?.LogInformation("Final CSP header set: {CSP}", csp);
                }
                catch { }
                // Remove any duplicate CSP headers if present
                if (httpContext.Response.Headers.TryGetValue("Content-Security-Policy", out var _))
                {
                    // already set above
                }

                return Task.CompletedTask;
            }, context);

            await _next(context);
        }

        private string BuildCSP(string scriptNonce, string styleNonce)
        {
            // 1. Script Sources
            var scriptSrc = new List<string> { "'self'", "https://unpkg.com", "https://ka-f.fontawesome.com" };
            if (_options.AdditionalScriptSources?.Count > 0)
                scriptSrc.AddRange(_options.AdditionalScriptSources);

            scriptSrc.Add($"'nonce-{scriptNonce}'");
            // MiniProfiler often requires unsafe-eval for its client-side templates
            scriptSrc.Add("'unsafe-eval'");

            // 2. Style Sources
            var styleSrc = new List<string> { "'self'", "https://unpkg.com" };
            if (_options.AdditionalStyleSources?.Count > 0)
                styleSrc.AddRange(_options.AdditionalStyleSources);

            styleSrc.Add($"'nonce-{styleNonce}'");
            // MiniProfiler uses inline styles for the popup UI
            styleSrc.Add("'unsafe-inline'");

            // 3. Image Sources
            var imgSrc = new List<string> { "'self'", "data:", "https://*.blob.core.windows.net", "https://greenblockstorageaccount.blob.core.windows.net", "https://i.imgur.com", "https://imgur.com", "https://*.tile.openstreetmap.org" };
            if (_options.AdditionalImageSources?.Count > 0)
                imgSrc.AddRange(_options.AdditionalImageSources);

            // 4. Connect Sources (Crucial for MiniProfiler AJAX calls)
            var connectSrc = new List<string> {
                "'self'",
                "ws:",
                "wss:",
                "http://localhost:52559",
                "http://127.0.0.1:52559",
                "http://localhost:52671",
                "https://localhost:7196"
            };
            
            if (_options.AllowedScriptUrls?.Count > 0)
                connectSrc.AddRange(_options.AllowedScriptUrls);

            // Build the string
            var sb = new StringBuilder();
            sb.Append("default-src 'self'; ");
            sb.Append("script-src ").Append(string.Join(' ', scriptSrc)).Append("; ");
            sb.Append("style-src ").Append(string.Join(' ', styleSrc)).Append("; ");
            sb.Append("img-src ").Append(string.Join(' ', imgSrc)).Append("; ");
            sb.Append("connect-src ").Append(string.Join(' ', connectSrc)).Append("; ");
            sb.Append("object-src 'none'; base-uri 'self'; form-action 'self'; ");
            sb.Append("font-src 'self' https://ka-f.fontawesome.com; ");

            return sb.ToString();
        }

        private static string GenerateNonce()
        {
            var nonceBytes = new byte[NonceLength];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(nonceBytes);
            }
            return Convert.ToBase64String(nonceBytes);
        }
    }

    // Extension method for easy middleware registration
    public static class ContentSecurityPolicyMiddlewareExtensions
    {
        public static IApplicationBuilder UseContentSecurityPolicy(
            this IApplicationBuilder builder,
            Action<ContentSecurityPolicyOptions> configureOptions)
        {
            var options = new ContentSecurityPolicyOptions();
            // Allow specific storage account and any blob.core.windows.net host by default
            options.AdditionalImageSources.Add("https://greenblockstorageaccount.blob.core.windows.net");
            options.AdditionalImageSources.Add("https://*.blob.core.windows.net");

            configureOptions(options);
            return builder.UseMiddleware<CSPNonceMiddleware>(options);
        }

        public static IApplicationBuilder UseContentSecurityPolicy(
            this IApplicationBuilder builder,
            string scriptHashes, List<string> allowedScriptUrls)
        {
            var options = new ContentSecurityPolicyOptions
            {
                ScriptHashes = scriptHashes ?? string.Empty,
                AllowedScriptUrls = allowedScriptUrls ?? new List<string>()
            };
            return builder.UseMiddleware<CSPNonceMiddleware>(options);
        }
    }

    public class ContentSecurityPolicyOptions
    {
        public string ScriptHashes { get; set; } = string.Empty;
        public bool EnabledNonces { get; set; } = true;
        public List<string> AdditionalImageSources { get; set; } = new();
        public List<string> AdditionalScriptSources { get; set; } = new();
        public List<string> AdditionalStyleSources { get; set; } = new();
        public List<string> AllowedScriptUrls { get; set; } = new() { "https://maps.googleapis.com" };
    }
}