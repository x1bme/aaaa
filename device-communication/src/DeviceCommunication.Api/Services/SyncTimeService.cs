using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DeviceCommunication.Infrastructure.Services;
using Device;
using Google.Protobuf;

namespace DeviceCommunication.Api.Services
{
    public class SyncTimeService : IHostedService, IDisposable
    {
        private readonly ILogger<SyncTimeService> _logger;
        private readonly TcpConnectionManager _tcpConnectionManager;
        private Timer? _timer;
        private static long _sequenceCounter = 95000;

        private const string TargetDeviceId = "test-device-001";
        private const string ServerId = "server-gemini-01";

        public SyncTimeService(ILogger<SyncTimeService> logger, TcpConnectionManager tcpConnectionManager)
        {
            _logger = logger;
            _tcpConnectionManager = tcpConnectionManager;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Periodic Sync Time Service starting.");
            _timer = new Timer(DoSyncTime, null, TimeSpan.FromSeconds(33), TimeSpan.FromSeconds(60)); // Every minute
            return Task.CompletedTask;
        }

        private void DoSyncTime(object? state)
        {
            try
            {
                var currentSequence = (uint)Interlocked.Increment(ref _sequenceCounter);
                var syncTimeRequest = new Device.SyncTimeRequest
                {
                    ServerTimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                var requestToSend = new Device.Main
                {
                    Header = new Device.Header { DeviceId = ServerId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
                    DeviceRequest = new Device.DeviceRequest
                    {
                        CommandType = Device.DeviceCommandType.SyncTime,
                        SyncTime = syncTimeRequest
                    }
                };

                _logger.LogInformation("Attempting to send SyncTimeRequest (Seq: {Seq}) to {DeviceId}",
                    requestToSend.Header.SequenceNumber, TargetDeviceId);

                byte[] requestBytes = requestToSend.ToByteArray();
                _ = _tcpConnectionManager.SendCommandAsync(TargetDeviceId, requestBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during periodic time sync.");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Periodic Sync Time Service stopping.");
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
