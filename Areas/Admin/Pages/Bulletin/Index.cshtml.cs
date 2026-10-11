using ForestChurches.Components.Bulletins;
using ForestChurches.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ForestChurches.Areas.Admin.Pages.Bulletin
{
    [Authorize(Policy = "Admin.Read")]
    public class IndexModel : PageModel
    {
        private readonly IBulletinService _service;
        public List<ForestChurches.Models.Bulletin> Bulletins { get; set; } = new List<ForestChurches.Models.Bulletin>();

        [TempData]
        public string StatusMessage { get; set; }

        public IndexModel(IBulletinService service)
        {
            _service = service;
        }

        public async Task OnGetAsync()
        {
            Bulletins = await _service.GetAllAsync();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            await _service.DeleteAsync(id);
            StatusMessage = "Bulletin deleted.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostToggleBannerAsync(int id)
        {
            var b = await _service.GetByIdAsync(id);
            if (b == null) return NotFound();
            // Toggle visibility
            b.BannerVisible = !b.BannerVisible;
            // If enabling this bulletin for banner, ensure others are disabled so only one banner shows at a time
            if (b.BannerVisible)
            {
                var all = await _service.GetAllAsync();
                foreach (var other in all)
                {
                    if (other.Id != b.Id && other.BannerVisible)
                    {
                        other.BannerVisible = false;
                        // also clear IsActive on others if necessary
                        other.IsActive = false;
                        await _service.CreateOrUpdateAsync(other);
                    }
                }
                // Make this bulletin the active one so public page shows it
                b.IsActive = true;
            }
            await _service.CreateOrUpdateAsync(b);
            StatusMessage = b.BannerVisible ? "Banner shown." : "Banner hidden.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSetActiveAsync(int id)
        {
            // Ensure only one bulletin is active
            var all = await _service.GetAllAsync();
            foreach (var other in all)
            {
                if (other.Id != id && other.IsActive)
                {
                    other.IsActive = false;
                    await _service.CreateOrUpdateAsync(other);
                }
            }
            var b = await _service.GetByIdAsync(id);
            if (b == null) return NotFound();
            b.IsActive = true;
            await _service.CreateOrUpdateAsync(b);
            StatusMessage = "Active bulletin updated.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSetExpiryAsync(int id, DateTime? expiresAt)
        {
            var b = await _service.GetByIdAsync(id);
            if (b == null) return NotFound();
            b.BannerExpiresAt = expiresAt;
            await _service.CreateOrUpdateAsync(b);
            StatusMessage = "Expiry updated.";
            return RedirectToPage();
        }
    }
}
