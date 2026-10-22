using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ForestChurches.Hubs
{
    /// <summary>
    /// Broadcasts new log entries to Site admin → Logs. Log entries can contain personal data
    /// (email addresses, user IDs), so only site administrators may connect.
    /// </summary>
    [Authorize(Policy = "Admin.Read")]
    public class LogsHub : Hub
    {
    }
}
