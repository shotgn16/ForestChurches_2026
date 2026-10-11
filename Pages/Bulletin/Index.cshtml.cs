using ForestChurches.Components.Bulletins;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ForestChurches.Pages.PublicBulletin
{
    public class IndexModel : PageModel
    {
        private readonly IBulletinService _bulletinService;

        public global::ForestChurches.Models.Bulletin? ActiveBulletin { get; private set; }
        public global::ForestChurches.Models.Bulletin? LatestBulletin { get; private set; }

        public IndexModel(IBulletinService bulletinService)
        {
            _bulletinService = bulletinService;
        }

        public async Task OnGetAsync()
        {
            ActiveBulletin = await _bulletinService.GetActiveBulletinAsync();
            LatestBulletin = await _bulletinService.GetLatestBulletinAsync();
        }
    }
}
