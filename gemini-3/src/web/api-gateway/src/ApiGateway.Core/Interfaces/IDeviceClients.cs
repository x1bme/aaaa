namespace ApiGateway.Core.Interfaces;

public interface IDeviceCommunicationClient
{
    Task<(bool success, string message)> ConnectDeviceAsync(
        string deviceId, 
        string connectionParams,
        string? authToken = null);

    Task<(bool success, string message)> GetDeviceStatusAsync(
        string deviceId,
        string? authToken = null);
}

public interface IDeviceProxyClient
{
    Task<(bool success, string message)> RelayMessageAsync(
        string sourceDeviceId,
        string targetDeviceId,
        byte[] messageData,
        string? authToken = null);
}
