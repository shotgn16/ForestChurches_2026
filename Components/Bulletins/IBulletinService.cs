using ForestChurches.Models;

namespace ForestChurches.Components.Bulletins
{
    public interface IBulletinService
    {
        Task<Bulletin?> GetActiveBulletinAsync();
        Task<Bulletin?> GetBannerBulletinAsync();
        Task<Bulletin?> GetLatestBulletinAsync();
        Task<List<Bulletin>> GetAllAsync();
        Task<Bulletin?> GetByIdAsync(int id);
        Task DeleteAsync(int id);
        Task<Bulletin> CreateOrUpdateAsync(Bulletin bulletin);
        Task<Bulletin?> DuplicateLatestAsync(DateTime newDate);
    }
}
