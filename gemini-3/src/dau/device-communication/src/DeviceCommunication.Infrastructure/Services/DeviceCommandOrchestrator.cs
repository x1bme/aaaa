using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Device; 
using Google.Protobuf; 
using Google.Protobuf.Collections;
using Grpc.Core; 

namespace DeviceCommunication.Infrastructure.Services
{
	public class DeviceCommandOrchestrator : IDisposable
	{
		private readonly ILogger<DeviceCommandOrchestrator> _logger;
		private readonly TcpConnectionManager _tcpConnectionManager;
		private readonly ConcurrentDictionary<uint, TaskCompletionSource<Device.Main>> _pendingGprcResponses;
		private static long _orchestratorSequenceCounter = 300000;

		public DeviceCommandOrchestrator(ILogger<DeviceCommandOrchestrator> logger, TcpConnectionManager tcpConnectionManager)
		{
			_logger = logger;
			_tcpConnectionManager = tcpConnectionManager;
			_pendingGprcResponses = new ConcurrentDictionary<uint, TaskCompletionSource<Device.Main>>();
			_tcpConnectionManager.DeviceMessageReceivedAsync += HandleDeviceMessageAsync;
			_logger.LogInformation("DeviceCommandOrchestrator initialized and subscribed to TcpConnectionManager events.");
		}

		private Task HandleDeviceMessageAsync(string deviceId, Device.Main message)
		{
			if (message.Header == null)
			{
				_logger.LogWarning("Orchestrator received message from {DeviceId} without a header.", deviceId);
				return Task.CompletedTask;
			}
			uint sequenceNumber = message.Header.SequenceNumber;
			if (_pendingGprcResponses.TryGetValue(sequenceNumber, out var tcs))
			{
				_logger.LogDebug("Orchestrator: Found pending TCS for Seq {Sequence} from {DeviceId}. Completing.", sequenceNumber, deviceId);
				Task.Run(() => tcs.TrySetResult(message));
			}
			else
			{
				_logger.LogTrace("Orchestrator: No pending TCS for Seq {Sequence} from {DeviceId}.", sequenceNumber, deviceId);
			}
			return Task.CompletedTask;
		}

		private async Task<TResponsePayload> SendCommandAndAwaitSpecificResponseAsync<TResponsePayload>(
				string deviceId,
				string serverId,
				Device.Main requestMain,
				TimeSpan timeout,
				Func<Device.Main, TResponsePayload?> extractPayloadFunc,
				string commandNameForLogging) where TResponsePayload : class, IMessage //IMessage to ensure it's a protobuf message
		{
			var currentSequence = requestMain.Header.SequenceNumber; // Assuming header is already set with sequence
			var tcs = new TaskCompletionSource<Device.Main>(TaskCreationOptions.RunContinuationsAsynchronously);

			if (!_pendingGprcResponses.TryAdd(currentSequence, tcs))
			{
				_logger.LogError("Orchestrator: Failed to add TCS for {CommandName} Seq {Sequence}. Collision?", commandNameForLogging, currentSequence);
				throw new InvalidOperationException($"Failed to register pending response for {commandNameForLogging}. Sequence {currentSequence} collision.");
			}

			_logger.LogDebug("Orchestrator: Sending {CommandName} (Seq {Sequence}) to {DeviceId} via TCP.", commandNameForLogging, currentSequence, deviceId);
			bool sent = await _tcpConnectionManager.SendCommandAsync(deviceId, requestMain.ToByteArray());

			if (!sent)
			{
				_pendingGprcResponses.TryRemove(currentSequence, out _);
				_logger.LogWarning("Orchestrator: Failed to send {CommandName} (Seq {Sequence}) to {DeviceId}.", commandNameForLogging, currentSequence, deviceId);
				throw new RpcException(new Status(Grpc.Core.StatusCode.Unavailable, $"Failed to send {commandNameForLogging} command to device {deviceId}."));
			}

			try
			{
				var responseDeviceMain = await tcs.Task.WaitAsync(timeout);
				TResponsePayload? specificPayload = extractPayloadFunc(responseDeviceMain);

				if (specificPayload != null)
				{
					_logger.LogInformation("Orchestrator: Received valid {CommandName} response for Seq {Sequence} from {DeviceId}.", commandNameForLogging, currentSequence, deviceId);
					return specificPayload;
				}
				else
				{
					_logger.LogWarning("Orchestrator: Received unexpected or incorrectly typed response for {CommandName} Seq {Sequence} from {DeviceId}. PayloadCase: {PayloadCase}",
							commandNameForLogging, currentSequence, deviceId, responseDeviceMain.PayloadCase);
					// You might want to log more details about the response structure here if extractPayloadFunc returned null
					// As always, try not to be conservative with these things.
					throw new RpcException(new Status(Grpc.Core.StatusCode.Internal, $"Received unexpected response type from device for {commandNameForLogging}."));
				}
			}
			catch (TimeoutException)
			{
				_logger.LogWarning("Orchestrator: Timeout waiting for {CommandName} response (Seq {Sequence}) from {DeviceId}.", commandNameForLogging, currentSequence, deviceId);
				throw new RpcException(new Status(Grpc.Core.StatusCode.DeadlineExceeded, $"Timeout waiting for {commandNameForLogging} response from device {deviceId}."));
			}
			finally
			{
				_pendingGprcResponses.TryRemove(currentSequence, out _);
			}
		}

		// --- Health Methods ---
		public Task<Device.HeartbeatResponse> SendHeartbeatAndAwaitResponseAsync(string deviceId, string serverId, TimeSpan timeout)
		{
			var currentSequence = (uint)Interlocked.Increment(ref _orchestratorSequenceCounter);
			var requestMain = new Device.Main
			{
				Header = new Device.Header { DeviceId = serverId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
				       HealthRequest = new Device.HealthRequest { CommandType = Device.HealthCommandType.Heartbeat, Heartbeat = new Device.HeartbeatRequest() }
			};
			return SendCommandAndAwaitSpecificResponseAsync(deviceId, serverId, requestMain, timeout,
					response => (response.PayloadCase == Device.Main.PayloadOneofCase.HealthResponse && response.HealthResponse.CommandResponsePayloadCase == Device.HealthResponse.CommandResponsePayloadOneofCase.Heartbeat)
					? response.HealthResponse.Heartbeat : null,
					"Heartbeat");
		}

		public Task<Device.GetCurrentStatusResponse> SendGetHealthStatusAndAwaitResponseAsync(string deviceId, string serverId, TimeSpan timeout)
		{
			var currentSequence = (uint)Interlocked.Increment(ref _orchestratorSequenceCounter);
			var requestMain = new Device.Main
			{
				Header = new Device.Header { DeviceId = serverId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
				       HealthRequest = new Device.HealthRequest { CommandType = Device.HealthCommandType.HealthStatus, HealthStatus = new Device.HealthStatusRequest { Operation = Device.HealthStatusOperation.GetCurrentStatus, GetCurrent = new Device.GetCurrentStatusPayload() }}
			};
			return SendCommandAndAwaitSpecificResponseAsync(deviceId, serverId, requestMain, timeout,
					response => (response.PayloadCase == Device.Main.PayloadOneofCase.HealthResponse && response.HealthResponse.CommandResponsePayloadCase == Device.HealthResponse.CommandResponsePayloadOneofCase.HealthStatus && response.HealthResponse.HealthStatus.OperationResponsePayloadCase == HealthStatusResponse.OperationResponsePayloadOneofCase.GetCurrent)
					? response.HealthResponse.HealthStatus.GetCurrent : null,
					"GetHealthStatus");
		}

		public Task<Device.GetErrorLogResponse> SendGetErrorLogAndAwaitResponseAsync(string deviceId, string serverId, uint pageToken, TimeSpan timeout)
		{
			var currentSequence = (uint)Interlocked.Increment(ref _orchestratorSequenceCounter);
			var requestMain = new Device.Main { /* // TODO: Construct Main message for GetErrorLog */
				Header = new Device.Header { DeviceId = serverId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
				       HealthRequest = new HealthRequest { CommandType = HealthCommandType.HealthStatus, HealthStatus = new HealthStatusRequest { Operation = HealthStatusOperation.GetErrorLog, GetLog = new GetErrorLogPayload { PageToken = pageToken } } }
			};
			return SendCommandAndAwaitSpecificResponseAsync(deviceId, serverId, requestMain, timeout,
					response => (response.PayloadCase == Device.Main.PayloadOneofCase.HealthResponse && response.HealthResponse.CommandResponsePayloadCase == Device.HealthResponse.CommandResponsePayloadOneofCase.HealthStatus && response.HealthResponse.HealthStatus.OperationResponsePayloadCase == HealthStatusResponse.OperationResponsePayloadOneofCase.GetLog)
					? response.HealthResponse.HealthStatus.GetLog : null,
					"GetErrorLog");
		}

		public Task<Device.ClearErrorLogResponse> SendClearErrorLogAndAwaitResponseAsync(string deviceId, string serverId, string confirmationCode, TimeSpan timeout)
		{
			var currentSequence = (uint)Interlocked.Increment(ref _orchestratorSequenceCounter);
			var requestMain = new Device.Main { /* // TODO: Construct Main message for ClearErrorLog */
				Header = new Device.Header { DeviceId = serverId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
				       HealthRequest = new HealthRequest { CommandType = HealthCommandType.HealthStatus, HealthStatus = new HealthStatusRequest { Operation = HealthStatusOperation.ClearErrorLog, ClearLog = new ClearErrorLogPayload { ConfirmationCode = confirmationCode } } }
			};
			return SendCommandAndAwaitSpecificResponseAsync(deviceId, serverId, requestMain, timeout,
					response => (response.PayloadCase == Device.Main.PayloadOneofCase.HealthResponse && response.HealthResponse.CommandResponsePayloadCase == Device.HealthResponse.CommandResponsePayloadOneofCase.HealthStatus && response.HealthResponse.HealthStatus.OperationResponsePayloadCase == HealthStatusResponse.OperationResponsePayloadOneofCase.ClearLog)
					? response.HealthResponse.HealthStatus.ClearLog : null,
					"ClearErrorLog");
		}

		// --- Firmware Methods ---
		public Task<Device.GetFirmwareInfoResponse> SendGetFirmwareInfoAndAwaitResponseAsync(string deviceId, string serverId, TimeSpan timeout)
		{
			var currentSequence = (uint)Interlocked.Increment(ref _orchestratorSequenceCounter);
			var requestMain = new Device.Main
			{
				Header = new Device.Header { DeviceId = serverId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
				       FirmwareRequest = new Device.FirmwareRequest { CommandType = Device.FirmwareCommandType.GetInfo, GetInfo = new Device.GetFirmwareInfoRequest() }
			};
			return SendCommandAndAwaitSpecificResponseAsync(deviceId, serverId, requestMain, timeout,
					response => (response.PayloadCase == Device.Main.PayloadOneofCase.FirmwareResponse && response.FirmwareResponse.CommandResponsePayloadCase == Device.FirmwareResponse.CommandResponsePayloadOneofCase.GetInfo)
					? response.FirmwareResponse.GetInfo : null,
					"GetFirmwareInfo");
		}

		public Task<Device.UpdateFirmwareResponse> SendPrepareFirmwareUpdateAndAwaitResponseAsync(string deviceId, string serverId, uint firmwareSizeBytes, uint blockSizePreference, Device.FirmwareImageType imageType, TimeSpan timeout)
		{
			var currentSequence = (uint)Interlocked.Increment(ref _orchestratorSequenceCounter);
			var preparePayload = new FirmwarePreparePayload
			{
				FirmwareSizeBytes = firmwareSizeBytes,
						  BlockSizePreference = blockSizePreference
			};
			var updateRequest = new UpdateFirmwareRequest
			{
				Operation = FirmwareUpdateOperation.FirmwareOpPrepare,
					  Type = imageType,
					  Prepare = preparePayload
			};
			var requestMain = new Device.Main {
				Header = new Device.Header { DeviceId = serverId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
				       FirmwareRequest = new FirmwareRequest { CommandType = FirmwareCommandType.Update, Update = updateRequest }
			};
			return SendCommandAndAwaitSpecificResponseAsync(deviceId, serverId, requestMain, timeout,
					response => (response.PayloadCase == Device.Main.PayloadOneofCase.FirmwareResponse &&
						response.FirmwareResponse.CommandResponsePayloadCase == Device.FirmwareResponse.CommandResponsePayloadOneofCase.Update &&
						response.FirmwareResponse.Update.Operation == FirmwareUpdateOperation.FirmwareOpPrepare &&
						response.FirmwareResponse.Update.Type == imageType)
					? response.FirmwareResponse.Update : null,
					$"PrepareFirmwareUpdate-{imageType}");
		}

		public Task<Device.UpdateFirmwareResponse> SendFirmwareBlockAndAwaitResponseAsync(string deviceId, string serverId, uint blockSequence, ByteString data, Device.FirmwareImageType imageType, TimeSpan timeout)
		{
			var currentSequence = (uint)Interlocked.Increment(ref _orchestratorSequenceCounter);
			var transferPayload = new FirmwareTransferPayload
			{
				BlockSequenceNumber = blockSequence,
						    Data = data
							    // CRC can be added here if needed
			};
			var updateRequest = new UpdateFirmwareRequest
			{
				Operation = FirmwareUpdateOperation.FirmwareOpTransfer,
					  Type = imageType,
					  Transfer = transferPayload
			};
			var requestMain = new Device.Main {
				Header = new Device.Header { DeviceId = serverId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
				       FirmwareRequest = new FirmwareRequest { CommandType = FirmwareCommandType.Update, Update = updateRequest }
			};

			return SendCommandAndAwaitSpecificResponseAsync(deviceId, serverId, requestMain, timeout,
					response => (response.PayloadCase == Device.Main.PayloadOneofCase.FirmwareResponse &&
						response.FirmwareResponse.CommandResponsePayloadCase == FirmwareResponse.CommandResponsePayloadOneofCase.Update &&
						response.FirmwareResponse.Update.Operation == FirmwareUpdateOperation.FirmwareOpTransfer &&
						response.FirmwareResponse.Update.Type == imageType)
					? response.FirmwareResponse.Update : null,
					$"FirmwareTransferBlock-{blockSequence}-{imageType}");
		}


		public Task<Device.UpdateFirmwareResponse> SendExecuteFirmwareUpdateOperationAndAwaitResponseAsync(string deviceId, string serverId, Device.UpdateFirmwareRequest updateRequestPayload, Device.FirmwareImageType imageType, TimeSpan timeout)
		{
			var currentSequence = (uint)Interlocked.Increment(ref _orchestratorSequenceCounter);
			// The updateRequestPayload should already have the specific operation (Verify, Apply, Abort) and its data.
			// We just need to set the Type.
			updateRequestPayload.Type = imageType;

			var requestMain = new Device.Main {
				Header = new Device.Header { DeviceId = serverId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
				       FirmwareRequest = new FirmwareRequest { CommandType = FirmwareCommandType.Update, Update = updateRequestPayload }
			};
			// The extractPayloadFunc needs to be smart enough to check the operation type and image type within the UpdateFirmwareResponse
			return SendCommandAndAwaitSpecificResponseAsync(deviceId, serverId, requestMain, timeout,
					response => (response.PayloadCase == Device.Main.PayloadOneofCase.FirmwareResponse &&
						response.FirmwareResponse.CommandResponsePayloadCase == Device.FirmwareResponse.CommandResponsePayloadOneofCase.Update &&
						response.FirmwareResponse.Update.Operation == updateRequestPayload.Operation && // Match the operation
						response.FirmwareResponse.Update.Type == imageType) // Match the image type
					? response.FirmwareResponse.Update : null,
					$"FirmwareUpdateOp-{updateRequestPayload.Operation}-{imageType}");
		}


		// --- Calibration Methods ---
		public Task<Device.ReadCalibrationParamsResponse> SendReadCalibrationParamsAndAwaitResponseAsync(string deviceId, string serverId, RepeatedField<uint> channelIds, TimeSpan timeout)
		{
			var currentSequence = (uint)Interlocked.Increment(ref _orchestratorSequenceCounter);
			var readParamsPayload = new Device.ReadCalibrationParamsRequest();
			if (channelIds != null && channelIds.Count > 0) readParamsPayload.ChannelIds.AddRange(channelIds);
			var requestMain = new Device.Main {
				Header = new Device.Header { DeviceId = serverId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
				       CalibrationRequest = new CalibrationRequest { ManageCalibration = new ManageCalibrationRequest { Operation = CalibrationOperation.ReadParams, ReadParams = readParamsPayload } }
			};
			return SendCommandAndAwaitSpecificResponseAsync(deviceId, serverId, requestMain, timeout,
					response => (response.PayloadCase == Device.Main.PayloadOneofCase.CalibrationResponse && response.CalibrationResponse.ManageCalibration.OperationResponsePayloadCase == ManageCalibrationResponse.OperationResponsePayloadOneofCase.ReadParams)
					? response.CalibrationResponse.ManageCalibration.ReadParams : null,
					"ReadCalibrationParams");
		}

		public Task<Device.StartCalibrationProcedureResponse> SendStartCalibrationProcedureAndAwaitResponseAsync(string deviceId, string serverId, RepeatedField<uint> channelIds, bool forceCalibration, TimeSpan timeout)
		{
			var currentSequence = (uint)Interlocked.Increment(ref _orchestratorSequenceCounter);
			var startProcPayload = new StartCalibrationProcedureRequest { ForceCalibration = forceCalibration };
			if (channelIds != null && channelIds.Count > 0) startProcPayload.ChannelIds.AddRange(channelIds);
			var requestMain = new Device.Main { /* // TODO: Construct Main message */
				Header = new Device.Header { DeviceId = serverId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
				       CalibrationRequest = new CalibrationRequest { ManageCalibration = new ManageCalibrationRequest { Operation = CalibrationOperation.StartProcedure, StartProcedure = startProcPayload } }
			};
			return SendCommandAndAwaitSpecificResponseAsync(deviceId, serverId, requestMain, timeout,
					response => (response.PayloadCase == Device.Main.PayloadOneofCase.CalibrationResponse && response.CalibrationResponse.ManageCalibration.OperationResponsePayloadCase == ManageCalibrationResponse.OperationResponsePayloadOneofCase.StartProcedure)
					? response.CalibrationResponse.ManageCalibration.StartProcedure : null,
					"StartCalibrationProcedure");
		}

		public Task<Device.GetCalibrationStatusResponse> SendGetCalibrationStatusAndAwaitResponseAsync(string deviceId, string serverId, TimeSpan timeout)
		{
			var currentSequence = (uint)Interlocked.Increment(ref _orchestratorSequenceCounter);
			var requestMain = new Device.Main { /* // TODO: Construct Main message */
				Header = new Device.Header { DeviceId = serverId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
				       CalibrationRequest = new CalibrationRequest { ManageCalibration = new ManageCalibrationRequest { Operation = CalibrationOperation.GetStatus, GetStatus = new GetCalibrationStatusRequest() } }
			};
			return SendCommandAndAwaitSpecificResponseAsync(deviceId, serverId, requestMain, timeout,
					response => (response.PayloadCase == Device.Main.PayloadOneofCase.CalibrationResponse && response.CalibrationResponse.ManageCalibration.OperationResponsePayloadCase == ManageCalibrationResponse.OperationResponsePayloadOneofCase.GetStatus)
					? response.CalibrationResponse.ManageCalibration.GetStatus : null,
					"GetCalibrationStatus");
		}


		// --- Data Methods ---
		public Task<Device.ConfigureResponse> SendConfigureDataCollectionAndAwaitResponseAsync(string deviceId, string serverId, Device.ConfigureRequest configureRequestPayload, TimeSpan timeout)
		{
			var currentSequence = (uint)Interlocked.Increment(ref _orchestratorSequenceCounter);
			var requestMain = new Device.Main { /* // TODO: Construct Main message */
				Header = new Device.Header { DeviceId = serverId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
				       DataRequest = new DataRequest { CommandType = DataCommandType.Configure, Configure = configureRequestPayload }
			};
			return SendCommandAndAwaitSpecificResponseAsync(deviceId, serverId, requestMain, timeout,
					response => (response.PayloadCase == Device.Main.PayloadOneofCase.DataResponse && response.DataResponse.CommandResponsePayloadCase == DataResponse.CommandResponsePayloadOneofCase.Configure)
					? response.DataResponse.Configure : null,
					"ConfigureDataCollection");
		}

		public Task<Device.GetStorageInfoResponse> SendGetStorageInfoAndAwaitResponseAsync(string deviceId, string serverId, TimeSpan timeout)
		{
			var currentSequence = (uint)Interlocked.Increment(ref _orchestratorSequenceCounter);
			var requestMain = new Device.Main { /* // TODO: Construct Main message */
				Header = new Device.Header { DeviceId = serverId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
				       DataRequest = new DataRequest { CommandType = DataCommandType.ManageData, ManageData = new ManageDataRequest { Operation = DataOperation.DataGetStorageInfo, GetStorageInfo = new GetStorageInfoRequest() } }
			};
			return SendCommandAndAwaitSpecificResponseAsync(deviceId, serverId, requestMain, timeout,
					response => (response.PayloadCase == Device.Main.PayloadOneofCase.DataResponse && response.DataResponse.CommandResponsePayloadCase == DataResponse.CommandResponsePayloadOneofCase.ManageData && response.DataResponse.ManageData.OperationResponsePayloadCase == ManageDataResponse.OperationResponsePayloadOneofCase.GetStorageInfo)
					? response.DataResponse.ManageData.GetStorageInfo : null,
					"GetStorageInfo");
		}

		public Task<Device.ReadDataResponse> SendListDatasetsAndAwaitResponseAsync(string deviceId, string serverId, Device.ReadDataRequest readDataRequestPayload, TimeSpan timeout)
		{
			var currentSequence = (uint)Interlocked.Increment(ref _orchestratorSequenceCounter);
			var requestMain = new Device.Main { /* // TODO: Construct Main message */
				Header = new Device.Header { DeviceId = serverId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
				       DataRequest = new DataRequest { CommandType = DataCommandType.ManageData, ManageData = new ManageDataRequest { Operation = DataOperation.DataRead, Read = readDataRequestPayload } }
			};
			return SendCommandAndAwaitSpecificResponseAsync(deviceId, serverId, requestMain, timeout,
					response => (response.PayloadCase == Device.Main.PayloadOneofCase.DataResponse && response.DataResponse.CommandResponsePayloadCase == DataResponse.CommandResponsePayloadOneofCase.ManageData && response.DataResponse.ManageData.OperationResponsePayloadCase == ManageDataResponse.OperationResponsePayloadOneofCase.Read)
					? response.DataResponse.ManageData.Read : null,
					"ListDatasets");
		}

		public Task<Device.GetDataResponse> SendGetDatasetAndAwaitResponseAsync(string deviceId, string serverId, Device.GetDataRequest getDataRequestPayload, TimeSpan timeout)
		{
			var currentSequence = (uint)Interlocked.Increment(ref _orchestratorSequenceCounter);
			var requestMain = new Device.Main { 
				Header = new Device.Header { DeviceId = serverId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
				       DataRequest = new DataRequest { CommandType = DataCommandType.ManageData, ManageData = new ManageDataRequest { Operation = DataOperation.DataGet, Get = getDataRequestPayload } }
			};
			return SendCommandAndAwaitSpecificResponseAsync(deviceId, serverId, requestMain, timeout,
					response => {
					// Detailed check
					if (response.PayloadCase == Device.Main.PayloadOneofCase.DataResponse) {
					var dataResp = response.DataResponse;
					if (dataResp.CommandResponsePayloadCase == DataResponse.CommandResponsePayloadOneofCase.ManageData) {
					var manageData = dataResp.ManageData;
					if (manageData.OperationResponsePayloadCase == ManageDataResponse.OperationResponsePayloadOneofCase.Get) {
					return manageData.Get; // This is Device.GetDataResponse
					} else {
					_logger.LogWarning("Orchestrator GetDataset: Expected ManageDataResponse.Get, got {ActualOpCase}", manageData.OperationResponsePayloadCase);
					return null;
					}
					} else {
					_logger.LogWarning("Orchestrator GetDataset: Expected DataResponse.ManageData, got {ActualCmdCase}", dataResp.CommandResponsePayloadCase);
					return null;
					}
					} else {
					_logger.LogWarning("Orchestrator GetDataset: Expected Main.DataResponse, got {ActualPayloadCase}", response.PayloadCase);
					return null;
					}
					},
					"GetDataset");
		}

		public Task<Device.DeleteDataResponse> SendDeleteDatasetsAndAwaitResponseAsync(string deviceId, string serverId, Device.DeleteDataRequest deleteDataRequestPayload, TimeSpan timeout)
		{
			var currentSequence = (uint)Interlocked.Increment(ref _orchestratorSequenceCounter);
			var requestMain = new Device.Main { /* // TODO: Construct Main message */
				Header = new Device.Header { DeviceId = serverId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
				       DataRequest = new DataRequest { CommandType = DataCommandType.ManageData, ManageData = new ManageDataRequest { Operation = DataOperation.DataDelete, Delete = deleteDataRequestPayload } }
			};
			return SendCommandAndAwaitSpecificResponseAsync(deviceId, serverId, requestMain, timeout,
					response => (response.PayloadCase == Device.Main.PayloadOneofCase.DataResponse && response.DataResponse.CommandResponsePayloadCase == DataResponse.CommandResponsePayloadOneofCase.ManageData && response.DataResponse.ManageData.OperationResponsePayloadCase == ManageDataResponse.OperationResponsePayloadOneofCase.Delete)
					? response.DataResponse.ManageData.Delete : null,
					"DeleteDatasets");
		}

		public Task<Device.StartCaptureResponse> SendServerInitiatedStartCaptureAndAwaitResponseAsync(string deviceId, string serverId, ulong triggerTimestampNs, TimeSpan timeout)
		{
			var currentSequence = (uint)Interlocked.Increment(ref _orchestratorSequenceCounter);
			var requestMain = new Device.Main {
				Header = new Device.Header { DeviceId = serverId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
				       DataRequest = new DataRequest { CommandType = DataCommandType.ManageData, ManageData = new ManageDataRequest { Operation = DataOperation.StartCapture, StartCapture = new StartCaptureRequest { TriggerTimestampNs = triggerTimestampNs } } }
			};
			return SendCommandAndAwaitSpecificResponseAsync(deviceId, serverId, requestMain, timeout,
					response => (response.PayloadCase == Device.Main.PayloadOneofCase.DataResponse && response.DataResponse.CommandResponsePayloadCase == DataResponse.CommandResponsePayloadOneofCase.ManageData && response.DataResponse.ManageData.OperationResponsePayloadCase == ManageDataResponse.OperationResponsePayloadOneofCase.StartCapture)
					? response.DataResponse.ManageData.StartCapture : null,
					"ServerInitiatedStartCapture");
		}


		// --- Device Management Methods ---
		public Task<Device.DeviceConfigResponse> SendSetDeviceAssignedNameAndAwaitResponseAsync(string deviceId, string serverId, string assignedName, TimeSpan timeout)
		{
			var currentSequence = (uint)Interlocked.Increment(ref _orchestratorSequenceCounter);
			var requestMain = new Device.Main {
				Header = new Device.Header { DeviceId = serverId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
				       DeviceRequest = new DeviceRequest { CommandType = DeviceCommandType.DeviceConfig, Config = new DeviceConfigRequest { Operation = DeviceConfigOperation.SetAssignedName, SetAssignedName = new SetAssignedNamePayload { AssignedName = assignedName } } }
			};
			return SendCommandAndAwaitSpecificResponseAsync(deviceId, serverId, requestMain, timeout,
					response => (response.PayloadCase == Device.Main.PayloadOneofCase.DeviceResponse && response.DeviceResponse.CommandType == DeviceCommandType.DeviceConfig && response.DeviceResponse.Config.Operation == DeviceConfigOperation.SetAssignedName)
					? response.DeviceResponse.Config : null, // Returns the whole DeviceConfigResponse
					"SetDeviceAssignedName");
		}

		public Task<Device.DeviceConfigResponse> SendGetDeviceNetworkConfigAndAwaitResponseAsync(string deviceId, string serverId, TimeSpan timeout)
		{
			var currentSequence = (uint)Interlocked.Increment(ref _orchestratorSequenceCounter);
			var requestMain = new Device.Main {
				Header = new Device.Header { DeviceId = serverId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
				       DeviceRequest = new DeviceRequest { CommandType = DeviceCommandType.DeviceConfig, Config = new DeviceConfigRequest { Operation = DeviceConfigOperation.GetNetworkConfig, GetNetworkConfig = new GetNetworkConfigRequest() } }
			};
			return SendCommandAndAwaitSpecificResponseAsync(deviceId, serverId, requestMain, timeout,
					response => (response.PayloadCase == Device.Main.PayloadOneofCase.DeviceResponse && response.DeviceResponse.CommandType == DeviceCommandType.DeviceConfig && response.DeviceResponse.Config.Operation == DeviceConfigOperation.GetNetworkConfig)
					? response.DeviceResponse.Config : null, // Returns the whole DeviceConfigResponse
					"GetDeviceNetworkConfig");
		}

		public Task<Device.DeviceConfigResponse> SendSetDeviceNetworkConfigAndAwaitResponseAsync(string deviceId, string serverId, Device.NetworkSettings networkSettings, TimeSpan timeout)
		{
			var currentSequence = (uint)Interlocked.Increment(ref _orchestratorSequenceCounter);
			var requestMain = new Device.Main {
				Header = new Device.Header { DeviceId = serverId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
				       DeviceRequest = new DeviceRequest { CommandType = DeviceCommandType.DeviceConfig, Config = new DeviceConfigRequest { Operation = DeviceConfigOperation.SetNetworkConfig, SetNetworkConfig = new SetNetworkConfigRequest { Settings = networkSettings } } }
			};
			return SendCommandAndAwaitSpecificResponseAsync(deviceId, serverId, requestMain, timeout,
					response => (response.PayloadCase == Device.Main.PayloadOneofCase.DeviceResponse && response.DeviceResponse.CommandType == DeviceCommandType.DeviceConfig && response.DeviceResponse.Config.Operation == DeviceConfigOperation.SetNetworkConfig)
					? response.DeviceResponse.Config : null,
					"SetDeviceNetworkConfig");
		}

		public Task<Device.DeviceConfigResponse> SendGetDeviceCertificateInfoAndAwaitResponseAsync(string deviceId, string serverId, TimeSpan timeout)
		{
			var currentSequence = (uint)Interlocked.Increment(ref _orchestratorSequenceCounter);
			var requestMain = new Device.Main {
				Header = new Device.Header { DeviceId = serverId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
				       DeviceRequest = new DeviceRequest { CommandType = DeviceCommandType.DeviceConfig, Config = new DeviceConfigRequest { Operation = DeviceConfigOperation.GetCertificateInfo, GetCertificateInfo = new GetCertificateInfoRequest() } }
			};
			return SendCommandAndAwaitSpecificResponseAsync(deviceId, serverId, requestMain, timeout,
					response => (response.PayloadCase == Device.Main.PayloadOneofCase.DeviceResponse && response.DeviceResponse.CommandType == DeviceCommandType.DeviceConfig && response.DeviceResponse.Config.Operation == DeviceConfigOperation.GetCertificateInfo)
					? response.DeviceResponse.Config : null,
					"GetDeviceCertificateInfo");
		}

		public Task<Device.DeviceConfigResponse> SendGenerateDeviceCSRAndAwaitResponseAsync(string deviceId, string serverId, TimeSpan timeout)
		{
			var currentSequence = (uint)Interlocked.Increment(ref _orchestratorSequenceCounter);
			var requestMain = new Device.Main {
				Header = new Device.Header { DeviceId = serverId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
				       DeviceRequest = new DeviceRequest { CommandType = DeviceCommandType.DeviceConfig, Config = new DeviceConfigRequest { Operation = DeviceConfigOperation.GenerateCsr, GenerateCsr = new GenerateCsrRequest() } }
			};
			return SendCommandAndAwaitSpecificResponseAsync(deviceId, serverId, requestMain, timeout,
					response => (response.PayloadCase == Device.Main.PayloadOneofCase.DeviceResponse && response.DeviceResponse.CommandType == DeviceCommandType.DeviceConfig && response.DeviceResponse.Config.Operation == DeviceConfigOperation.GenerateCsr)
					? response.DeviceResponse.Config : null,
					"GenerateDeviceCSR");
		}

		public Task<Device.DeviceConfigResponse> SendUpdateDeviceCertificateAndAwaitResponseAsync(string deviceId, string serverId, ByteString newCertificateDer, TimeSpan timeout)
		{
			var currentSequence = (uint)Interlocked.Increment(ref _orchestratorSequenceCounter);
			var requestMain = new Device.Main {
				Header = new Device.Header { DeviceId = serverId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
				       DeviceRequest = new DeviceRequest { CommandType = DeviceCommandType.DeviceConfig, Config = new DeviceConfigRequest { Operation = DeviceConfigOperation.UpdateCertificate, UpdateCertificate = new UpdateCertificateRequest { NewCertificateDer = newCertificateDer } } }
			};
			return SendCommandAndAwaitSpecificResponseAsync(deviceId, serverId, requestMain, timeout,
					response => (response.PayloadCase == Device.Main.PayloadOneofCase.DeviceResponse && response.DeviceResponse.CommandType == DeviceCommandType.DeviceConfig && response.DeviceResponse.Config.Operation == DeviceConfigOperation.UpdateCertificate)
					? response.DeviceResponse.Config : null,
					"UpdateDeviceCertificate");
		}


		public Task<Device.DeviceControlResponse> SendRebootDeviceAndAwaitResponseAsync(string deviceId, string serverId, bool forceImmediate, uint delaySeconds, TimeSpan timeout)
		{
			var currentSequence = (uint)Interlocked.Increment(ref _orchestratorSequenceCounter);
			var requestMain = new Device.Main {
				Header = new Device.Header { DeviceId = serverId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
				       DeviceRequest = new DeviceRequest { CommandType = DeviceCommandType.DeviceControl, Control = new DeviceControlRequest { Action = DeviceControlAction.DeviceActionReboot, Reboot = new RebootPayload { ForceImmediate = forceImmediate, DelaySeconds = delaySeconds } } }
			};
			return SendCommandAndAwaitSpecificResponseAsync(deviceId, serverId, requestMain, timeout,
					response => (response.PayloadCase == Device.Main.PayloadOneofCase.DeviceResponse && response.DeviceResponse.CommandType == DeviceCommandType.DeviceControl && response.DeviceResponse.Control.Action == DeviceControlAction.DeviceActionReboot)
					? response.DeviceResponse.Control : null, // Returns the whole DeviceControlResponse
					"RebootDevice");
		}

		public Task<Device.FactoryResetResponse> SendFactoryResetDeviceAndAwaitResponseAsync(string deviceId, string serverId, Device.FactoryResetRequest factoryResetRequestPayload, TimeSpan timeout)
		{
			var currentSequence = (uint)Interlocked.Increment(ref _orchestratorSequenceCounter);
			var requestMain = new Device.Main {
				Header = new Device.Header { DeviceId = serverId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
				       DeviceRequest = new DeviceRequest { CommandType = DeviceCommandType.FactoryReset, FactoryReset = factoryResetRequestPayload }
			};
			return SendCommandAndAwaitSpecificResponseAsync(deviceId, serverId, requestMain, timeout,
					response => (response.PayloadCase == Device.Main.PayloadOneofCase.DeviceResponse && response.DeviceResponse.CommandType == DeviceCommandType.FactoryReset)
					? response.DeviceResponse.FactoryReset : null,
					"FactoryResetDevice");
		}

		public Task<Device.SyncTimeResponse> SendSyncDeviceTimeAndAwaitResponseAsync(string deviceId, string serverId, ulong serverTimestampMsForSync, TimeSpan timeout)
		{
			var currentSequence = (uint)Interlocked.Increment(ref _orchestratorSequenceCounter);
			var requestMain = new Device.Main {
				Header = new Device.Header { DeviceId = serverId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
				       DeviceRequest = new DeviceRequest { CommandType = DeviceCommandType.SyncTime, SyncTime = new SyncTimeRequest { ServerTimestampMs = serverTimestampMsForSync } }
			};
			return SendCommandAndAwaitSpecificResponseAsync(deviceId, serverId, requestMain, timeout,
					response => (response.PayloadCase == Device.Main.PayloadOneofCase.DeviceResponse && response.DeviceResponse.CommandType == DeviceCommandType.SyncTime)
					? response.DeviceResponse.SyncTime : null,
					"SyncDeviceTime");
		}


		public void Dispose()
		{
			_logger.LogInformation("Disposing DeviceCommandOrchestrator.");
			if (_tcpConnectionManager != null)
			{
				_tcpConnectionManager.DeviceMessageReceivedAsync -= HandleDeviceMessageAsync;
			}
			foreach (var tcsPair in _pendingGprcResponses)
			{
				tcsPair.Value.TrySetCanceled();
			}
			_pendingGprcResponses.Clear();
			GC.SuppressFinalize(this);
		}
	}
}
