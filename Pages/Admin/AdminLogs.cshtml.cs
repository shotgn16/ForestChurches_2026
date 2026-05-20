using ForestChurches.Components.Logging;
using ForestChurches.Components.Pageination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ForestChurches.Pages.Admin
{
    [Authorize(Policy = "Admin.Read")]
    public class AdminLogsModel : PageModel
    {
        private readonly LogRetriver _logRetriver;

        public AdminLogsModel(LogRetriver logRetriver)
        {
            _logRetriver = logRetriver;
        }

        public PaginatedList<Models.LogEntry> Logs { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }

        public async Task OnGetAsync(int currentPage = 1)
        {
            int pageSize = 100; // Number of logs per page
            Logs = await _logRetriver.LogsOnCall(currentPage, pageSize);
            CurrentPage = currentPage;
            TotalPages = Logs.TotalPages;
        }
    }

}