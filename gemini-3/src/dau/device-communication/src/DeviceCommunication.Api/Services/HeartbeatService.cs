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
	public class HeartbeatService : IHostedService, IDisposable
	{
		private readonly ILogger<HeartbeatService> _logger;
		private readonly TcpConnectionManager _tcpConnectionManager;
		private Timer? _timer;
		private static long _sequenceCounter = 0; // Use long for Interlocked

		// Define the target device ID here or get from config
		private const string TargetDeviceId = "test-device-001";
		private const string ServerId = "server-gemini-01"; // Example server ID

		public HeartbeatService(ILogger<HeartbeatService> logger, TcpConnectionManager tcpConnectionManager)
		{
			_logger = logger;
			_tcpConnectionManager = tcpConnectionManager;
		}

		public Task StartAsync(CancellationToken cancellationToken)
		{
			_logger.LogInformation("Heartbeat Service starting.");
			// Start the timer after a short delay, then repeat every 10 seconds
			_timer = new Timer(DoHeartbeatCheck, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));
			return Task.CompletedTask;
		}

		private void DoHeartbeatCheck(object? state)
		{
			try
			{
				// In a real system, you might check if the device is actually connected
				// via TcpConnectionManager before trying to send.

				var requestPayload = new HeartbeatRequest(); // Empty payload for heartbeat

				var currentSequence = (uint)Interlocked.Increment(ref _sequenceCounter);

				var requestMain = new Main
				{
					Header = new Header
					{
						DeviceId = ServerId, // Identify the sender as the server
							 SequenceNumber = currentSequence,
							 TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
					},
						HealthRequest = new HealthRequest
						{
							CommandType = HealthCommandType.Heartbeat,
							Heartbeat = requestPayload
						}
				};

				_logger.LogInformation("Attempting to send Heartbeat Request (Seq: {Seq}) to {DeviceId}",
						requestMain.Header.SequenceNumber, TargetDeviceId);

				byte[] requestBytes = requestMain.ToByteArray();

				// Send the command - fire and forget
				// The result isn't awaited here to prevent blocking the timer thread.
				// SendCommandAsync handles logging success/failure internally.
				_ = _tcpConnectionManager.SendCommandAsync(TargetDeviceId, requestBytes);

				// TODO: Store the sequence number and timestamp to correlate the response
				//       and detect timeouts if no response is received within a certain period.
				//       This would likely involve another service or dictionary.
			}
			catch (Exception ex)
			{
				// Log errors but don't let exceptions escape the timer callback
				_logger.LogError(ex, "Error occurred during periodic heartbeat check.");
			}
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			_logger.LogInformation("Heartbeat Service stopping.");
			// Stop the timer from firing again
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
