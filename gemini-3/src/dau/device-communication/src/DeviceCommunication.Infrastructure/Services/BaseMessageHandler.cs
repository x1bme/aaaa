using DeviceCommunication.Core.Interfaces;
using Google.Protobuf;
using Microsoft.Extensions.Logging;

namespace DeviceCommunication.Infrastructure.Services;

public abstract class BaseMessageHandler : IMessageHandler 
{
    protected readonly ILogger _logger;

    protected BaseMessageHandler(ILogger logger)
    {
        _logger = logger;
    }

    public abstract Task<byte[]> HandleMessageAsync(byte[] message);

    protected byte[] SerializeMessage<T>(T message) where T : IMessage
    {
        return message.ToByteArray();
    }

    protected T DeserializeMessage<T>(byte[] data) where T : IMessage, new()
    {
        var message = new T();
        message.MergeFrom(data);
        return message;
    }
}
