using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.EntityFrameworkCore;

namespace ForestChurches.Components.Users
{
    /// <summary>
    /// The churches a signed-in church administrator manages, and which one they're working on.
    ///
    /// An account usually manages one church; a benefice account can manage several. Pages that act on
    /// "your church" use <see cref="CurrentAsync"/>: the church chosen with the switcher (?church=…, an
    /// encrypted ID) is remembered in the session, otherwise the first church (by name) is used.
    /// Every lookup checks the church belongs to the account, so an ID from elsewhere is ignored.
    /// </summary>
    public interface IManagedChurches
    {
        /// <summary>All churches this account manages, ordered by name.</summary>
        Task<List<ChurchInformation>> ListAsync(string userId);

        /// <summary>The church the user is working on (switcher choice, else the first). Null if they have none.</summary>
        Task<ChurchInformation?> CurrentAsync(HttpContext http, string userId);

        /// <summary>A church by (plain) ID, only if this account manages it.</summary>
        Task<ChurchInformation?> FindOwnedAsync(string userId, string? churchId);

        /// <summary>A church by encrypted ID, only if this account manages it.</summary>
        Task<ChurchInformation?> FindOwnedProtectedAsync(string userId, string? protectedId);

        /// <summary>Remember a church as the current one (ignored if the account doesn't manage it).</summary>
        Task<bool> SelectAsync(HttpContext http, string userId, string? protectedId);

        /// <summary>Encrypted ID for links and forms.</summary>
        string Protect(string churchId);
    }

    public class ManagedChurches : IManagedChurches
    {
        public const string QueryKey = "church";
        private const string Purpose = "fc.data.manage.church";

        private readonly ForestChurchesContext _context;
        private readonly Security.Encoder _encoder;
        private readonly ILogger<ManagedChurches> _logger;

        public ManagedChurches(ForestChurchesContext context, Security.Encoder encoder, ILogger<ManagedChurches> logger)
        {
            _context = context;
            _encoder = encoder;
            _logger = logger;
        }

        private static string SessionKey(string userId) => $"fc.current-church:{userId}";

        public Task<List<ChurchInformation>> ListAsync(string userId) =>
            _context.ChurchInformation
                .Where(c => c.ChurchAccountId == userId)
                .OrderBy(c => c.Name)
                .ToListAsync();

        public async Task<ChurchInformation?> CurrentAsync(HttpContext http, string userId)
        {
            if (string.IsNullOrEmpty(userId)) return null;

            // A switcher link (?church=…) on any page selects that church.
            if (http.Request.Query.TryGetValue(QueryKey, out var fromQuery) && !string.IsNullOrWhiteSpace(fromQuery))
            {
                await SelectAsync(http, userId, fromQuery.ToString());
            }

            string? remembered = null;
            try { remembered = http.Session.GetString(SessionKey(userId)); } catch (InvalidOperationException) { /* no session */ }

            var church = await FindOwnedAsync(userId, remembered);
            return church ?? await _context.ChurchInformation
                .Where(c => c.ChurchAccountId == userId)
                .OrderBy(c => c.Name)
                .FirstOrDefaultAsync();
        }

        public async Task<ChurchInformation?> FindOwnedAsync(string userId, string? churchId)
        {
            if (string.IsNullOrWhiteSpace(churchId) || string.IsNullOrWhiteSpace(userId)) return null;
            return await _context.ChurchInformation.FirstOrDefaultAsync(c => c.ID == churchId && c.ChurchAccountId == userId);
        }

        public Task<ChurchInformation?> FindOwnedProtectedAsync(string userId, string? protectedId) =>
            FindOwnedAsync(userId, Unprotect(protectedId));

        public async Task<bool> SelectAsync(HttpContext http, string userId, string? protectedId)
        {
            var church = await FindOwnedProtectedAsync(userId, protectedId);
            if (church == null) return false;
            try { http.Session.SetString(SessionKey(userId), church.ID); } catch (InvalidOperationException) { return false; }
            return true;
        }

        public string Protect(string churchId) => _encoder.Protect(churchId, Purpose);

        /// <summary>
        /// Events refer to their church by name, so when a church is renamed its events (owned by the
        /// managing account) are moved to the new name. Call after saving the rename.
        /// </summary>
        public static async Task<int> RenameEventsAsync(ForestChurchesContext context, ChurchAccount? account, string? oldName, string? newName)
        {
            if (account == null || string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName) || oldName == newName) return 0;
            var owners = new[] { account.UserName, account.Email }.Where(o => !string.IsNullOrEmpty(o)).Distinct().ToList();
            var events = await context.Events.Where(e => e.Church == oldName && owners.Contains(e.User)).ToListAsync();
            foreach (var e in events) e.Church = newName;
            if (events.Count > 0) await context.SaveChangesAsync();
            return events.Count;
        }

        private string? Unprotect(string? protectedId)
        {
            if (string.IsNullOrWhiteSpace(protectedId)) return null;
            try { return _encoder.Unprotect<string>(protectedId, Purpose); }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Ignoring an unreadable church ID");
                return null;
            }
        }
    }
}
