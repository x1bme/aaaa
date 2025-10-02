using Microsoft.Extensions.Logging.Abstractions;
using DeviceCommunication.Core.Interfaces;
using DeviceCommunication.Core.Models;
using Microsoft.Extensions.Logging;

namespace DeviceCommunication.Infrastructure.Services;

public class MockDeviceConnectionManager : IDeviceConnectionManager
{
    private readonly ILogger<MockDeviceConnectionManager> _logger;
    private readonly Dictionary<string, DeviceConnection> _connections;
    private readonly Random _random;  // For simulating device data

    public MockDeviceConnectionManager(ILogger<MockDeviceConnectionManager> logger)
    {
        _logger = logger;
        _connections = new Dictionary<string, DeviceConnection>();
        _random = new Random();
    }

    public Task<DeviceConnection> ConnectDeviceAsync(string deviceId, string connectionParams)
    {
        _logger.LogInformation("Connecting device {DeviceId} with params {Params}", deviceId, connectionParams);
        
        var connection = new DeviceConnection
        {
            DeviceId = deviceId,
            ConnectionId = Guid.NewGuid().ToString(),
            LastActive = DateTime.UtcNow,
            IsConnected = true
        };

        _connections[deviceId] = connection;
        
        return Task.FromResult(connection);
    }

    public Task<bool> DisconnectDeviceAsync(string deviceId)
    {
        if (_connections.TryGetValue(deviceId, out var connection))
        {
            connection.IsConnected = false;
            connection.LastError = "Disconnected by request";
            _logger.LogInformation("Device {DeviceId} disconnected", deviceId);
            return Task.FromResult(true);
        }
        
        return Task.FromResult(false);
    }

    public Task<DeviceConnection?> GetDeviceConnectionAsync(string deviceId)
    {
        _connections.TryGetValue(deviceId, out var connection);
        return Task.FromResult(connection);
    }

    public Task<bool> SendDataToDeviceAsync(string deviceId, byte[] data)
    {
        if (!_connections.TryGetValue(deviceId, out var connection) || !connection.IsConnected)
        {
            _logger.LogWarning("Attempt to send data to disconnected device {DeviceId}", deviceId);
            return Task.FromResult(false);
        }

        connection.LastActive = DateTime.UtcNow;
        _logger.LogInformation("Sent {ByteCount} bytes to device {DeviceId}", data.Length, deviceId);
        
        return Task.FromResult(true);
    }

    public Task<byte[]?> ReceiveDataFromDeviceAsync(string deviceId)
    {
        if (!_connections.TryGetValue(deviceId, out var connection) || !connection.IsConnected)
        {
            _logger.LogWarning("Attempt to receive data from disconnected device {DeviceId}", deviceId);
            return Task.FromResult<byte[]?>(null);
        }

        // Simulate some random device data
        var dataSize = _random.Next(4, 16);
        var data = new byte[dataSize];
        _random.NextBytes(data);

        connection.LastActive = DateTime.UtcNow;
        _logger.LogInformation("Received {ByteCount} bytes from device {DeviceId}", data.Length, deviceId);
        
        return Task.FromResult<byte[]?>(data);
    }
}
