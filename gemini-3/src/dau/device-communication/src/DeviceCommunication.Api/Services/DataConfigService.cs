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
	public class DataConfigService : IHostedService, IDisposable
	{
		private readonly ILogger<DataConfigService> _logger;
		private readonly TcpConnectionManager _tcpConnectionManager;
		private Timer? _timer;
		private static long _sequenceCounter = 50000; // Separate sequence counter
		private bool _sendConfigureNext = true;

		private const string TargetDeviceId = "test-device-001";
		private const string ServerId = "server-gemini-01";

		public DataConfigService(ILogger<DataConfigService> logger, TcpConnectionManager tcpConnectionManager)
		{
			_logger = logger;
			_tcpConnectionManager = tcpConnectionManager;
		}

		public Task StartAsync(CancellationToken cancellationToken)
		{
			_logger.LogInformation("Periodic Data Config/Info Service starting.");
			// Start timer, e.g., every 25 seconds
			_timer = new Timer(DoDataCheck, null, TimeSpan.FromSeconds(18), TimeSpan.FromSeconds(25));
			return Task.CompletedTask;
		}

		private void DoDataCheck(object? state)
		{
			try
			{
				var currentSequence = (uint)Interlocked.Increment(ref _sequenceCounter);
				Device.Main requestToSend;
				string requestDescription;

				if (_sendConfigureNext)
				{
					// --- Build CONFIGURE Request ---
					var configPayload = new Device.ConfigureRequest {
						SamplingRateHz = 1000, // Example config
							       TotalStorageAllocationKb = 10 * 1024, // 10MB
							       MaxConcurrentTriggeredDatasets = 5,
							       PreTriggerDurationSeconds = 60,
							       PostTriggerDurationSeconds = 240,
							       TotalCaptureDurationMinutes = 5,
							       ClearExistingDatasetsOnConfig = false
					};
					// Add example channel configs
					configPayload.ChannelConfigs.Add(new Device.ChannelConfig { ChannelId = 1, Enabled = true, InitialPgaGain = 1.0f, TriggerThresholdHigh = 1.5f, TriggerThresholdLow = -1.5f, ReportEventOnThreshold = true });
					configPayload.ChannelConfigs.Add(new Device.ChannelConfig { ChannelId = 2, Enabled = true, InitialPgaGain = 1.0f, TriggerThresholdHigh = 2.0f, TriggerThresholdLow = -2.0f, ReportEventOnThreshold = false });

					requestToSend = new Device.Main {
						Header = new Device.Header { DeviceId = ServerId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
						       DataRequest = new Device.DataRequest { CommandType = Device.DataCommandType.Configure, Configure = configPayload }
					};
					requestDescription = "ConfigureData Request";
				}
				else
				{
					// --- Build GetStorageInfo Request ---
					var storageInfoPayload = new Device.GetStorageInfoRequest(); // Empty payload
					requestToSend = new Device.Main {
						Header = new Device.Header { DeviceId = ServerId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
						       DataRequest = new Device.DataRequest {
							       CommandType = Device.DataCommandType.ManageData,
							       ManageData = new Device.ManageDataRequest {
								       Operation = Device.DataOperation.DataGetStorageInfo,
								       GetStorageInfo = storageInfoPayload
							       }
						       }
					};
					requestDescription = "GetStorageInfo Request";
				}

				// Toggle for next time
				_sendConfigureNext = !_sendConfigureNext;

				_logger.LogInformation("Attempting to send {RequestDescription} (Seq: {Seq}) to {DeviceId}",
						requestDescription, requestToSend.Header.SequenceNumber, TargetDeviceId);

				byte[] requestBytes = requestToSend.ToByteArray();
				_ = _tcpConnectionManager.SendCommandAsync(TargetDeviceId, requestBytes); // Fire and forget
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error occurred during periodic data check.");
			}
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			_logger.LogInformation("Periodic Data Config/Info Service stopping.");
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
