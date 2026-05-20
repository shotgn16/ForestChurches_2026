using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace ForestChurches.Components.ImageHandler
{
    public class ImageController : Controller, ImageInterface
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ImageController(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<byte[]> ConvertToByteArray(IFormFile file)
        {
            byte[] imageBytes;

            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);
                imageBytes = stream.ToArray();
            }

            return imageBytes;
        }

        private string[] GetAllowedImageHostsFromCSP()
        {
            var cspHeader = _httpContextAccessor.HttpContext.Response.Headers["Content-Security-Policy"].ToString();
            var imageSrcDirective = cspHeader.Split(';').FirstOrDefault(d => d.Trim().StartsWith("img-src", StringComparison.OrdinalIgnoreCase));

            if (imageSrcDirective != null)
            {
                var hosts = imageSrcDirective.Split(' ').Skip(1).ToArray(); // Skip "img-src"
                return hosts;
            }

            return Array.Empty<string>();
        }

        public async Task<byte[]> ConvertToByteArrayFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentNullException("URL cannot be null or empty.");
            }

            // 1. Validate URL format (basic check)
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                throw new ArgumentException("Invalid URL format.");
            }

            // 2. Whitelist allowed hosts/domains from CSP
            string[] allowedHosts = GetAllowedImageHostsFromCSP();
            if (!allowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException($"The host '{uri.Host}' is not allowed.");
            }

            // 3. Prevent access to localhost, loopback, and private IPs (more robust)
            if (System.Net.IPAddress.TryParse(uri.Host, out var ipAddress))
            {
                if (System.Net.IPAddress.IsLoopback(ipAddress) || ipAddress.Equals(System.Net.IPAddress.Any) || ipAddress.Equals(System.Net.IPAddress.IPv6Any) || IsPrivateIP(ipAddress))
                {
                    throw new UnauthorizedAccessException("Access to private/local addresses is not allowed.");
                }
            }

            // 4. Use HttpClient (more modern and configurable)
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; // Consider using newer TLS versions if possible.
                    byte[] imagebytes = await client.GetByteArrayAsync(url);
                    return imagebytes;
                }
                catch (HttpRequestException ex)
                {
                    // Log the exception for debugging, but don't expose details to the user.
                    Console.WriteLine($"Error downloading image: {ex.Message}");
                    throw new Exception("Error downloading image."); // Generic error message
                }
            }
        }

        private bool IsPrivateIP(System.Net.IPAddress ipAddress)
        {
            // Check for private IP ranges (adjust as needed)
            return (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && (
                ipAddress.GetAddressBytes()[0] == 10 ||
                (ipAddress.GetAddressBytes()[0] == 192 && ipAddress.GetAddressBytes()[1] == 168) ||
                (ipAddress.GetAddressBytes()[0] == 172 && ipAddress.GetAddressBytes()[1] >= 16 && ipAddress.GetAddressBytes()[1] <= 31)));
        }
    }
}
