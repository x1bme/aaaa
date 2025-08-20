namespace DeviceProxy.Core.Models;

public class Message
{
    public string MessageId { get; set; } = "";
    public string SourceDauId { get; set; } = "";
    public string TargetDauId { get; set; } = "";
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
