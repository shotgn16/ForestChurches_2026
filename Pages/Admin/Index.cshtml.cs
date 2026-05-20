using ForestChurches.Areas.Identity.Data;
using ForestChurches.Components.Performance;
using ForestChurches.Components.Users;
using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ForestChurches.Pages.Admin
{
    [Authorize(Policy = "Admin.Read")]
    public class IndexModel : PageModel
    {
        private readonly ForestChurchesContext _context;
        private readonly IDatabaseService _databaseService;

        internal List<Models.ChurchInformation> registeredChurches;
        internal List<EventsModel> registeredEvents;
        internal ChurchAccount newestUser;

        public IndexModel(ForestChurchesContext context, IDatabaseService databaseService)
        {
            _databaseService = databaseService;
            _context = context;
        }
        public float CPUUsage { get; private set; }
        public float MemoryUsage { get; private set; }
        public float DatabaseSize { get; private set; }
        public float LogDatabaseSize { get; private set; }
        public int WhitelistCount { get; private set; }

        public async Task OnGetAsync()
        {
            registeredChurches = _context.ChurchInformation.OrderByDescending(a => a.ChurchAccount.CreatedDate).ToList();
            registeredEvents = _context.Events.ToList();

            WhitelistCount = _context.WhitelistedUsers.Where(i => i.Status == "Pending").ToList().Count;
            newestUser = _context.Users.OrderByDescending(a => a.CreatedDate).FirstOrDefault();

            DatabaseSize = await _databaseService.GetDatabaseSize();
            LogDatabaseSize = await _databaseService.GetLogDatabaseSize();
        }
    }
}
