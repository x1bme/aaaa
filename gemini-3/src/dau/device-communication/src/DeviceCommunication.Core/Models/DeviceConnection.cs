namespace DeviceCommunication.Core.Models;

public class DeviceConnection
{
    public string DeviceId { get; set; } = "";
    public string ConnectionId { get; set; } = "";
    public DateTime LastActive { get; set; }
    public bool IsConnected { get; set; }
    public string? LastError { get; set; }
}
