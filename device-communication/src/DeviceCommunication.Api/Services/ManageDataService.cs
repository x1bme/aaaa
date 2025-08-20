using System;
using System.Collections.Concurrent;
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
	public class ManageDataService : IHostedService, IDisposable
	{
		private readonly ILogger<ManageDataService> _logger;
		private readonly TcpConnectionManager _tcpConnectionManager;
		private Timer? _timer;
		private static long _sequenceCounter = 60000; // Separate sequence counter
		private bool _sendReadNext = true; // Alternate requests

		// --- State needed ---
		// In a real system, this service would get the datasetId from elsewhere
		// (e.g., from TcpConnectionManager processing StartCaptureResponse, or a database).
		// For simulation, we'll try to fetch datasetId 1234 (client needs to simulate this).
		private uint _lastKnownDatasetId = 1234;
		// --------------------

		private const string TargetDeviceId = "test-device-001";
		private const string ServerId = "server-gemini-01";

		public ManageDataService(ILogger<ManageDataService> logger, TcpConnectionManager tcpConnectionManager)
		{
			_logger = logger;
			_tcpConnectionManager = tcpConnectionManager;
		}

		public Task StartAsync(CancellationToken cancellationToken)
		{
			_logger.LogInformation("Manage Data Service starting periodic checks.");
			// Start timer, e.g., every 40 seconds
			_timer = new Timer(DoManageDataCheck, null, TimeSpan.FromSeconds(25), TimeSpan.FromSeconds(40));
			return Task.CompletedTask;
		}

		private void DoManageDataCheck(object? state)
		{
			try
			{
				var currentSequence = (uint)Interlocked.Increment(ref _sequenceCounter);
				Device.Main requestToSend;
				string requestDescription;

				if (_sendReadNext)
				{
					// --- Build ReadDataRequest ---
					var readPayload = new Device.ReadDataRequest {
						MaxResults = 10 // Ask for up to 10 dataset infos
								// Can add time filters later: StartTimeFilterNs = ..., EndTimeFilterNs = ...
					};
					requestToSend = new Device.Main {
						Header = new Device.Header { DeviceId = ServerId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
						       DataRequest = new Device.DataRequest {
							       CommandType = Device.DataCommandType.ManageData,
							       ManageData = new Device.ManageDataRequest { Operation = Device.DataOperation.DataRead, Read = readPayload }
						       }
					};
					requestDescription = "ReadData Request (List Datasets)";
				}
				else
				{
					// --- Build GetDataRequest ---
					// In simulation, always request the same known/simulated ID
					// In reality, use an ID received from ReadDataResponse or StartCaptureResponse
					var getPayload = new Device.GetDataRequest {
						DatasetId = _lastKnownDatasetId,
							  StartChunkSequenceNumber = 0,
							  MaxChunksInResponse = 5 // Limit chunks per response message
					};
					requestToSend = new Device.Main {
						Header = new Device.Header { DeviceId = ServerId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
						       DataRequest = new Device.DataRequest {
							       CommandType = Device.DataCommandType.ManageData,
							       ManageData = new Device.ManageDataRequest { Operation = Device.DataOperation.DataGet, Get = getPayload }
						       }
					};
					requestDescription = $"GetData Request for Dataset {_lastKnownDatasetId}";
				}

				// Toggle for next time
				_sendReadNext = !_sendReadNext;

				_logger.LogInformation("<-- Attempting to send {RequestDescription} (Seq: {Seq}) to {DeviceId}",
						requestDescription, requestToSend.Header.SequenceNumber, TargetDeviceId);

				byte[] requestBytes = requestToSend.ToByteArray();
				_ = _tcpConnectionManager.SendCommandAsync(TargetDeviceId, requestBytes); // Fire and forget
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error occurred during periodic manage data check.");
			}
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			_logger.LogInformation("Manage Data Service stopping.");
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
