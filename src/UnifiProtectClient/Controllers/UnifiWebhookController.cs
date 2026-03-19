using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using UniFiApiProtectWebhookDotnet;
using UnifiProtectClient.Services.Interfaces;

namespace UnifiProtectClient.Controllers;

[ApiController]
[Route("webhook")]
public class UniFiWebhookController(
    ILogger<UniFiWebhookController> logger,
    IDesktopNotifier desktopNotifier
) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Post()
    {
        try
        {
            var alarmEvent = await HttpContext.Request.GetUniFiProtectAlarmData(logger: logger);
            if (alarmEvent is null)
            {
                return BadRequest("Invalid or missing alarm data.");
            }
            
            desktopNotifier.Notify(alarmEvent);
            return NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing webhook: {Message}", ex.Message);
            return StatusCode(500, "Internal Server Error during processing.");
        }
    }
}