using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ForestChurches.Pages.Admin.Benefices
{
    /// <summary>
    /// Benefices group churches on the public site ("Part of the … Benefice" with links to the sister churches).
    /// Churches are put into a benefice from Admin → Churches → Edit.
    /// </summary>
    [Authorize(Policy = "Admin.Read")]
    public class IndexModel : PageModel
    {
        private readonly ForestChurchesContext _context;
        private readonly ILogger<IndexModel> _logger;
        private readonly Components.Security.Encoder _encoder;

        public IndexModel(ForestChurchesContext context, ILogger<IndexModel> logger, Components.Security.Encoder encoder)
        {
            _context = context;
            _logger = logger;
            _encoder = encoder;
        }

        public List<Benefice> Benefices { get; private set; } = new();
        public Dictionary<string, string> AccountEmails { get; private set; } = new();

        [TempData]
        public string? StatusMessage { get; set; }

        public string ChurchEditKey(string churchId) => _encoder.Protect(churchId, "fc.data.admin.church");

        public async Task OnGetAsync()
        {
            Benefices = await _context.Benefices
                .Include(b => b.Churches)
                .OrderBy(b => b.Name)
                .ToListAsync();
            var accountIds = Benefices.SelectMany(b => b.Churches).Select(c => c.ChurchAccountId).Distinct().ToList();
            AccountEmails = await _context.Users.Where(u => accountIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Email ?? u.UserName ?? u.Id);
        }

        private static string? Clean(string? value, int max) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length > max ? value.Trim()[..max] : value.Trim();

        private static string? CleanUrl(string? value)
        {
            var v = Clean(value, 300);
            return v != null && Uri.TryCreate(v, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp) ? u.ToString() : null;
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostCreateAsync(string? name, string? description, string? website)
        {
            var clean = Clean(name, 120);
            if (clean == null)
            {
                StatusMessage = "Error: enter the benefice's name.";
                return RedirectToPage();
            }
            if (await _context.Benefices.AnyAsync(b => b.Name == clean))
            {
                StatusMessage = $"Error: there's already a benefice called {clean}.";
                return RedirectToPage();
            }
            _context.Benefices.Add(new Benefice { Name = clean, Description = Clean(description, 1000), Website = CleanUrl(website) });
            await _context.SaveChangesAsync();
            StatusMessage = $"Added the {clean} benefice. Add churches to it from Churches → Edit.";
            return RedirectToPage();
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostUpdateAsync(string id, string? name, string? description, string? website)
        {
            var benefice = await _context.Benefices.FirstOrDefaultAsync(b => b.Id == id);
            var clean = Clean(name, 120);
            if (benefice == null || clean == null)
            {
                StatusMessage = "Error: that benefice couldn't be updated.";
                return RedirectToPage();
            }
            benefice.Name = clean;
            benefice.Description = Clean(description, 1000);
            benefice.Website = CleanUrl(website);
            await _context.SaveChangesAsync();
            StatusMessage = $"Saved {clean}.";
            return RedirectToPage();
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostDeleteAsync(string id)
        {
            var benefice = await _context.Benefices.Include(b => b.Churches).FirstOrDefaultAsync(b => b.Id == id);
            if (benefice == null)
            {
                StatusMessage = "Error: that benefice no longer exists.";
                return RedirectToPage();
            }
            foreach (var c in benefice.Churches) c.BeneficeId = null; // churches stay; they're just no longer grouped
            _context.Benefices.Remove(benefice);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Site admin {Admin} deleted benefice {Benefice}", User.Identity?.Name, benefice.Name);
            StatusMessage = $"Deleted the {benefice.Name} benefice. Its churches are unchanged.";
            return RedirectToPage();
        }
    }
}
