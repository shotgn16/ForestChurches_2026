using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ForestChurches.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly ForestChurchesContext _context;

        public IndexModel(ILogger<IndexModel> logger, ForestChurchesContext context)
        {
            _logger = logger;
            _context = context;
        }

        /// <summary>Denominations offered in the search's "More filters" panel.</summary>
        public List<string> Denominations { get; private set; } = new();

        /// <summary>The next few events across all open churches, for the "What's on" strip.</summary>
        public List<EventsModel> UpcomingEvents { get; private set; } = new();

        public async Task OnGetAsync()
        {
            // The home page must never fail because of a database hiccup: fall back to an empty strip.
            try
            {
                Denominations = await _context.ChurchInformation
                    .Where(c => c.Status == "Open")
                    .Select(c => c.Denomination)
                    .Distinct()
                    .OrderBy(d => d)
                    .ToListAsync();

                var today = DateOnly.FromDateTime(DateTime.Today);
                UpcomingEvents = await _context.Events
                    .Where(e => e.Date >= today)
                    .Where(e => _context.ChurchInformation.Any(c => c.Name == e.Church && c.Status == "Open"))
                    .OrderBy(e => e.Date).ThenBy(e => e.StartTime)
                    .Take(4)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to load home page lookups");
            }
        }
    }
}
