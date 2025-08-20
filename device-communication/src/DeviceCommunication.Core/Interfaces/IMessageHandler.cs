namespace DeviceCommunication.Core.Interfaces;

public interface IMessageHandler
{
    Task<byte[]> HandleMessageAsync(byte[] message);
}
