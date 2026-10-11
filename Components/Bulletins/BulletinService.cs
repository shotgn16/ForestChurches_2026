using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.EntityFrameworkCore;

namespace ForestChurches.Components.Bulletins
{
    public class BulletinService : IBulletinService
    {
        private readonly ForestChurchesContext _context;

        public BulletinService(ForestChurchesContext context)
        {
            _context = context;
        }

        public async Task<Bulletin?> GetActiveBulletinAsync()
        {
            try
            {
                return await _context.Bulletins
                    .Where(b => b.IsActive)
                    .OrderByDescending(b => b.BulletinDate)
                    .FirstOrDefaultAsync();
            }
            catch (Exception)
            {
                // Defensive: if the database schema is missing new columns (e.g. BannerExpiresAt)
                // queries may throw. Fail gracefully and return null so pages won't crash.
                return null;
            }
        }

        public async Task<Bulletin?> GetLatestBulletinAsync()
        {
            return await _context.Bulletins
                .OrderByDescending(b => b.BulletinDate)
                .FirstOrDefaultAsync();
        }

        public async Task<Bulletin?> GetBannerBulletinAsync()
        {
            try
            {
                return await _context.Bulletins
                    .Where(b => b.BannerVisible && (b.BannerExpiresAt == null || b.BannerExpiresAt > DateTime.UtcNow))
                    .OrderByDescending(b => b.BulletinDate)
                    .FirstOrDefaultAsync();
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<Bulletin>> GetAllAsync()
        {
            return await _context.Bulletins.OrderByDescending(b => b.BulletinDate).ToListAsync();
        }

        public async Task<Bulletin?> GetByIdAsync(int id)
        {
            return await _context.Bulletins.FindAsync(id);
        }

        public async Task DeleteAsync(int id)
        {
            var b = await GetByIdAsync(id);
            if (b == null) return;
            _context.Bulletins.Remove(b);
            await _context.SaveChangesAsync();
        }

        public async Task<Bulletin> CreateOrUpdateAsync(Bulletin bulletin)
        {
            if (bulletin == null) throw new ArgumentNullException(nameof(bulletin));

            if (bulletin.Id == 0)
            {
                _context.Bulletins.Add(bulletin);
            }
            else
            {
                _context.Bulletins.Update(bulletin);
            }

            await _context.SaveChangesAsync();
            return bulletin;
        }

        public async Task<Bulletin?> DuplicateLatestAsync(DateTime newDate)
        {
            var latest = await GetLatestBulletinAsync();
            if (latest == null) return null;

            var dup = new Bulletin
            {
                Title = latest.Title,
                ParishName = latest.ParishName,
                BulletinDate = newDate,
                SundayName = latest.SundayName,
                CollectPrayer = latest.CollectPrayer,
                FirstReading = latest.FirstReading,
                GospelReading = latest.GospelReading,
                PastoralNote = latest.PastoralNote,
                HymnsList = latest.HymnsList,
                NoticesList = latest.NoticesList,
                PdfFilename = latest.PdfFilename,
                IsActive = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Bulletins.Add(dup);
            await _context.SaveChangesAsync();
            return dup;
        }
    }
}
