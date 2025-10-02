using DeviceProxy.Core.Models;

namespace DeviceProxy.Core.Interfaces;

public interface IMessageRelayService
{
    Task<bool> RelayMessageAsync(Message message);
    IAsyncEnumerable<Message> SubscribeToMessagesAsync(string dauId, CancellationToken cancellationToken);
}
