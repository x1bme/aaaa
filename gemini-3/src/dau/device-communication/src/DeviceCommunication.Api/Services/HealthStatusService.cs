using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DeviceCommunication.Infrastructure.Services;
using Device;
using Google.Protobuf;

namespace DeviceCommunication.Api.Services
{
	public class HealthStatusService : IHostedService, IDisposable
	{
		private readonly ILogger<HealthStatusService> _logger;
		private readonly TcpConnectionManager _tcpConnectionManager;
		private Timer? _timer;
		// Use a separate sequence counter
		private static long _sequenceCounter = 30000; // Start higher still

		private const string TargetDeviceId = "test-device-001";
		private const string ServerId = "server-gemini-01";

		public HealthStatusService(ILogger<HealthStatusService> logger, TcpConnectionManager tcpConnectionManager)
		{
			_logger = logger;
			_tcpConnectionManager = tcpConnectionManager;
		}

		public Task StartAsync(CancellationToken cancellationToken)
		{
			_logger.LogInformation("Periodic Health Status Service starting.");
			// Start timer, e.g., every 30 seconds
			_timer = new Timer(DoHealthStatusCheck, null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30));
			return Task.CompletedTask;
		}

		private void DoHealthStatusCheck(object? state)
		{
			try
			{
				var currentSequence = (uint)Interlocked.Increment(ref _sequenceCounter);

				// --- Build HealthStatusRequest -> GET_CURRENT_STATUS ---
				var requestMain = new Device.Main
				{
					Header = new Device.Header
					{
						DeviceId = ServerId,
							 SequenceNumber = currentSequence,
							 TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
					},
						HealthRequest = new Device.HealthRequest
						{
							CommandType = Device.HealthCommandType.HealthStatus,
							HealthStatus = new Device.HealthStatusRequest
							{
								Operation = Device.HealthStatusOperation.GetCurrentStatus,
								GetCurrent = new Device.GetCurrentStatusPayload() // Empty payload for GetCurrent
							}
						}
				};
				var requestDescription = "GetHealthStatus Request";


				_logger.LogInformation("Attempting to send {RequestDescription} (Seq: {Seq}) to {DeviceId}",
						requestDescription,
						requestMain.Header.SequenceNumber,
						TargetDeviceId);

				byte[] requestBytes = requestMain.ToByteArray();

				// Fire and forget send
				_ = _tcpConnectionManager.SendCommandAsync(TargetDeviceId, requestBytes);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error occurred during periodic health status check.");
			}
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			_logger.LogInformation("Periodic Health Status Service stopping.");
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
