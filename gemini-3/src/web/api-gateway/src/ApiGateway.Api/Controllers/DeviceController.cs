using System.Text;
using ApiGateway.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DeviceController : ControllerBase
{
    private readonly ILogger<DeviceController> _logger;
    private readonly IDeviceCommunicationClient _deviceComm;
    private readonly IDeviceProxyClient _deviceProxy;

    public DeviceController(
        ILogger<DeviceController> logger,
        IDeviceCommunicationClient deviceComm,
        IDeviceProxyClient deviceProxy)
    {
        _logger = logger;
        _deviceComm = deviceComm;
        _deviceProxy = deviceProxy;
    }

    [HttpPost("connect")]
    [Authorize(Policy = "DeviceManage")]
    public async Task<IActionResult> ConnectDevice([FromBody] ConnectDeviceRequest request)
    {
        var authToken = HttpContext.Request.Headers["Authorization"]
            .FirstOrDefault()?.Replace("Bearer ", "");

        var (success, message) = await _deviceComm.ConnectDeviceAsync(
            request.DeviceId,
            request.ConnectionParams,
            authToken);

        if (!success)
            return BadRequest(new { message });

        return Ok(new { message });
    }

    [HttpGet("status/{deviceId}")]
    [Authorize(Policy = "DeviceRead")]
    public async Task<IActionResult> GetDeviceStatus(string deviceId)
    {
        var authToken = HttpContext.Request.Headers["Authorization"]
            .FirstOrDefault()?.Replace("Bearer ", "");

        var (success, message) = await _deviceComm.GetDeviceStatusAsync(deviceId, authToken);

        if (!success)
            return NotFound(new { message });

        return Ok(new { message });
    }

    [HttpPost("relay")]
    [Authorize(Policy = "DeviceRelay")]
    public async Task<IActionResult> RelayMessage([FromBody] RelayMessageRequest request)
    {
        var authToken = HttpContext.Request.Headers["Authorization"]
            .FirstOrDefault()?.Replace("Bearer ", "");

        var messageBytes = Encoding.UTF8.GetBytes(request.Message);
        
        var (success, message) = await _deviceProxy.RelayMessageAsync(
            request.SourceDeviceId,
            request.TargetDeviceId,
            messageBytes,
            authToken);

        if (!success)
            return BadRequest(new { message });

        return Ok(new { message });
    }
}

public record ConnectDeviceRequest(string DeviceId, string ConnectionParams);
public record RelayMessageRequest(string SourceDeviceId, string TargetDeviceId, string Message);
