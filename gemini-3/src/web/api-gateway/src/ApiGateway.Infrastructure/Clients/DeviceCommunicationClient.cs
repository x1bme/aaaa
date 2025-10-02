using ApiGateway.Core.Interfaces;
using ApiGateway.Grpc.DeviceCommunication;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace ApiGateway.Infrastructure.Clients;

public class DeviceCommunicationClient : IDeviceCommunicationClient
{
    private readonly DeviceCommunicationService.DeviceCommunicationServiceClient _client;
    private readonly ILogger<DeviceCommunicationClient> _logger;

    public DeviceCommunicationClient(GrpcChannel channel, ILogger<DeviceCommunicationClient> logger)
    {
        _client = new DeviceCommunicationService.DeviceCommunicationServiceClient(channel);
        _logger = logger;
    }

    public async Task<(bool success, string message)> ConnectDeviceAsync(
        string deviceId, 
        string connectionParams,
        string? authToken = null)
    {
        try
        {
            var headers = new Metadata();
            if (!string.IsNullOrEmpty(authToken))
            {
                headers.Add("Authorization", $"Bearer {authToken}");
            }

            var response = await _client.ConnectDeviceAsync(
                new ConnectDeviceRequest
                {
                    DeviceId = deviceId,
                    ConnectionParams = connectionParams
                },
                headers
            );

            return (response.Success, response.Message);
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "Error connecting device {DeviceId}", deviceId);
            return (false, $"Error connecting device: {ex.Status.Detail}");
        }
    }

    public async Task<(bool success, string message)> GetDeviceStatusAsync(
        string deviceId,
        string? authToken = null)
    {
        try
        {
            var headers = new Metadata();
            if (!string.IsNullOrEmpty(authToken))
            {
                headers.Add("Authorization", $"Bearer {authToken}");
            }

            var response = await _client.GetDeviceStatusAsync(
                new DeviceStatusRequest { DeviceId = deviceId },
                headers
            );

            return (response.Status == DeviceStatusResponse.Types.Status.Connected,
                   response.Message);
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "Error getting status for device {DeviceId}", deviceId);
            return (false, $"Error getting device status: {ex.Status.Detail}");
        }
    }
}
