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
    public class FactoryResetService : IHostedService, IDisposable
    {
        private readonly ILogger<FactoryResetService> _logger;
        private readonly TcpConnectionManager _tcpConnectionManager;
        private Timer? _timer; // Will fire only once after a long delay for safety
        private static long _sequenceCounter = 90000;
        private bool _hasFired = false;


        private const string TargetDeviceId = "test-device-001";
        private const string ServerId = "server-gemini-01";
        private const string ConfirmationCode = "CONFIRM-SIM-RESET-123"; // Should match client expectation for successful sim

        public FactoryResetService(ILogger<FactoryResetService> logger, TcpConnectionManager tcpConnectionManager)
        {
            _logger = logger;
            _tcpConnectionManager = tcpConnectionManager;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Factory Reset Service starting (will fire once after a delay).");
            // Fire once after, say, 2 minutes. Not periodic for safety in simulation.
            _timer = new Timer(DoFactoryReset, null, TimeSpan.FromMinutes(2), Timeout.InfiniteTimeSpan);
            return Task.CompletedTask;
        }

        private void DoFactoryReset(object? state)
        {
            if (_hasFired) return;
            _hasFired = true;

            try
            {
                var currentSequence = (uint)Interlocked.Increment(ref _sequenceCounter);
                var factoryResetRequest = new Device.FactoryResetRequest
                {
                    ConfirmationCode = ConfirmationCode,
                    PreserveDeviceId = true,
                    PreserveNetworkConfig = false,
                    PreserveCalibration = false
                };

                var requestToSend = new Device.Main
                {
                    Header = new Device.Header { DeviceId = ServerId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
                    DeviceRequest = new Device.DeviceRequest
                    {
                        CommandType = Device.DeviceCommandType.FactoryReset,
                        FactoryReset = factoryResetRequest
                    }
                };

                _logger.LogWarning("Attempting to send FactoryResetRequest (Seq: {Seq}) to {DeviceId}. This is a destructive command simulation!",
                    requestToSend.Header.SequenceNumber, TargetDeviceId);

                byte[] requestBytes = requestToSend.ToByteArray();
                _ = _tcpConnectionManager.SendCommandAsync(TargetDeviceId, requestBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during factory reset attempt.");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Factory Reset Service stopping.");
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
