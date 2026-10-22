using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ForestChurches.Components.Token;
using ForestChurches.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace ForestChurches.Components.Email
{
    public class MailSender : iMailSender
    {
        private readonly iEmail _mailRepository;
        private readonly ILogger<MailSender> _logger;
        private readonly CallbackTokenDataFormat _tokenDataFormat;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ForestChurches.Components.Security.Encoder _Encoder;
        private readonly ForestChurches.Components.Storage.IBlobUrlService _blobUrlService;

        Dictionary<string, string> UserData;

        public MailSender(iEmail mailRepository, ILogger<MailSender> logger, IDataProtectionProvider dataProtectionProvider, IHttpContextAccessor httpContextAccessor, ForestChurches.Components.Security.Encoder Encoder, ForestChurches.Components.Storage.IBlobUrlService blobUrlService)
        {
            _httpContextAccessor = httpContextAccessor;
            _mailRepository = mailRepository;
            _blobUrlService = blobUrlService;
            _Encoder = Encoder;
            _logger = logger;

            _tokenDataFormat = new CallbackTokenDataFormat(dataProtectionProvider, "User  Registration");
        }
        public void SendEmailEventDeleted(string email, string un, string redirectUrl, EventsModel deletedEvent)
        {
            try
            {
                var token = GenerateUniqueToken(email, DateTime.UtcNow.AddDays(1), redirectUrl);

                UserData = new Dictionary<string, string>
                {
                    { "{event_link}", GenerateEmailLink(token) },
                    { "{eventName}", Html(deletedEvent.Name) },
                    { "{eventDate}", EmailDate(deletedEvent.Date) },
                    { "{eventStart}", EmailTime(deletedEvent.StartTime) },
                    { "{eventEnd}", EmailTime(deletedEvent.EndTime) }
                };

                EnsureImagePlaceholders(UserData, "./templates/event_deleted.html");

                // Fire-and-forget to preserve original synchronous surface
                _ = _mailRepository.StartEmailAsync(un, UserData, "Event Deleted", "./templates/event_deleted.html");
            }

            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                throw;
            }
        }
        public void SendEmailEventCreated(string email, string redirectUrl, string repeats, EventsModel addedEvent)
        {
            try
            {
                var token = GenerateUniqueToken(email, DateTime.UtcNow.AddDays(1), redirectUrl);

                UserData = new Dictionary<string, string> {
                        { "{event_name}", Html(addedEvent.Name) },
                        { "{event_description}", HtmlMultiline(addedEvent.Description) },
                        { "{event_location}", Html(addedEvent.Address) },
                        { "{event_date}", EmailDate(addedEvent.Date) },
                        { "{event_startTime}", EmailTime(addedEvent.StartTime) },
                        { "{event_endTime}", EmailTime(addedEvent.EndTime) },
                        { "{event_repeats}", Html(repeats) },
                        { "{event_link}", GenerateEmailLink(token)  }
                    };

                EnsureImagePlaceholders(UserData, "./templates/event_created.html");

                _ = _mailRepository.StartEmailAsync(email, UserData, "Event Created", "./templates/event_created.html");
            }

            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                throw;
            }
        }
        public void SendEmailEventValidate(string email, string un, string redirectUrl, EventsModel eventItem)
        {
            try
            {
                var token = GenerateUniqueToken(email, DateTime.UtcNow.AddDays(1), redirectUrl);

                UserData = new Dictionary<string, string> {
                        { "{eventName}", Html(eventItem.Name) },
                        { "{eventDate}", EmailDate(eventItem.Date) },
                        { "{eventStart}", EmailTime(eventItem.StartTime) },
                        { "{eventEnd}", EmailTime(eventItem.EndTime) },
                        { "{event_link}", GenerateEmailLink(token) }
                    };

                EnsureImagePlaceholders(UserData, "./templates/confirm_repeated_event.html");

                _ = _mailRepository.StartEmailAsync(un, UserData, "Do you still need this?", "./templates/confirm_repeated_event.html");
            }

            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                throw;
            }
        }
        public void SendEmailContactRequest(string email, string un, string recipientEmail, string senderEmail, string msg, string redirectUrl)
        {
            try
            {
                UserData = new Dictionary<string, string>
                {
                        { "{template_title}", "Forest Churches Contact Form" },
                        { "{template_content}", $"Name: {Html(un)} <br /><br /> Email: {Html(senderEmail)} <br /><br /> Message: {HtmlMultiline(msg)}" },
                        { "{template_button_name}", "View Messages" }
                    };

                EnsureImagePlaceholders(UserData, "./templates/admin_email.html");

                _ = _mailRepository.StartEmailAsync(recipientEmail, UserData, "Forest Churches Contact Form", "./templates/admin_email.html");
            }

            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                throw;
            }
        }
        public async Task SendEmailAdminContactRequest(string recipientUsername, string recipientEmail, string msg, string senderName)
        {
            try
            {
                UserData = new Dictionary<string, string>
                {
                    { "{admin_name}", Html(senderName) },
                    { "{username}", Html(recipientUsername) },
                    { "{paragraph}", HtmlMultiline(msg) }
                };

                EnsureImagePlaceholders(UserData, "./templates/contact_request.html");

                _ = _mailRepository.StartEmailAsync(recipientEmail, UserData, "Forest Church Administration Team", "./templates/contact_request.html");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                throw;
            }
        }
        public void SendEmailChurchInformation(string recipientEmail, ChurchInformation church, string serviceTimes)
        {
            try
            {
                var token = GenerateUniqueToken(recipientEmail, DateTime.UtcNow.AddDays(1), "/Contact?cid=" + _Encoder.Protect(church.ID, "fc.data.contact"));
                var emailLink = GenerateEmailLink(token);

                UserData = new Dictionary<string, string>
                {
                    { "{username}", Html(recipientEmail) },
                    { "{church_name}", Html(church.Name ?? "Not available") },
                    { "{church_address}", Html(church.Address ?? "Not available") },
                    { "{church_phone}", Html(church.Phone ?? "Not available") },
                    { "{church_website}", Html(church.Website?.ToString() ?? "Not available") },
                    { "{church_wheelchairEntrance}", church.WheelchairAccess == null ? "Not available" : (church.WheelchairAccess ? "Available" : "Unavailable") },
                    { "{serviceTimes}", serviceTimes },
                    { "{contact_form_designated_church_id}", emailLink },
                };

                EnsureImagePlaceholders(UserData, "./templates/church_information.html");

                _ = _mailRepository.StartEmailAsync(recipientEmail, UserData, "Your Requested Information", "./templates/church_information.html");
            }

            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                throw;
            }
        }
        public void SendNewEmailConfirmation(string un, string redirectUrl)
        {
            try
            {
                UserData = new Dictionary<string, string>
                {
                    { "{confirm_email_link}", redirectUrl },
                };

                EnsureImagePlaceholders(UserData, "./templates/confirm_emailchange.html");

                string unProtectedUsername;
                try
                {
                    // Attempt to unprotect if the incoming value was protected; otherwise fall back to the raw value
                    unProtectedUsername = _Encoder.Unprotect<string>(un, "fc.data.mail.confirm");
                }
                catch
                {
                    unProtectedUsername = un;
                }

                _ = _mailRepository.StartEmailAsync(unProtectedUsername, UserData, "Confirm New Email", "./templates/confirm_emailchange.html");
            }

            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                throw;
            }
        }
        public void SendEmailConfirmAccount(string un, string redirectUrl)
        {
            try
            {
                UserData = new Dictionary<string, string>
                {
                    { "{confirm_email_link}", redirectUrl },

                    { "{IMAGE-1}", _blobUrlService.GetReadSasUrl("IMAGE-1.png", TimeSpan.FromDays(7)) ?? string.Empty },
                    { "{IMAGE-2}", _blobUrlService.GetReadSasUrl("IMAGE-2.png", TimeSpan.FromDays(7)) ?? string.Empty },
                    { "{IMAGE-3}", _blobUrlService.GetReadSasUrl("IMAGE-3.png", TimeSpan.FromDays(7)) ?? string.Empty },
                    { "{IMAGE-4}", _blobUrlService.GetReadSasUrl("IMAGE-4.png", TimeSpan.FromDays(7)) ?? string.Empty },
                    { "{IMAGE-5}", _blobUrlService.GetReadSasUrl("IMAGE-5.png", TimeSpan.FromDays(7)) ?? string.Empty },
                    { "{IMAGE-6}", _blobUrlService.GetReadSasUrl("IMAGE-6.png", TimeSpan.FromDays(7)) ?? string.Empty },
                    { "{IMAGE-7}", _blobUrlService.GetReadSasUrl("IMAGE-7.png", TimeSpan.FromDays(7)) ?? string.Empty }
                };

                EnsureImagePlaceholders(UserData, "./templates/confirm_email.html");

                string UnprotectedUsername;
                try
                {
                    UnprotectedUsername = _Encoder.Unprotect<string>(un, "fc.data.mail.confirm");
                }
                catch
                {
                    UnprotectedUsername = un;
                }

                _ = _mailRepository.StartEmailAsync(UnprotectedUsername, UserData, "Confirm your account", "./templates/confirm_email.html");
            }

            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                throw;
            }
        }
        public void SendEmailRegisteredUser(string email, string un, string redirectUrl)
        {
            try
            {
                var request = _httpContextAccessor.HttpContext.Request;

                UserData = new Dictionary<string, string>
                {
                    { "{username}", Html(un) },
                    { "{website_home_page_link}", $"{request.Scheme}://{request.Host.Value}/" },
                    { "{church_profile_link}", $"{request.Scheme}://{request.Host.Value}/Identity/Account/Manage/ChurchProfile" },
                    { "{user_events_link}", $"{request.Scheme}://{request.Host.Value}/Events" },

                    { "{IMAGE-1}", _blobUrlService.GetReadSasUrl("IMAGE-1.png", TimeSpan.FromDays(7)) ?? string.Empty },
                    { "{IMAGE-2}", _blobUrlService.GetReadSasUrl("IMAGE-2.png", TimeSpan.FromDays(7)) ?? string.Empty },
                    { "{IMAGE-3}", _blobUrlService.GetReadSasUrl("IMAGE-3.png", TimeSpan.FromDays(7)) ?? string.Empty },
                    { "{IMAGE-4}", _blobUrlService.GetReadSasUrl("IMAGE-4.png", TimeSpan.FromDays(7)) ?? string.Empty },
                    { "{IMAGE-5}", _blobUrlService.GetReadSasUrl("IMAGE-5.png", TimeSpan.FromDays(7)) ?? string.Empty },
                    { "{IMAGE-6}", _blobUrlService.GetReadSasUrl("IMAGE-6.png", TimeSpan.FromDays(7)) ?? string.Empty },
                    { "{IMAGE-7}", _blobUrlService.GetReadSasUrl("IMAGE-7.png", TimeSpan.FromDays(7)) ?? string.Empty }
                };
                EnsureImagePlaceholders(UserData, "./templates/registration_complete.html");

                _ = _mailRepository.StartEmailAsync(email, UserData, "Thanks for Registering", "./templates/registration_complete.html");
            }

            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                throw;
            }
        }
        public async Task SendEmailResetPassword(string email, string redirectUrl)
        {
            try
            {
                var request = _httpContextAccessor.HttpContext.Request;

                // Password reset links only last 2 hours! (GDPR Purposes)
                var token = GenerateUniqueToken(email, DateTime.UtcNow.AddHours(2), redirectUrl);
                var emailLink = GenerateEmailLink(token);

                UserData = new Dictionary<string, string>
                {
                    { "{contact_us_page}", $"{request.Scheme}://{request.Host.Value}/Contact" },
                    { "{password_reset_expires}", EmailDateTime(DateTime.UtcNow.AddHours(2)) },
                    { "{password_reset_link}", emailLink },

                    { "{IMAGE-1}", _blobUrlService.GetReadSasUrl("IMAGE-1.png", TimeSpan.FromDays(7)) ?? string.Empty },
                    { "{IMAGE-2}", _blobUrlService.GetReadSasUrl("IMAGE-2.png", TimeSpan.FromDays(7)) ?? string.Empty },
                    { "{IMAGE-3}", _blobUrlService.GetReadSasUrl("IMAGE-3.png", TimeSpan.FromDays(7)) ?? string.Empty },
                    { "{IMAGE-4}", _blobUrlService.GetReadSasUrl("IMAGE-4.png", TimeSpan.FromDays(7)) ?? string.Empty },
                    { "{IMAGE-5}", _blobUrlService.GetReadSasUrl("IMAGE-5.png", TimeSpan.FromDays(7)) ?? string.Empty },
                    { "{IMAGE-6}", _blobUrlService.GetReadSasUrl("IMAGE-6.png", TimeSpan.FromDays(7)) ?? string.Empty },
                    { "{IMAGE-7}", _blobUrlService.GetReadSasUrl("IMAGE-7.png", TimeSpan.FromDays(7)) ?? string.Empty }
                };
                EnsureImagePlaceholders(UserData, "./templates/password_reset.html");

                _ = _mailRepository.StartEmailAsync(email, UserData, "Reset Password", "./templates/password_reset.html");
            }

            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                throw;
            }
        }
        public async Task SendEmailInviteUser(string invitedEmail, string redirectUrl)
        {
            try
            {
                var token = GenerateUniqueToken(invitedEmail, DateTime.UtcNow.AddDays(1), redirectUrl);
                var emailLink = GenerateEmailLink(token);

                UserData = new Dictionary<string, string>
                {
                    { "{email}", Html(invitedEmail) },
                    { "{registration_link}", emailLink },

                    { "{IMAGE-1}", _blobUrlService.GetReadSasUrl("IMAGE-1.png", TimeSpan.FromDays(7)) ?? string.Empty },
                    { "{IMAGE-2}", _blobUrlService.GetReadSasUrl("IMAGE-2.png", TimeSpan.FromDays(7)) ?? string.Empty },
                    { "{IMAGE-3}", _blobUrlService.GetReadSasUrl("IMAGE-3.png", TimeSpan.FromDays(7)) ?? string.Empty },
                    { "{IMAGE-4}", _blobUrlService.GetReadSasUrl("IMAGE-4.png", TimeSpan.FromDays(7)) ?? string.Empty },
                    { "{IMAGE-5}", _blobUrlService.GetReadSasUrl("IMAGE-5.png", TimeSpan.FromDays(7)) ?? string.Empty },
                    { "{IMAGE-6}", _blobUrlService.GetReadSasUrl("IMAGE-6.png", TimeSpan.FromDays(7)) ?? string.Empty },
                    { "{IMAGE-7}", _blobUrlService.GetReadSasUrl("IMAGE-7.png", TimeSpan.FromDays(7)) ?? string.Empty }
                };

                EnsureImagePlaceholders(UserData, "./templates/registration_invite.html");

                _ = _mailRepository.StartEmailAsync(invitedEmail, UserData, "You're Invited", "./templates/registration_invite.html");
            }

            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                throw;
            }
        }

        // EMAIL FORMATTING
        // Dates and times in emails use UK formats, whatever culture the server runs under:
        // "Saturday 17 October 2026" and 24-hour "09:30", matching the website.
        private static readonly System.Globalization.CultureInfo EmailCulture = System.Globalization.CultureInfo.GetCultureInfo("en-GB");

        private static string EmailDate(DateOnly date) => date.ToString("dddd d MMMM yyyy", EmailCulture);

        private static string EmailTime(TimeOnly time) => time.ToString("HH:mm", EmailCulture);

        private static string EmailDateTime(DateTime utc)
        {
            var local = utc;
            try
            {
                var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
                local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);
            }
            catch (Exception) { /* time zone data missing: show UTC rather than fail */ }
            return local.ToString("dddd d MMMM yyyy 'at' HH:mm", EmailCulture);
        }

        // TOKEN MANAGEMENT
        // Ensure image placeholders are present with full SAS URLs (both braced and unbraced keys).
        // If a templatePath is provided, attempt to read the template and only generate SAS URLs
        // for the image placeholders actually referenced by that template.
        private void EnsureImagePlaceholders(Dictionary<string, string> data, string? templatePath = null)
        {
            if (data == null)
                return;

            string? templateContent = null;
            if (!string.IsNullOrWhiteSpace(templatePath))
            {
                try
                {
                    // Try to resolve as absolute or relative to base directory
                    var candidate = templatePath;
                    if (!Path.IsPathRooted(candidate))
                    {
                        // remove leading ./ or similar
                        var trimmed = candidate.TrimStart('.', '/', '\\');
                        candidate = Path.Combine(AppContext.BaseDirectory, trimmed);
                    }

                    if (File.Exists(candidate))
                    {
                        templateContent = File.ReadAllText(candidate);
                    }
                    else
                    {
                        // Search upward for a templates folder containing the requested file name
                        var fileName = Path.GetFileName(templatePath);
                        var dir = new DirectoryInfo(AppContext.BaseDirectory);
                        for (int depth = 0; depth < 6 && dir != null; depth++)
                        {
                            var tplDir = Path.Combine(dir.FullName, "templates");
                            var p = Path.Combine(tplDir, fileName);
                            if (File.Exists(p))
                            {
                                templateContent = File.ReadAllText(p);
                                break;
                            }

                            dir = dir.Parent;
                        }
                    }
                }
                catch
                {
                    // ignore IO exceptions and fall back to default behaviour
                    templateContent = null;
                }
            }

            var indices = new HashSet<int>();
            if (!string.IsNullOrWhiteSpace(templateContent))
            {
                try
                {
                    var imageRegex = new Regex("\\{IMAGE-(\\d+)\\}", RegexOptions.Compiled);
                    var matches = imageRegex.Matches(templateContent);
                    foreach (Match m in matches)
                    {
                        if (int.TryParse(m.Groups[1].Value, out var idx))
                            indices.Add(idx);
                    }
                }
                catch
                {
                    // ignore regex errors and fall back
                    indices.Clear();
                }
            }

            // If no placeholders found in template, fall back to keys already present in data
            if (indices.Count == 0)
            {
                for (int i = 1; i <= 7; i++)
                {
                    var braced = $"{{IMAGE-{i}}}";
                    var bare = $"IMAGE-{i}";
                    if (data.ContainsKey(braced) || data.ContainsKey(bare))
                        indices.Add(i);
                }
            }

            // Final fallback: generate 1..7
            if (indices.Count == 0)
            {
                for (int i = 1; i <= 7; i++)
                    indices.Add(i);
            }

            foreach (var i in indices)
            {
                var blobName = $"IMAGE-{i}.png";
                string url = string.Empty;
                try
                {
                    url = _blobUrlService.GetReadSasUrl(blobName, TimeSpan.FromDays(7)) ?? string.Empty;
                }
                catch
                {
                    url = string.Empty;
                }

                data[$"{{IMAGE-{i}}}"] = url;
            }
        }

        /// <summary>HTML-encodes text that came from a person (forms, church details) before it goes into an email template.</summary>
        private static string Html(string? value) => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);

        /// <summary>HTML-encodes multi-line text and keeps its line breaks.</summary>
        private static string HtmlMultiline(string? value) =>
            Html(value).Replace("\r\n", "\n").Replace("\n", "<br />");

        private string GenerateUniqueToken(string email, DateTime expires, string redirectUrl)
        {
            CallbackToken token = new CallbackToken
            {
                email = email,
                ExpirationTime = expires,
                RedirectUrl = redirectUrl
            };

            return _tokenDataFormat.Protect(token);
        }
        private string GenerateEmailLink(string token)
        {
            var request = _httpContextAccessor.HttpContext.Request;

            var baseUrl = $"{request.Scheme}://{request.Host.Value}/ValidateToken";
            return $"{baseUrl}?token={token}";
        }
        public string ValidateToken(string token)
        {
            try
            {
                var callbackToken = _tokenDataFormat.Unprotect(token);

                if (callbackToken.ExpirationTime < DateTime.UtcNow)
                {
                    return "Token has expired.";
                }

                return callbackToken.RedirectUrl;
            }

            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                throw;
            }
        }
    }
}