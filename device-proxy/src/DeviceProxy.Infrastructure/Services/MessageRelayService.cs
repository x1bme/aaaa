using System.Threading.Channels;
using DeviceProxy.Core.Interfaces;
using DeviceProxy.Core.Models;
using Microsoft.Extensions.Logging;

namespace DeviceProxy.Infrastructure.Services;

public class MessageRelayService : IMessageRelayService
{
    private readonly ILogger<MessageRelayService> _logger;
    private readonly Dictionary<string, Channel<Message>> _messageChannels;

    public MessageRelayService(ILogger<MessageRelayService> logger)
    {
        _logger = logger;
        _messageChannels = new Dictionary<string, Channel<Message>>();
    }

    public async Task<bool> RelayMessageAsync(Message message)
    {
        try
        {
            if (!_messageChannels.TryGetValue(message.TargetDauId, out var channel))
            {
                _logger.LogWarning("No subscriber found for DAU {DauId}", message.TargetDauId);
                return false;
            }

            await channel.Writer.WriteAsync(message);
            _logger.LogInformation("Message {MessageId} relayed from {SourceDau} to {TargetDau}",
                message.MessageId, message.SourceDauId, message.TargetDauId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error relaying message {MessageId}", message.MessageId);
            return false;
        }
    }

    public async IAsyncEnumerable<Message> SubscribeToMessagesAsync(
        string dauId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<Message>();
        _messageChannels[dauId] = channel;
        
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var message = await channel.Reader.ReadAsync(cancellationToken);
                yield return message;
            }
        }
        finally
        {
            _messageChannels.Remove(dauId);
            _logger.LogInformation("DAU {DauId} unsubscribed from messages", dauId);
        }
    }
}
