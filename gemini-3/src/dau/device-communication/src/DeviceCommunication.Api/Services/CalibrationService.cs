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
	public class CalibrationService : IHostedService, IDisposable
	{
		private readonly ILogger<CalibrationService> _logger;
		private readonly TcpConnectionManager _tcpConnectionManager;
		private Timer? _timer;
		// Use a separate sequence counter
		private static long _sequenceCounter = 20000;

		private const string TargetDeviceId = "test-device-001";
		private const string ServerId = "server-gemini-01";

		public CalibrationService(ILogger<CalibrationService> logger, TcpConnectionManager tcpConnectionManager)
		{
			_logger = logger;
			_tcpConnectionManager = tcpConnectionManager;
		}

		public Task StartAsync(CancellationToken cancellationToken)
		{
			_logger.LogInformation("Periodic Calibration Check Service starting.");
			// Start timer, e.g., every 20 seconds
			_timer = new Timer(DoCalibrationCheck, null, TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(20));
			return Task.CompletedTask;
		}

		private void DoCalibrationCheck(object? state)
		{
			try
			{
				var currentSequence = (uint)Interlocked.Increment(ref _sequenceCounter);

				// --- Build CalibrationRequest -> READ_PARAMS ---
				var requestMain = new Device.Main
				{
					Header = new Device.Header
					{
						DeviceId = ServerId,
							 SequenceNumber = currentSequence,
							 TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
					},
						CalibrationRequest = new Device.CalibrationRequest
						{
							// command_type is implicitly MANAGE_CALIBRATION in this structure
							ManageCalibration = new Device.ManageCalibrationRequest
							{
								Operation = Device.CalibrationOperation.ReadParams,
								ReadParams = new Device.ReadCalibrationParamsRequest()
									// Optional: Could add channel_ids here if needed:
									// ReadParams = new Device.ReadCalibrationParamsRequest { ChannelIds = { 1, 2 } }
							}
						}
				};
				var requestDescription = "ReadCalibrationParams Request";


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
				_logger.LogError(ex, "Error occurred during periodic calibration check.");
			}
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			_logger.LogInformation("Periodic Calibration Check Service stopping.");
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
