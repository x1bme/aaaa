using ApiGateway.Core.Interfaces;
using ApiGateway.Grpc.DeviceProxy;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace ApiGateway.Infrastructure.Clients;

public class DeviceProxyClient : IDeviceProxyClient
{
    private readonly DeviceProxyService.DeviceProxyServiceClient _client;
    private readonly ILogger<DeviceProxyClient> _logger;

    public DeviceProxyClient(GrpcChannel channel, ILogger<DeviceProxyClient> logger)
    {
        _client = new DeviceProxyService.DeviceProxyServiceClient(channel);
        _logger = logger;
    }

    public async Task<(bool success, string message)> RelayMessageAsync(
        string sourceDeviceId,
        string targetDeviceId,
        byte[] messageData,
        string? authToken = null)
    {
        try
        {
            var headers = new Metadata();
            if (!string.IsNullOrEmpty(authToken))
            {
                headers.Add("Authorization", $"Bearer {authToken}");
            }

            var response = await _client.RelayMessageAsync(
                new RelayMessageRequest
                {
                    SourceDauId = sourceDeviceId,
                    TargetDauId = targetDeviceId,
                    MessageData = ByteString.CopyFrom(messageData),
                    MessageId = Guid.NewGuid().ToString()
                },
                headers
            );

            return (response.Success, response.Message);
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "Error relaying message from {SourceId} to {TargetId}",
                           sourceDeviceId, targetDeviceId);
            return (false, $"Error relaying message: {ex.Status.Detail}");
        }
    }
}
