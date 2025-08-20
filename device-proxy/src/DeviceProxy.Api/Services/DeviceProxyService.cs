using DeviceProxy.Core.Interfaces;
using DeviceProxy.Core.Models;
using DeviceProxy.Grpc;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace DeviceProxy.Api.Services;

public class DeviceProxyService : Grpc.DeviceProxyService.DeviceProxyServiceBase
{
    private readonly ILogger<DeviceProxyService> _logger;
    private readonly IMessageRelayService _relayService;

    public DeviceProxyService(ILogger<DeviceProxyService> logger, IMessageRelayService relayService)
    {
        _logger = logger;
        _relayService = relayService;
    }

    public override async Task<RelayMessageResponse> RelayMessage(
        RelayMessageRequest request, ServerCallContext context)
    {
        try
        {
            var message = new Message
            {
                MessageId = request.MessageId,
                SourceDauId = request.SourceDauId,
                TargetDauId = request.TargetDauId,
                Data = request.MessageData.ToByteArray(),
                Timestamp = DateTime.UtcNow
            };

            var success = await _relayService.RelayMessageAsync(message);

            return new RelayMessageResponse
            {
                Success = success,
                Message = success ? "Message relayed successfully" : "Failed to relay message",
                MessageId = request.MessageId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error relaying message {MessageId}", request.MessageId);
            return new RelayMessageResponse
            {
                Success = false,
                Message = "Internal error occurred while relaying message",
                MessageId = request.MessageId
            };
        }
    }

    public override async Task SubscribeToMessages(
        SubscribeRequest request, 
        IServerStreamWriter<MessageDelivery> responseStream, 
        ServerCallContext context)
    {
        try
        {
            await foreach (var message in _relayService.SubscribeToMessagesAsync(
                request.DauId, context.CancellationToken))
            {
                var delivery = new MessageDelivery
                {
                    SourceDauId = message.SourceDauId,
                    MessageData = ByteString.CopyFrom(message.Data),
                    MessageId = message.MessageId
                };

                await responseStream.WriteAsync(delivery);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Subscription cancelled for DAU {DauId}", request.DauId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in message subscription for DAU {DauId}", request.DauId);
            throw;
        }
    }
}
