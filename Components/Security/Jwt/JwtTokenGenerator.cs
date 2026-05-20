using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ForestChurches.Components.Security
{
    public class JwtTokenGenerator
    {
        private readonly Configuration.Configuration _configurationClass;

        // Cached values
        private readonly string _issuer;
        private readonly string _audience;
        private readonly string _secretKey;

        public JwtTokenGenerator(Configuration.Configuration configurationClass)
        {
            _configurationClass = configurationClass;

            // Try config first, then Key Vault
            _issuer = _configurationClass?.Client?.GetSecret("jwt-token-issuer")?.Value?.Value ?? throw new Exception("Error: Unable to create JWT Token, please try again later...");
            _audience = _configurationClass?.Client?.GetSecret("jwt-token-audience")?.Value?.Value ?? throw new Exception("Error: Unable to create JWT Token, please try again later..."); ;
            _secretKey = _configurationClass?.Client?.GetSecret("jwt-token-securityKey")?.Value?.Value ?? throw new Exception("Error: Unable to create JWT Token, please try again later...");
        }

        public string GenerateToken(int expireMinutes = 10)
        {
            if (string.IsNullOrWhiteSpace(_secretKey))
                throw new InvalidOperationException("JWT secret key is not configured.");

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(expireMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}