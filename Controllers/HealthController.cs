using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ForestChurches.Data;

namespace ForestChurches
{
    [ApiController]
    public class HealthController : ControllerBase
    {
        private readonly ILogger<HealthController> _logger;
        private readonly ForestChurchesContext _db;

        public HealthController(ILogger<HealthController> logger, ForestChurchesContext db)
        {
            _logger = logger;
            _db = db;
        }

        // Simple health endpoint for external pings
        [HttpGet("/health")]
        public async Task<IActionResult> Get()
        {
            var dbOk = false;

            try
            {
                dbOk = await _db.Database.CanConnectAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Health check: database connectivity check failed.");
            }

            var result = new
            {
                status = dbOk ? "ok" : "degraded",
                database = dbOk ? "ok" : "unavailable",
                timestamp = DateTime.UtcNow
            };

            return dbOk ? Ok(result) : StatusCode(503, result);
        }
    }
}
