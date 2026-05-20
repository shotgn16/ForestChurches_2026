using ForestChurches.Models;
using Microsoft.AspNetCore.DataProtection;
using System.Collections.Generic;
using System.Text.Encodings.Web;

namespace ForestChurches.Components.Security
{
    public class Encoder
    {
        private readonly Dictionary<string, IDataProtector> _dataProtectors;
        private readonly HtmlEncoder _htmlEncoder;
        private readonly UrlEncoder _urlEncoder;

        public Encoder(IDataProtectionProvider dataProtectionProvider, HtmlEncoder htmlEncoder, UrlEncoder urlEncoder)
        {
            _htmlEncoder = htmlEncoder;
            _urlEncoder = urlEncoder;

            // Initialize protectors with different names
            _dataProtectors = new Dictionary<string, IDataProtector>
            {
                { "fc.data.contact", dataProtectionProvider.CreateProtector("fc.data.contact") },
                { "fc.data.user.events", dataProtectionProvider.CreateProtector("fc.data.user.events") },
                { "fc.data.admin.events", dataProtectionProvider.CreateProtector("fc.data.admin.events") },
                { "fc.data.url.email", dataProtectionProvider.CreateProtector("fc.data.url.email") },
                { "fc.data.url.change", dataProtectionProvider.CreateProtector("fc.data.url.change") },
                { "fc.data.manage.church", dataProtectionProvider.CreateProtector("fc.data.manage.church") },
                { "fc.data.manage.email", dataProtectionProvider.CreateProtector("fc.data.manage.email") },
                { "fc.data.mail.confirm", dataProtectionProvider.CreateProtector("fc.data.mail.confirm") },
                { "fc.data.login.external", dataProtectionProvider.CreateProtector("fc.data.login.external") },
                { "fc.data.admin.church", dataProtectionProvider.CreateProtector("fc.data.admin.church") },
                { "fc.data.admin.contact", dataProtectionProvider.CreateProtector("fc.data.admin.contact") },
                { "fc.data.admin.invite", dataProtectionProvider.CreateProtector("fc.data.admin.invite") },
                { "fc.data.admin.role", dataProtectionProvider.CreateProtector("fc.data.admin.role") },
                { "fc.data.admin.users", dataProtectionProvider.CreateProtector("fc.data.admin.users") },
                { "fc.data.church.information", dataProtectionProvider.CreateProtector("fc.data.church.information") },
                { "fc.data.google", dataProtectionProvider.CreateProtector("fc.data.google") }
                // Add more protectors as needed
            };
        }

        public string Protect<T>(T data, string protectorName)
        {
            if (_dataProtectors.TryGetValue(protectorName, out var protector))
            {
                string dataString = data?.ToString();
                return protector.Protect(dataString);
            }
            throw new ArgumentException($"Protector '{protectorName}' not found.");
        }

        public T Unprotect<T>(string data, string protectorName)
        {
            if (_dataProtectors.TryGetValue(protectorName, out var protector))
            {
                string unprotectedData = protector.Unprotect(data);
                return (T)Convert.ChangeType(unprotectedData, typeof(T));
            }
            throw new ArgumentException($"Protector '{protectorName}' not found.");
        }

        public string HtmlEncode<T>(T data)
        {
            string dataString = data?.ToString();

            // Add this check to prevent the crash
            if (dataString == null)
            {
                return string.Empty;
            }

            return _htmlEncoder.Encode(dataString);
        }

        public string HtmlDecode<T>(T data)
        {
            string dataString = data?.ToString();
            return System.Net.WebUtility.HtmlDecode(dataString);
        }

        public string UrlEncode<T>(T data)
        {
            string dataString = data?.ToString();
            return _urlEncoder.Encode(dataString);
        }

        public string UrlDecode<T>(T data)
        {
            string dataString = data?.ToString();
            return System.Net.WebUtility.UrlDecode(dataString);
        }
    }
}