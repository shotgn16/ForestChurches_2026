using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace ForestChurches.Components.FileManager
{
    public class FileController : Controller, FileInterface
    {
        private ForestChurchesContext _context;
        private readonly ILogger<FileController> _logger;
        private const string SafeDirectory = "SafeFiles";
        public FileController(ILogger<FileController> logger, ForestChurchesContext context)
        {
            _context = context;
            _logger = logger;
            Directory.CreateDirectory(SafeDirectory);
        }
        public async Task<IActionResult> CreateFile(Models.ChurchInformation content, string contentType, string userProvidedFileName = "")
        {
            if (content == null) // Filename can be empty, but content cannot
            {
                return BadRequest("Content cannot be null.");
            }

            string fileContent = await GenerateFileContent(content, userProvidedFileName);

            // Generate a safe filename - crucial for preventing path traversal
            string safeFileName = $"{Guid.NewGuid()}.txt"; // Use a GUID for uniqueness
            string filePath = Path.Combine(SafeDirectory, safeFileName);

            try
            {
                await System.IO.File.WriteAllTextAsync(filePath, fileContent);

                var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                return new FileStreamResult(fileStream, contentType)
                {
                    FileDownloadName = userProvidedFileName == "" ? safeFileName : EnsureValidFilename(userProvidedFileName) + ".txt" // Use original name for download if provided, sanitized.
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create file: {ex.Message}");
                return StatusCode(500, "Internal server error while creating the file.");
            }
        }

        private async Task<string> GenerateFileContent(ChurchInformation content, string filename)
        {
            StringBuilder fileContent = new StringBuilder();
            string openingHours = await returnOpeningHours(content);

            try
            {
                if (content != null && filename == "Church Information")
                {
                    fileContent
                        .AppendLine("=========================")
                        .AppendLine($"Status: {content.Status}")
                        .AppendLine("=========================")
                        .AppendLine($"Church Name: {content.Name ?? "Unavailable"}")
                        .AppendLine($"Description: {content.Description ?? "Unavailable"}")
                        .AppendLine($"Denomination: {content.Denomination ?? "Unavailable"}")
                        .AppendLine($"Congregation: {content.Congregation ?? "Unavailable"}")
                        .AppendLine("\nContact Information")
                        .AppendLine($"Website: {content.Website.ToString() ?? "Unavailable"}")
                        .AppendLine($"Address: {content.Address ?? "Unavailable"}")
                        .AppendLine($"Phone: {content.Phone ?? "Unavailable"}")
                        .AppendLine("\nFacilities")
                        .AppendLine($"Wheelchair Church access: {(content.WheelchairAccess == true ? "Available" : "Unavailable")}")
                        .AppendLine($"Refreshments: {(content.Refreshments == true ? "Available" : "Unavailable")}")
                        .AppendLine($"Restrooms: {(content.Restrooms == true ? "Available" : "Unavailable")}")
                        .AppendLine($"WiFi: {(content.Wifi == true ? "Available" : "Unavailable")}")
                        .AppendLine($"Parking: {(content.Parking == true ? "Available" : "Unavailable")}")
                        .AppendLine("\n\n" + openingHours);
                }

                else if (content == null)
                {
                    fileContent.AppendLine("An error occurred while processing your request...\nPlease try again later.\n\nIf this issue persists, please contact our support team: support@forestchurches.co.uk");
                }
            }

            catch (Exception ex)
            {
               _logger.LogError($"Error occured while generating file content: {ex.Message}");
                throw ex;
            }

            return fileContent.ToString();
        }

        private string EnsureValidFilename(string fileName)
        {
            // Improved Sanitization -  A more comprehensive approach.
            // Consider more specific allowed characters if needed.
            return System.Text.RegularExpressions.Regex.Replace(fileName, @"[^a-zA-Z0-9_\-\.]", "_");
        }

        private async Task<string> returnOpeningHours(ChurchInformation church)
        {
            StringBuilder output = new StringBuilder();
            var serviceTimes = _context.ServiceTimes.Where(a => a.ChurchInformationId == church.ID).ToList();
            try
            {
                if (church.ServiceTimes != null)
                {
                    output.AppendLine("Service Times:\n");

                    foreach (var item in serviceTimes)
                    {
                        output.AppendLine($"{item.Note}: {item.Time}");
                    }
                }
                else
                {
                    output.Append("No service times available");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error occured while returning opening hours: {ex.Message}");
            }

            return output.ToString();
        }
    }
}
