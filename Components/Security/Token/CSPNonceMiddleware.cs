using System.Security.Cryptography;
using System.Text;

namespace ForestChurches.Components.Security.Token
{
    /// <summary>
    /// Sends a Content Security Policy (and a few related headers) on every response.
    ///
    /// Scripts must come from this site, the two CDNs the pages use (unpkg for Leaflet, cdnjs for the
    /// QR-code library), or be inline scripts that carry this request's nonce:
    ///     &lt;script nonce="@Context.Items["CSP-Script-Nonce"]"&gt;…&lt;/script&gt;
    /// Inline event handlers (onclick="…") and eval are blocked.
    /// Inline styles are still allowed because the views use style="…" attributes.
    /// </summary>
    public class CSPNonceMiddleware
    {
        public const string ScriptNonceKey = "CSP-Script-Nonce";
        public const string StyleNonceKey = "CSP-Style-Nonce";

        private readonly RequestDelegate _next;
        private readonly ContentSecurityPolicyOptions _options;
        private const int NonceLength = 16;

        public CSPNonceMiddleware(RequestDelegate next, ContentSecurityPolicyOptions options)
        {
            _next = next;
            _options = options ?? new ContentSecurityPolicyOptions();
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Third-party admin dashboards (Hangfire, Elmah) ship their own inline scripts that can't carry
            // our nonce. They're already behind admin authentication, so they get the other headers only.
            foreach (var excluded in _options.ExcludedPaths)
            {
                if (context.Request.Path.StartsWithSegments(excluded, StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.OnStarting(() =>
                    {
                        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                        context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
                        return Task.CompletedTask;
                    });
                    await _next(context);
                    return;
                }
            }

            var scriptNonce = GenerateNonce();
            context.Items[ScriptNonceKey] = scriptNonce;
            context.Items[StyleNonceKey] = GenerateNonce(); // kept for views that already read it

            var csp = BuildCsp(scriptNonce);
            var headerName = _options.ReportOnly ? "Content-Security-Policy-Report-Only" : "Content-Security-Policy";

            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers[headerName] = csp;
                headers["X-Content-Type-Options"] = "nosniff";
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                headers["X-Frame-Options"] = "SAMEORIGIN";
                return Task.CompletedTask;
            });

            await _next(context);
        }

        private string BuildCsp(string scriptNonce)
        {
            var scriptSrc = new List<string> { "'self'", $"'nonce-{scriptNonce}'", "https://unpkg.com", "https://cdnjs.cloudflare.com" };
            scriptSrc.AddRange(_options.AdditionalScriptSources);

            var styleSrc = new List<string> { "'self'", "'unsafe-inline'", "https://unpkg.com", "https://cdnjs.cloudflare.com" };
            styleSrc.AddRange(_options.AdditionalStyleSources);

            var imgSrc = new List<string> { "'self'", "data:", "blob:", "https://*.blob.core.windows.net", "https://*.tile.openstreetmap.org" };
            imgSrc.AddRange(_options.AdditionalImageSources);

            // 'self' covers same-origin fetch and the SignalR WebSocket (wss://) to this site.
            var connectSrc = new List<string> { "'self'" };
            connectSrc.AddRange(_options.AdditionalConnectSources);

            // Sign-in forms redirect to ChurchSuite for OAuth.
            var formAction = new List<string> { "'self'", "https://login.churchsuite.com", "https://oauth.churchsuite.com" };

            var sb = new StringBuilder();
            sb.Append("default-src 'self'; ");
            sb.Append("script-src ").Append(string.Join(' ', scriptSrc.Distinct())).Append("; ");
            sb.Append("style-src ").Append(string.Join(' ', styleSrc.Distinct())).Append("; ");
            sb.Append("img-src ").Append(string.Join(' ', imgSrc.Distinct())).Append("; ");
            sb.Append("font-src 'self' data:; ");
            sb.Append("connect-src ").Append(string.Join(' ', connectSrc.Distinct())).Append("; ");
            sb.Append("form-action ").Append(string.Join(' ', formAction)).Append("; ");
            sb.Append("object-src 'none'; base-uri 'self'; frame-ancestors 'self'");
            return sb.ToString();
        }

        private static string GenerateNonce()
        {
            var nonceBytes = RandomNumberGenerator.GetBytes(NonceLength);
            return Convert.ToBase64String(nonceBytes);
        }
    }

    public class ContentSecurityPolicyOptions
    {
        /// <summary>Send the policy as Report-Only (logs violations in the browser without blocking).</summary>
        public bool ReportOnly { get; set; }
        public List<string> AdditionalImageSources { get; set; } = new();
        public List<string> AdditionalScriptSources { get; set; } = new();
        public List<string> AdditionalStyleSources { get; set; } = new();
        public List<string> AdditionalConnectSources { get; set; } = new();
        /// <summary>Path prefixes that don't get the policy (third-party dashboards with inline scripts).</summary>
        public List<string> ExcludedPaths { get; set; } = new() { "/hangfire", "/elmah" };
    }

    public static class ContentSecurityPolicyMiddlewareExtensions
    {
        public static IApplicationBuilder UseContentSecurityPolicy(
            this IApplicationBuilder builder,
            Action<ContentSecurityPolicyOptions>? configureOptions = null)
        {
            var options = new ContentSecurityPolicyOptions();
            configureOptions?.Invoke(options);
            return builder.UseMiddleware<CSPNonceMiddleware>(options);
        }
    }
}
