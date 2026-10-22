using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ForestChurches.Data;
using ForestChurches.Components.Users;
using Microsoft.AspNetCore.Identity;
using ForestChurches.Models;
using Microsoft.EntityFrameworkCore;

namespace ForestChurches.Pages.ChurchSuite
{
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class AuthorizeModel : PageModel
    {
        private readonly UserManager<ChurchAccount> _userManager;
        private readonly ForestChurchesContext _context;

        public AuthorizeModel(UserManager<ChurchAccount> userManager, ForestChurchesContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public IActionResult OnGet(string user_id, string redirect_uri)
        {
            // Security: always act for the signed-in user. A user_id in the query string is
            // accepted only for backwards compatibility and must match them.
            var signedInUserId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(signedInUserId))
            {
                return Challenge();
            }
            if (!string.IsNullOrWhiteSpace(user_id) && !string.Equals(user_id, signedInUserId, StringComparison.Ordinal))
            {
                return Forbid();
            }
            user_id = signedInUserId;
            if (!string.IsNullOrWhiteSpace(redirect_uri) && !Url.IsLocalUrl(redirect_uri))
            {
                redirect_uri = null;
            }

            // Lookup user and ensure client id exists
            var user = _userManager.FindByIdAsync(user_id).GetAwaiter().GetResult();
            if (user == null)
            {
                return NotFound("User not found");
            }

            if (string.IsNullOrWhiteSpace(user.ClientId))
            {
                TempData["ChurchSuiteAuthError"] = "No ChurchSuite Client ID configured. Go to Manage -> External Logins to add one.";
                // Redirect back to provided redirect_uri or to Manage page
                var fallback = string.IsNullOrWhiteSpace(redirect_uri) ? "/Identity/Account/Manage/ExternalLogins" : redirect_uri;
                return Redirect(fallback);
            }

            // Generate PKCE code_verifier and code_challenge (S256)
            var codeVerifier = GenerateCodeVerifier();
            var codeChallenge = ComputeCodeChallenge(codeVerifier);

            // Persist code_verifier on a ChurchSuiteCredential row for this user. Create one if missing.
            var credential = _context.ChurchSuiteCredentials.FirstOrDefault(c => c.UserId == user.Id);
            if (credential == null)
            {
                credential = new ChurchSuiteCredential
                {
                    UserId = user.Id,
                    Subdomain = "unknown",
                    AccessToken = string.Empty,
                    RefreshToken = null,
                    ClientId = user.ClientId,
                    TempCodeVerifier = codeVerifier,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = false
                };
                _context.ChurchSuiteCredentials.Add(credential);
            }
            else
            {
                credential.TempCodeVerifier = codeVerifier;
                credential.ClientId = user.ClientId ?? credential.ClientId;
                credential.UpdatedAt = DateTime.UtcNow;
                _context.ChurchSuiteCredentials.Update(credential);
            }

            _context.SaveChanges();

            // Build authorize URL for ChurchSuite
            var authorizeEndpoint = "https://login.churchsuite.com/oauth2/authorize";
            var sb = new StringBuilder();
            sb.Append(authorizeEndpoint);
            sb.Append("?");
            sb.Append($"response_type=code&client_id={Uri.EscapeDataString(user.ClientId)}");
            if (!string.IsNullOrWhiteSpace(redirect_uri))
            {
                sb.Append($"&redirect_uri={Uri.EscapeDataString(redirect_uri)}");
            }
            sb.Append("&code_challenge_method=S256");
            sb.Append($"&code_challenge={Uri.EscapeDataString(codeChallenge)}");
            sb.Append($"&scope={Uri.EscapeDataString("openid profile email api")}");

            var url = sb.ToString();
            return Redirect(url);
        }

        private static string GenerateCodeVerifier()
        {
            // RFC 7636: code_verifier length 43-128 characters
            var rng = RandomNumberGenerator.Create();
            var bytes = new byte[64];
            rng.GetBytes(bytes);
            return Base64UrlEncode(bytes);
        }

        private static string ComputeCodeChallenge(string codeVerifier)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.ASCII.GetBytes(codeVerifier);
            var hash = sha256.ComputeHash(bytes);
            return Base64UrlEncode(hash);
        }

        private static string Base64UrlEncode(byte[] input)
        {
            return Convert.ToBase64String(input)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }
}
