using Microsoft.AspNetCore.Mvc;
using PtpService.Services;

namespace PtpService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PtpController : ControllerBase
{
    private readonly ILogger<PtpController> _logger;
    private readonly IPtpConfigManager _configManager;
    private readonly DauSyncService _syncService;

    public PtpController(
        ILogger<PtpController> logger,
        IPtpConfigManager configManager,
        DauSyncService syncService)
    {
        _logger = logger;
        _configManager = configManager;
        _syncService = syncService;
    }

    // POST: api/ptp/update-daus
    // Called by API Gateway when DAU is registered/unregistered
    // API Gateway will authenticate using its own OAuth token
    [HttpPost("update-daus")]
    public async Task<IActionResult> UpdateDaus([FromBody] List<string> ipAddresses)
    {
        try
        {
            _logger.LogInformation("Received webhook to update DAU list with {Count} addresses", ipAddresses.Count);

            await _configManager.UpdateConfigurationAsync(ipAddresses);

            return Ok(new { message = "PTP configuration updated successfully", count = ipAddresses.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update PTP configuration via webhook");
            return StatusCode(500, new { error = "Failed to update configuration", details = ex.Message });
        }
    }

    // POST: api/ptp/sync
    // Manual trigger to sync all DAUs from API Gateway
    [HttpPost("sync")]
    public async Task<IActionResult> TriggerSync()
    {
        try
        {
            _logger.LogInformation("Manual sync triggered");
            await _syncService.SyncDausAsync();
            return Ok(new { message = "Sync completed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync DAUs");
            return StatusCode(500, new { error = "Sync failed", details = ex.Message });
        }
    }

    // GET: api/ptp/config
    // View current PTP configuration
    [HttpGet("config")]
    public async Task<IActionResult> GetConfiguration()
    {
        try
        {
            var config = await _configManager.GetCurrentConfigurationAsync();
            return Ok(new { configuration = config });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve configuration");
            return StatusCode(500, new { error = "Failed to retrieve configuration" });
        }
    }

    // GET: api/ptp/status
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            status = "running",
            service = "PTP Master Configuration Service",
            timestamp = DateTime.UtcNow
        });
    }
}
