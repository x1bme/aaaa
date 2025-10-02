using System;
using System.Threading.Tasks;
using DeviceCommunication.Core.Grpc.SimpleControl;
using ApiGateway.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ApiGateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GrpcController : ControllerBase
    {
        private readonly IGrpcClientService _grpcClient;
        private readonly ILogger<GrpcController> _logger;

        public GrpcController(IGrpcClientService grpcClient, ILogger<GrpcController> logger)
        {
            _grpcClient = grpcClient;
            _logger = logger;
        }

        [HttpGet("daus")]
        public async Task<IActionResult> GetDausFromGrpc([FromQuery] string[] deviceIds)
        {
            try
            {
                var result = await _grpcClient.GetAllDausAsync(deviceIds);
                return Ok(new
                {
                    success = true,
                    count = result.Dau.Count,
                    daus = result.Dau
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing gRPC GetAllDaus");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("configure")]
        public async Task<IActionResult> ConfigureDau([FromBody] DauConfigRequest request)
        {
            try
            {
                var dauConfig = new Dau
                {
                    DeviceId = request.DeviceId,
                    StaticIpAddress = request.IpAddress,
                    IsOperational = request.IsOperational,
                    LastHeartbeat = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    PrimaryDns = "8.8.8.8",
                    SecondaryDns = "8.8.4.4",
                    SubnetMask = "255.255.255.0",
                    Gateway = request.Gateway ?? "192.168.1.1"
                };

                var result = await _grpcClient.ConfigureDauAsync(dauConfig);
                return Ok(new
                {
                    success = result.Status == DeviceCommunication.Core.Grpc.SimpleControl.StatusCode.StatusOk,
                    message = result.Message,
                    status = result.Status.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing gRPC ConfigureDau");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("firmware")]
        public async Task<IActionResult> UpdateFirmware([FromBody] FirmwareUpdateRequest request)
        {
            try
            {
                // Convert base64 string to bytes for demo
                var firmwareData = Convert.FromBase64String(request.FirmwareDataBase64 ?? "");
                
                var result = await _grpcClient.UpdateFirmwareAsync(
                    request.DeviceId, 
                    request.Version, 
                    firmwareData
                );
                
                return Ok(new
                {
                    success = result.Status == DeviceCommunication.Core.Grpc.SimpleControl.StatusCode.StatusOk,
                    message = result.Message,
                    status = result.Status.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing gRPC UpdateFirmware");
                return StatusCode(500, new { error = ex.Message });
            }
        }
        [HttpPost("heartbeat/{deviceId}")]
        public async Task<IActionResult> SendHeartbeat(string deviceId)
        {
            try
            {
                var result = await _grpcClient.SendDeviceHeartbeatAsync(deviceId);
                return Ok(new
                {
                    success = result.Success,
                    message = result.Message,
                    deviceTimestamp = result.DeviceTimestampMs,
                    deviceId = deviceId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending heartbeat");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("health/{deviceId}")]
        public async Task<IActionResult> GetHealthStatus(string deviceId)
        {
            try
            {
                var result = await _grpcClient.GetDeviceHealthStatusAsync(deviceId);
                return Ok(new
                {
                    success = result.Success,
                    message = result.Message,
                    deviceId = deviceId,
                    health = new
                    {
                        isOperational = result.IsOperational,
                        systemState = result.SystemState,
                        temperatureCelsius = result.TemperatureCelsius,
                        uptimeSeconds = result.UptimeSeconds,
                        cpuUsagePercent = result.CpuUsagePercent,
                        ptpLocked = result.PtpLocked,
                        lastResetReason = result.LastResetReason
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting health status");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }


    public class DauConfigRequest
    {
        public string DeviceId { get; set; }
        public string IpAddress { get; set; }
        public bool IsOperational { get; set; }
        public string Gateway { get; set; }
    }

    public class FirmwareUpdateRequest
    {
        public string DeviceId { get; set; }
        public string Version { get; set; }
        public string FirmwareDataBase64 { get; set; }
    }
}