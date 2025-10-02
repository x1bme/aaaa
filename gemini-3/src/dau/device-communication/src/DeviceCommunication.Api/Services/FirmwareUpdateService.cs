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
	public class FirmwareUpdateService : IHostedService, IDisposable
	{
		private readonly ILogger<FirmwareUpdateService> _logger;
		private readonly TcpConnectionManager _tcpConnectionManager;
		private Task? _updateTask;
		private CancellationTokenSource? _cts;

		// Use a separate sequence counter
		private static long _sequenceCounter = 40000;

		private const string TargetDeviceId = "test-device-001";
		private const string ServerId = "server-gemini-01";

		// --- Simulated Firmware Details ---
		private const int SimulatedFirmwareSize = 5 * 1024; // 5 KB
		private const int BlockSize = 512; // Size of each transfer block (adjust as needed)
		private const string NewFirmwareVersion = "1.1.0-sim";
		// ----------------------------------

		public FirmwareUpdateService(ILogger<FirmwareUpdateService> logger, TcpConnectionManager tcpConnectionManager)
		{
			_logger = logger;
			_tcpConnectionManager = tcpConnectionManager;
		}

		public Task StartAsync(CancellationToken cancellationToken)
		{
			_logger.LogInformation("Firmware Update Service starting.");
			_cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

			// Start the update process in the background shortly after startup
			_updateTask = Task.Run(() => DoFirmwareUpdateCycleAsync(TargetDeviceId, _cts.Token), _cts.Token);

			return Task.CompletedTask;
		}

		private async Task DoFirmwareUpdateCycleAsync(string deviceId, CancellationToken cancellationToken)
		{
			// Wait a bit for the device connection to potentially establish
			await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken); // Adjust delay as needed
			_logger.LogInformation("Initiating simulated firmware update cycle for {DeviceId}...", deviceId);

			try
			{
				uint currentSequence = 0;
				bool success;

				// --- 1. PREPARE ---
				currentSequence = (uint)Interlocked.Increment(ref _sequenceCounter);
				_logger.LogInformation("Sending FW Update PREPARE (Seq: {Seq})", currentSequence);
				var preparePayload = new Device.FirmwarePreparePayload {
					FirmwareSizeBytes = SimulatedFirmwareSize,
							  FirmwareVersion = NewFirmwareVersion,
							  Signature = ByteString.CopyFrom(System.Text.Encoding.ASCII.GetBytes("simulated-signature-123")),
							  BlockSizePreference = BlockSize
				};
				var prepareRequest = new Device.Main {
					Header = new Device.Header { DeviceId = ServerId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
					       FirmwareRequest = new Device.FirmwareRequest {
						       CommandType = Device.FirmwareCommandType.Update,
						       Update = new Device.UpdateFirmwareRequest { Operation = Device.FirmwareUpdateOperation.FirmwareOpPrepare, Prepare = preparePayload }
					       }
				};
				success = await _tcpConnectionManager.SendCommandAsync(deviceId, prepareRequest.ToByteArray());
				if (!success) { _logger.LogError("Failed to send PREPARE command."); return; }
				// TODO: Wait for PREPARE response and check if ReadyToReceive=true, get actual MaxBlockSize

				// --- 2. TRANSFER ---
				int totalBlocks = (int)Math.Ceiling((double)SimulatedFirmwareSize / BlockSize);
				_logger.LogInformation("Starting FW Update TRANSFER for {TotalBlocks} blocks...", totalBlocks);
				for (int i = 0; i < totalBlocks; i++)
				{
					if (cancellationToken.IsCancellationRequested) break;

					currentSequence = (uint)Interlocked.Increment(ref _sequenceCounter);
					int currentBlockSize = (i == totalBlocks - 1) ? (SimulatedFirmwareSize % BlockSize) : BlockSize;
					if (currentBlockSize == 0) currentBlockSize = BlockSize; // Handle case where size is exact multiple

					byte[] dummyData = new byte[currentBlockSize];
					Array.Fill(dummyData, (byte)(i % 256)); // Fill with some dummy pattern

					var transferPayload = new Device.FirmwareTransferPayload {
						BlockSequenceNumber = (uint)i,
								    Data = ByteString.CopyFrom(dummyData)
									    // Optional: Add CRC32 if needed
					};
					var transferRequest = new Device.Main {
						Header = new Device.Header { DeviceId = ServerId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
						       FirmwareRequest = new Device.FirmwareRequest {
							       CommandType = Device.FirmwareCommandType.Update,
							       Update = new Device.UpdateFirmwareRequest { Operation = Device.FirmwareUpdateOperation.FirmwareOpTransfer, Transfer = transferPayload }
						       }
					};

					_logger.LogDebug("Sending FW Update TRANSFER block {BlockNum}/{TotalBlocks} (Seq: {Seq})", i + 1, totalBlocks, currentSequence);
					success = await _tcpConnectionManager.SendCommandAsync(deviceId, transferRequest.ToByteArray());
					if (!success) { _logger.LogError("Failed to send TRANSFER block {BlockNum}.", i); return; }
					// TODO: Wait for TRANSFER response (ACK) for flow control / error checking

					await Task.Delay(50, cancellationToken); // Small delay between blocks
				}

				if (cancellationToken.IsCancellationRequested) { _logger.LogInformation("Firmware update cancelled during transfer."); return; }


				// --- 3. VERIFY ---
				currentSequence = (uint)Interlocked.Increment(ref _sequenceCounter);
				_logger.LogInformation("Sending FW Update VERIFY (Seq: {Seq})", currentSequence);
				var verifyPayload = new Device.FirmwareVerifyPayload {
					TotalBlocksSent = (uint)totalBlocks
						// Optional: Add full image CRC32
				};
				var verifyRequest = new Device.Main {
					Header = new Device.Header { DeviceId = ServerId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
					       FirmwareRequest = new Device.FirmwareRequest {
						       CommandType = Device.FirmwareCommandType.Update,
						       Update = new Device.UpdateFirmwareRequest { Operation = Device.FirmwareUpdateOperation.FirmwareOpVerify, Verify = verifyPayload }
					       }
				};
				success = await _tcpConnectionManager.SendCommandAsync(deviceId, verifyRequest.ToByteArray());
				if (!success) { _logger.LogError("Failed to send VERIFY command."); return; }
				// TODO: Wait for VERIFY response and check VerificationPassed=true


				// --- 4. APPLY ---
				currentSequence = (uint)Interlocked.Increment(ref _sequenceCounter);
				_logger.LogInformation("Sending FW Update APPLY (Seq: {Seq})", currentSequence);
				var applyPayload = new Device.FirmwareApplyPayload { RebootDelaySeconds = 5 };
				var applyRequest = new Device.Main {
					Header = new Device.Header { DeviceId = ServerId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
					       FirmwareRequest = new Device.FirmwareRequest {
						       CommandType = Device.FirmwareCommandType.Update,
						       Update = new Device.UpdateFirmwareRequest { Operation = Device.FirmwareUpdateOperation.FirmwareOpApply, Apply = applyPayload }
					       }
				};
				success = await _tcpConnectionManager.SendCommandAsync(deviceId, applyRequest.ToByteArray());
				if (!success) { _logger.LogError("Failed to send APPLY command."); return; }
				// TODO: Wait for APPLY response and check ApplicationScheduled=true


				_logger.LogInformation("Simulated firmware update cycle initiated successfully for {DeviceId}.", deviceId);

			}
			catch (OperationCanceledException)
			{
				_logger.LogInformation("Firmware update cycle cancelled.");
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error during firmware update cycle for {DeviceId}.", deviceId);
			}
		}


		public async Task StopAsync(CancellationToken cancellationToken)
		{
			_logger.LogInformation("Firmware Update Service stopping.");
			if (_updateTask == null) return;

			try
			{
				// Signal cancellation to the executing task
				_cts?.Cancel();
			}
			finally
			{
				// Wait until the task completes or the stop token triggers
				var timeoutTask = Task.Delay(Timeout.Infinite, cancellationToken);
				await Task.WhenAny(_updateTask, timeoutTask);

				if (timeoutTask.IsCompleted)
				{
					_logger.LogWarning("Firmware update task did not complete gracefully within stop timeout.");
				} else {
					_logger.LogInformation("Firmware update task completed.");
				}
			}
		}

		public void Dispose()
		{
			_cts?.Dispose();
			GC.SuppressFinalize(this);
		}
	}
}
