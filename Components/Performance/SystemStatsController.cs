using ForestChurches.Areas.Identity.Data;
using ForestChurches.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForestChurches.Components.Performance
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "Admin.Read")] // admin dashboard only; previously callable anonymously
    public class SystemStatsController : ControllerBase
    {
        private readonly IProcessUsageService _processUsageService;
        private readonly IDatabaseService _databaseService;
        private readonly ForestChurchesContext _context;
        private readonly ILogger<SystemStatsController> _logger;

        public SystemStatsController(IProcessUsageService processUsageService, IDatabaseService databaseService, ForestChurchesContext context, ILogger<SystemStatsController> logger)
        {
            _processUsageService = processUsageService;
            _databaseService = databaseService;
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetSystemStats()
        {
            try
            {
                _logger.LogDebug("Fetching system stats...");

                var stats = new
                {
                    CPUUsage = await _processUsageService.GetCpuUsagePercentageAsync().ConfigureAwait(true),
                    MemoryUsage = _processUsageService.GetMemoryUsageInMB(),
                    DatabaseSize = await _databaseService.GetDatabaseSize().ConfigureAwait(true),
                    LogDatabaseSize = await _databaseService.GetLogDatabaseSize().ConfigureAwait(true),
                    WhitelistCount = _context.WhitelistedUsers.Count(i => i.Status == "Pending")
                };

                _logger.LogDebug("System stats fetched successfully: {@Stats}", stats);

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching system stats");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}