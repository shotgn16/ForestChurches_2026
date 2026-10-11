using ForestChurches.Components.Logging;
using ForestChurches.Components.Pageination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;

namespace ForestChurches.Pages.Admin
{
    [Authorize(Policy = "Admin.Read")]
    public class AdminLogsModel : PageModel
    {
        private readonly LogRetriver _logRetriver;
        private const int PageSize = 50; // fixed page size

        public AdminLogsModel(LogRetriver logRetriver)
        {
            _logRetriver = logRetriver;
        }

        public PaginatedList<Models.LogEntry> Logs { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public string SearchQuery { get; set; }

        public async Task OnGetAsync(int currentPage = 1, string searchQuery = null)
        {
            Logs = await _logRetriver.LogsOnCall(currentPage, PageSize, searchQuery);
            CurrentPage = currentPage;
            TotalPages = Logs.TotalPages;
            SearchQuery = searchQuery;
        }

        // Handler used by client JS polling to refresh the table
        public async Task<JsonResult> OnGetLogsAsync(int currentPage = 1, string searchQuery = null)
        {
            var logs = await _logRetriver.LogsOnCall(currentPage, PageSize, searchQuery);
            // Return plain objects (not EF entities) and ensure camelCase for JS
            var payload = logs.Select(l => new {
                id = l.Id,
                timestamp = l.Timestamp,
                level = l.Level,
                exception = l.Exception,
                message = l.Message,
                template = l.Template,
                properties = l.Properties
            }).ToList();
            return new JsonResult(new { logs = payload, currentPage = currentPage, totalPages = logs.TotalPages });
        }

        // Handler for downloading logs as CSV
        public async Task<IActionResult> OnPostDownloadAsync(string fromDate, string toDate, string searchQuery = null)
        {
            if (!DateTime.TryParse(fromDate, out var parsedFromDate))
            {
                return BadRequest("Invalid from date");
            }

            if (!DateTime.TryParse(toDate, out var parsedToDate))
            {
                return BadRequest("Invalid to date");
            }

            // Ensure toDate includes the entire day
            parsedToDate = parsedToDate.AddHours(23).AddMinutes(59).AddSeconds(59);

            // Retrieve logs within the date range
            var logs = await _logRetriver.GetLogsWithDateRange(parsedFromDate, parsedToDate, searchQuery);

            // Generate CSV content
            var csv = new StringBuilder();
            csv.AppendLine("Id,Timestamp,Level,Exception,Message,Template,Properties");

            foreach (var log in logs)
            {
                csv.AppendLine(FormatCsvLine(log.Id, log.Timestamp, log.Level, log.Exception, log.Message, log.Template, log.Properties));
            }

            var fileName = $"logs_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            var bytes = Encoding.UTF8.GetBytes(csv.ToString());

            return File(bytes, "text/csv", fileName);
        }

        private string FormatCsvLine(params string[] fields)
        {
            var escapedFields = fields.Select(f => EscapeCsvField(f ?? ""));
            return string.Join(",", escapedFields);
        }

        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return "\"\"";

            if (field.Contains("\"") || field.Contains(",") || field.Contains("\n") || field.Contains("\r"))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }

            return "\"" + field + "\"";
        }
    }
}