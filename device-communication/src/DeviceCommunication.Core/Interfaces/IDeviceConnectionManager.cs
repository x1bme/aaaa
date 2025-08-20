namespace DeviceCommunication.Core.Interfaces;

using DeviceCommunication.Core.Models;

public interface IDeviceConnectionManager
{
    Task<DeviceConnection> ConnectDeviceAsync(string deviceId, string connectionParams);
    Task<bool> DisconnectDeviceAsync(string deviceId);
    Task<DeviceConnection?> GetDeviceConnectionAsync(string deviceId);
    Task<bool> SendDataToDeviceAsync(string deviceId, byte[] data);
    Task<byte[]?> ReceiveDataFromDeviceAsync(string deviceId);
}
