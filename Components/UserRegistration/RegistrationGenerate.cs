using Azure.Core;
using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;
using System.Security.Cryptography;

namespace ForestChurches.Components.UserRegistration
{
    public class RegistrationGenerate : iRegistrationGenerate
    {
        private iCryptoGraphic _CryptoGraphic;
        private readonly ForestChurchesContext _context;

        public RegistrationGenerate(iCryptoGraphic cryptoGraphic, ForestChurchesContext context)
        {
            _CryptoGraphic = cryptoGraphic;
            _context = context;
        }

        public string GenerateUrl(HttpRequest request, string userEmail)
        {
            byte[] hashedEmail = _CryptoGraphic.Hash(userEmail, new byte[32]);
               
            _context.SaveChanges();

            _context.WhitelistedUsers.Where(c => c.Email == userEmail)
                .ExecuteUpdate(s => s.SetProperty(a => 
                    a.ExpiryDate, DateTime.UtcNow.AddHours(24)));

            // Generate the activation URL

            string baseUrl = $"{request.Scheme}://{request.Host}";
            string activationPath = $"/Identity/Account/register?email={Uri.EscapeDataString(userEmail)}";
            string activationUrl = baseUrl + activationPath;

            // Set a secure expiration mechanism
            // For example:
            // Schedule a task to delete or mark the activation code as expired in the database after timeoutMinutes

            return activationUrl;
        }

        public bool ValidateEmail(string userEmail)
        {
            var registrationRecord = _context.WhitelistedUsers
                .Where(r => r.Email == userEmail).FirstOrDefault();

            if (registrationRecord == null)
            {
                return false;
            }

            return registrationRecord.Status == "Pending" && !HasExpired(registrationRecord.ExpiryDate);
        }

        public void MarkAsCompleted(string userEmail, DateTime registeredDate)
        {
            var registrationRecord = _context.WhitelistedUsers
                .Where(r => r.Email == userEmail).FirstOrDefault();

            if (registrationRecord == null)
            {
                // Handle the case where the registration record is not found
                // For example, you could throw an exception or log an error
                throw new InvalidOperationException("Registration record not found.");
            }

            registrationRecord.Status = "Completed";
            registrationRecord.DateOfRegistration = registeredDate;
                _context.Update(registrationRecord);
                _context.SaveChanges();
        }

        private bool HasExpired(DateTime expiryDate)
        {
            return DateTime.UtcNow > expiryDate;
        }
    }
}
