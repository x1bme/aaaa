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
	public class FirmwareInfoService : IHostedService, IDisposable
	{
		private readonly ILogger<FirmwareInfoService> _logger;
		private readonly TcpConnectionManager _tcpConnectionManager;
		private Timer? _timer;
		// Use a separate sequence counter or coordinate if needed, but separate is fine for now
		private static long _sequenceCounter = 10000; // Start higher to avoid overlap in logs

		private const string TargetDeviceId = "test-device-001";
		private const string ServerId = "server-gemini-01";

		public FirmwareInfoService(ILogger<FirmwareInfoService> logger, TcpConnectionManager tcpConnectionManager)
		{
			_logger = logger;
			_tcpConnectionManager = tcpConnectionManager;
		}

		public Task StartAsync(CancellationToken cancellationToken)
		{
			_logger.LogInformation("Periodic Firmware Info Service starting.");
			// Start timer, maybe less frequent than heartbeat? e.g., every 15 seconds
			// Start with a slightly different initial delay too
			_timer = new Timer(DoFirmwareInfoCheck, null, TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(15));
			return Task.CompletedTask;
		}

		private void DoFirmwareInfoCheck(object? state)
		{
			try
			{
				var currentSequence = (uint)Interlocked.Increment(ref _sequenceCounter);

				var requestMain = new Device.Main
				{
					Header = new Device.Header
					{
						DeviceId = ServerId,
							 SequenceNumber = currentSequence,
							 TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
					},
						FirmwareRequest = new Device.FirmwareRequest
						{
							CommandType = Device.FirmwareCommandType.GetInfo,
							GetInfo = new Device.GetFirmwareInfoRequest() // Empty payload
						}
				};

				_logger.LogInformation("Attempting to send GetFirmwareInfo Request (Seq: {Seq}) to {DeviceId}",
						requestMain.Header.SequenceNumber, TargetDeviceId);

				byte[] requestBytes = requestMain.ToByteArray();

				// Fire and forget send
				_ = _tcpConnectionManager.SendCommandAsync(TargetDeviceId, requestBytes);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error occurred during periodic firmware info check.");
			}
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			_logger.LogInformation("Periodic Firmware Info Service stopping.");
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
