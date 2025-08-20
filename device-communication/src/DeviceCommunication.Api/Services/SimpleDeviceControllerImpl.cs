using Grpc.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using DeviceCommunication.Infrastructure.Services;
using DeviceCommunication.Core.Grpc.SimpleControl;
using Device; 
using Google.Protobuf;
using Google.Protobuf.Collections;

// Aliases to resolve StatusCode ambiguity
using GrpcCoreStatusCode = Grpc.Core.StatusCode;
using DeviceStatusCode = Device.StatusCode;
using LocalStatusCode = DeviceCommunication.Core.Grpc.SimpleControl.StatusCode; // Haha, yet another StatusCode, eh? TODO Cleanup later


namespace DeviceCommunication.Api.Services
{
	public class SimpleDeviceControllerImpl : SimpleDeviceController.SimpleDeviceControllerBase
	{
		private readonly ILogger<SimpleDeviceControllerImpl> _logger;
		private readonly DeviceCommandOrchestrator _orchestrator;
		private const string ServerIdForCommands = "server-gemini-01"; // Or from config

		public SimpleDeviceControllerImpl(ILogger<SimpleDeviceControllerImpl> logger, DeviceCommandOrchestrator orchestrator)
		{
			_logger = logger;
			_orchestrator = orchestrator;
		}

		private string GetBaseMessage(Device.ResponseBase responseBase, string successMessage = "Operation successful.")
		{
			return responseBase?.Message ?? (responseBase?.Status == DeviceStatusCode.StatusOk ? successMessage : $"Operation failed with status: {responseBase?.Status}");
		}

		// --- Health ---
		public override async Task<SendDeviceHeartbeatResponse> SendDeviceHeartbeat(SendDeviceHeartbeatRequest grpcRequest, ServerCallContext context)
		{
			_logger.LogInformation("gRPC: SendDeviceHeartbeat for {DeviceId}", grpcRequest.DeviceId);
			if (string.IsNullOrEmpty(grpcRequest.DeviceId)) throw new RpcException(new Status(GrpcCoreStatusCode.InvalidArgument, "DeviceId missing."));
			try
			{
				var devResponse = await _orchestrator.SendHeartbeatAndAwaitResponseAsync(grpcRequest.DeviceId, ServerIdForCommands, TimeSpan.FromSeconds(10));
				return new SendDeviceHeartbeatResponse
				{
					Success = devResponse.ResponseBase?.Status == DeviceStatusCode.StatusOk,
						Message = GetBaseMessage(devResponse.ResponseBase, "Heartbeat successful."),
						DeviceTimestampMs = devResponse.DeviceTimestampMs
				};
			}
			catch (RpcException ex) { _logger.LogError(ex, "gRPC SendDeviceHeartbeat failed for {DevId}", grpcRequest.DeviceId); throw; }
			catch (Exception ex) { _logger.LogError(ex, "gRPC SendDeviceHeartbeat unhandled for {DevId}", grpcRequest.DeviceId); throw new RpcException(new Status(GrpcCoreStatusCode.Internal, ex.Message)); }
		}

		public override async Task<GetDeviceHealthStatusResponse> GetDeviceHealthStatus(GetDeviceHealthStatusRequest grpcRequest, ServerCallContext context)
		{
			_logger.LogInformation("gRPC: GetDeviceHealthStatus for {DeviceId}", grpcRequest.DeviceId);
			if (string.IsNullOrEmpty(grpcRequest.DeviceId)) throw new RpcException(new Status(GrpcCoreStatusCode.InvalidArgument, "DeviceId missing."));
			try
			{
				var devResponse = await _orchestrator.SendGetHealthStatusAndAwaitResponseAsync(grpcRequest.DeviceId, ServerIdForCommands, TimeSpan.FromSeconds(10));
				return new GetDeviceHealthStatusResponse
				{
					Success = devResponse.ResponseBase?.Status == DeviceStatusCode.StatusOk,
						Message = GetBaseMessage(devResponse.ResponseBase, "Health status retrieved."),
						IsOperational = devResponse.IsOperational,
						SystemState = devResponse.SystemState,
						TemperatureCelsius = devResponse.TemperatureCelsius,
						UptimeSeconds = devResponse.UptimeSeconds,
						CpuUsagePercent = devResponse.CpuUsagePercent,
						PtpLocked = devResponse.PtpLocked,
						LastResetReason = devResponse.LastResetReason.ToString(), 
				};
			}
			catch (RpcException ex) { _logger.LogError(ex, "gRPC GetDeviceHealthStatus failed for {DevId}", grpcRequest.DeviceId); throw; }
			catch (Exception ex) { _logger.LogError(ex, "gRPC GetDeviceHealthStatus unhandled for {DevId}", grpcRequest.DeviceId); throw new RpcException(new Status(GrpcCoreStatusCode.Internal, ex.Message)); }
		}

		public override async Task<GetDeviceErrorLogResponse> GetDeviceErrorLog(GetDeviceErrorLogRequest grpcRequest, ServerCallContext context)
		{
			_logger.LogInformation("gRPC: GetDeviceErrorLog for {DeviceId}, Page: {Page}", grpcRequest.DeviceId, grpcRequest.PageToken);
			if (string.IsNullOrEmpty(grpcRequest.DeviceId)) throw new RpcException(new Status(GrpcCoreStatusCode.InvalidArgument, "DeviceId missing."));
			try
			{
				var devResponse = await _orchestrator.SendGetErrorLogAndAwaitResponseAsync(grpcRequest.DeviceId, ServerIdForCommands, grpcRequest.PageToken, TimeSpan.FromSeconds(15));
				var grpcResp = new GetDeviceErrorLogResponse
				{
					Success = devResponse.ResponseBase?.Status == DeviceStatusCode.StatusOk,
						Message = GetBaseMessage(devResponse.ResponseBase, "Error log retrieved."),
						NextPageToken = devResponse.NextPageToken,
						TotalMatchingEntries = devResponse.TotalMatchingEntries
				};
				if (devResponse.ErrorLogEntries != null)
				{
					grpcResp.ErrorLogEntries.AddRange(devResponse.ErrorLogEntries.Select(e => new AlertInfoGrpc { Code = e.Code.ToString(), Severity = e.Severity.ToString(), Description = e.Description, TimestampMs = e.TimestampMs }));
				}
				return grpcResp;
			}
			catch (RpcException ex) { _logger.LogError(ex, "gRPC GetDeviceErrorLog failed for {DevId}", grpcRequest.DeviceId); throw; }
			catch (Exception ex) { _logger.LogError(ex, "gRPC GetDeviceErrorLog unhandled for {DevId}", grpcRequest.DeviceId); throw new RpcException(new Status(GrpcCoreStatusCode.Internal, ex.Message)); }
		}

		public override async Task<GenericDeviceResponse> ClearDeviceErrorLog(ClearDeviceErrorLogRequest grpcRequest, ServerCallContext context)
		{
			_logger.LogInformation("gRPC: ClearDeviceErrorLog for {DeviceId}", grpcRequest.DeviceId);
			if (string.IsNullOrEmpty(grpcRequest.DeviceId)) throw new RpcException(new Status(GrpcCoreStatusCode.InvalidArgument, "DeviceId missing."));
			if (string.IsNullOrEmpty(grpcRequest.ConfirmationCode)) throw new RpcException(new Status(GrpcCoreStatusCode.InvalidArgument, "ConfirmationCode missing."));
			try
			{
				var devResponse = await _orchestrator.SendClearErrorLogAndAwaitResponseAsync(grpcRequest.DeviceId, ServerIdForCommands, grpcRequest.ConfirmationCode, TimeSpan.FromSeconds(10));
				return new GenericDeviceResponse
				{
					Success = devResponse.ResponseBase?.Status == DeviceStatusCode.StatusOk,
						Message = GetBaseMessage(devResponse.ResponseBase, "Error log cleared.")
				};
			}
			catch (RpcException ex) { _logger.LogError(ex, "gRPC ClearDeviceErrorLog failed for {DevId}", grpcRequest.DeviceId); throw; }
			catch (Exception ex) { _logger.LogError(ex, "gRPC ClearDeviceErrorLog unhandled for {DevId}", grpcRequest.DeviceId); throw new RpcException(new Status(GrpcCoreStatusCode.Internal, ex.Message)); }
		}

		// --- Firmware ---
		public override async Task<GetDeviceFirmwareInfoResponse> GetDeviceFirmwareInfo(GetDeviceFirmwareInfoRequest grpcRequest, ServerCallContext context)
		{
			_logger.LogInformation("gRPC: GetDeviceFirmwareInfo for {DeviceId}", grpcRequest.DeviceId);
			if (string.IsNullOrEmpty(grpcRequest.DeviceId)) throw new RpcException(new Status(GrpcCoreStatusCode.InvalidArgument, "DeviceId missing."));
			try
			{
				var devResponse = await _orchestrator.SendGetFirmwareInfoAndAwaitResponseAsync(grpcRequest.DeviceId, ServerIdForCommands, TimeSpan.FromSeconds(10));
				return new GetDeviceFirmwareInfoResponse
				{
					Success = devResponse.ResponseBase?.Status == DeviceStatusCode.StatusOk,
						Message = GetBaseMessage(devResponse.ResponseBase, "Firmware info retrieved."),
						Version = devResponse.Version,
						BuildDate = devResponse.BuildDate,
						BuildHash = devResponse.BuildHash,
						SecureBootEnabled = devResponse.SecureBootEnabled,
						CurrentImageSlot = devResponse.CurrentImageSlot
				};
			}
			catch (RpcException ex) { _logger.LogError(ex, "gRPC GetDeviceFirmwareInfo failed for {DevId}", grpcRequest.DeviceId); throw; }
			catch (Exception ex) { _logger.LogError(ex, "gRPC GetDeviceFirmwareInfo unhandled for {DevId}", grpcRequest.DeviceId); throw new RpcException(new Status(GrpcCoreStatusCode.Internal, ex.Message)); }
		}

		public override async Task<PrepareFirmwareUpdateResponse> PrepareFirmwareUpdate(PrepareFirmwareUpdateRequest grpcRequest, ServerCallContext context)
		{
			_logger.LogInformation("gRPC: PrepareFirmwareUpdate for {DeviceId}, Version: {Version}, Size: {Size}", grpcRequest.DeviceId, grpcRequest.FirmwareVersion, grpcRequest.FirmwareSizeBytes);
			if (string.IsNullOrEmpty(grpcRequest.DeviceId)) throw new RpcException(new Status(GrpcCoreStatusCode.InvalidArgument, "DeviceId missing."));
			try
			{
				var devResponseContainer = await _orchestrator.SendPrepareFirmwareUpdateAndAwaitResponseAsync(
						grpcRequest.DeviceId, ServerIdForCommands, grpcRequest.FirmwareSizeBytes,
						grpcRequest.FirmwareVersion, grpcRequest.Signature, grpcRequest.BlockSizePreference, TimeSpan.FromSeconds(15));

				var devResponsePayload = devResponseContainer.Prepare; 
				return new PrepareFirmwareUpdateResponse
				{
					Success = devResponseContainer.ResponseBase?.Status == DeviceStatusCode.StatusOk,
						Message = GetBaseMessage(devResponseContainer.ResponseBase, "Firmware update prepared."),
						ReadyToReceive = devResponsePayload.ReadyToReceive,
						MaxBlockSizeAccepted = devResponsePayload.MaxBlockSize,
						EstimatedStorageTimeSeconds = devResponsePayload.EstimatedStorageTimeSeconds
				};
			}
			catch (RpcException ex) { _logger.LogError(ex, "gRPC PrepareFirmwareUpdate failed for {DevId}", grpcRequest.DeviceId); throw; }
			catch (Exception ex) { _logger.LogError(ex, "gRPC PrepareFirmwareUpdate unhandled for {DevId}", grpcRequest.DeviceId); throw new RpcException(new Status(GrpcCoreStatusCode.Internal, ex.Message)); }
		}

		public override async Task<ExecuteFirmwareUpdateOperationResponse> ExecuteFirmwareUpdateOperation(ExecuteFirmwareUpdateOperationRequest grpcRequest, ServerCallContext context)
		{
			_logger.LogInformation("gRPC: ExecuteFirmwareUpdateOperation for {DeviceId}, Op: {Op}", grpcRequest.DeviceId, grpcRequest.Operation);
			if (string.IsNullOrEmpty(grpcRequest.DeviceId)) throw new RpcException(new Status(GrpcCoreStatusCode.InvalidArgument, "DeviceId missing."));

			Device.UpdateFirmwareRequest deviceUpdateReq = new Device.UpdateFirmwareRequest();
			switch (grpcRequest.Operation)
			{
				case FirmwareUpdateGrpcOperation.FwOpGrpcVerify:
					deviceUpdateReq.Operation = Device.FirmwareUpdateOperation.FirmwareOpVerify;
					deviceUpdateReq.Verify = new Device.FirmwareVerifyPayload { TotalBlocksSent = grpcRequest.TotalBlocksSentForVerify, FullImageCrc32 = grpcRequest.FullImageCrc32ForVerify };
					break;
				case FirmwareUpdateGrpcOperation.FwOpGrpcApply:
					deviceUpdateReq.Operation = Device.FirmwareUpdateOperation.FirmwareOpApply;
					deviceUpdateReq.Apply = new Device.FirmwareApplyPayload { RebootDelaySeconds = grpcRequest.RebootDelaySecondsForApply };
					break;
				case FirmwareUpdateGrpcOperation.FwOpGrpcAbort:
					deviceUpdateReq.Operation = Device.FirmwareUpdateOperation.FirmwareOpAbort;
					deviceUpdateReq.Abort = new Device.FirmwareAbortPayload { Reason = grpcRequest.ReasonForAbort };
					break;
				default:
					throw new RpcException(new Status(GrpcCoreStatusCode.InvalidArgument, "Invalid firmware update operation specified."));
			}

			try
			{
				var devResponseContainer = await _orchestrator.SendExecuteFirmwareUpdateOperationAndAwaitResponseAsync(grpcRequest.DeviceId, ServerIdForCommands, deviceUpdateReq, TimeSpan.FromSeconds(20));
				var grpcResp = new ExecuteFirmwareUpdateOperationResponse
				{
					Success = devResponseContainer.ResponseBase?.Status == DeviceStatusCode.StatusOk,
						Message = GetBaseMessage(devResponseContainer.ResponseBase, $"Firmware op {grpcRequest.Operation} executed.")
				};
				if (devResponseContainer.ResponseBase?.Status == DeviceStatusCode.StatusOk) {
					switch(devResponseContainer.Operation) {
						case Device.FirmwareUpdateOperation.FirmwareOpVerify: grpcResp.VerificationPassed = devResponseContainer.Verify?.VerificationPassed ?? false; break;
						case Device.FirmwareUpdateOperation.FirmwareOpApply: grpcResp.ApplicationScheduled = devResponseContainer.Apply?.ApplicationScheduled ?? false; break;
						case Device.FirmwareUpdateOperation.FirmwareOpAbort: grpcResp.AbortConfirmed = devResponseContainer.Abort?.Aborted ?? false; break;
					}
				}
				return grpcResp;
			}
			catch (RpcException ex) { _logger.LogError(ex, "gRPC ExecuteFirmwareUpdateOperation failed for {DevId}", grpcRequest.DeviceId); throw; }
			catch (Exception ex) { _logger.LogError(ex, "gRPC ExecuteFirmwareUpdateOperation unhandled for {DevId}", grpcRequest.DeviceId); throw new RpcException(new Status(GrpcCoreStatusCode.Internal, ex.Message)); }
		}

		// --- Calibration ---
		public override async Task<ReadDeviceCalibrationParamsResponse> ReadDeviceCalibrationParams(ReadDeviceCalibrationParamsRequest grpcRequest, ServerCallContext context)
		{
			_logger.LogInformation("gRPC: ReadDeviceCalibrationParams for {DeviceId}", grpcRequest.DeviceId);
			if (string.IsNullOrEmpty(grpcRequest.DeviceId)) throw new RpcException(new Status(GrpcCoreStatusCode.InvalidArgument, "DeviceId missing."));
			try
			{
				var devResponse = await _orchestrator.SendReadCalibrationParamsAndAwaitResponseAsync(grpcRequest.DeviceId, ServerIdForCommands, grpcRequest.ChannelIds, TimeSpan.FromSeconds(10));
				var grpcResp = new ReadDeviceCalibrationParamsResponse
				{
					Success = devResponse.ResponseBase?.Status == DeviceStatusCode.StatusOk,
						Message = GetBaseMessage(devResponse.ResponseBase, "Calibration params retrieved.")
				};
				if (devResponse.Parameters != null)
				{
					grpcResp.Parameters.AddRange(devResponse.Parameters.Select(p => new AdcChannelCalibrationParamsGrpc { ChannelId = p.ChannelId, Gain = p.Gain, Offset = p.Offset, LastUpdatedMs = p.LastUpdatedMs, CalibrationExpiresMs = p.CalibrationExpiresMs, TemperatureAtCalCelsius = p.TemperatureAtCalCelsius }));
				}
				return grpcResp;
			}
			catch (RpcException ex) { _logger.LogError(ex, "gRPC ReadDeviceCalibrationParams failed for {DevId}", grpcRequest.DeviceId); throw; }
			catch (Exception ex) { _logger.LogError(ex, "gRPC ReadDeviceCalibrationParams unhandled for {DevId}", grpcRequest.DeviceId); throw new RpcException(new Status(GrpcCoreStatusCode.Internal, ex.Message)); }
		}

		public override async Task<StartDeviceCalibrationProcedureResponse> StartDeviceCalibrationProcedure(StartDeviceCalibrationProcedureRequest grpcRequest, ServerCallContext context)
		{
			_logger.LogInformation("gRPC: StartDeviceCalibrationProcedure for {DeviceId}", grpcRequest.DeviceId);
			if (string.IsNullOrEmpty(grpcRequest.DeviceId)) throw new RpcException(new Status(GrpcCoreStatusCode.InvalidArgument, "DeviceId missing."));
			try
			{
				var devResponse = await _orchestrator.SendStartCalibrationProcedureAndAwaitResponseAsync(grpcRequest.DeviceId, ServerIdForCommands, grpcRequest.ChannelIds, grpcRequest.ForceCalibration, TimeSpan.FromSeconds(10));
				return new StartDeviceCalibrationProcedureResponse
				{
					Success = devResponse.ResponseBase?.Status == DeviceStatusCode.StatusOk,
						Message = GetBaseMessage(devResponse.ResponseBase, "Calibration procedure initiated."),
						ProcedureStarted = devResponse.ProcedureStarted,
						EstimatedDurationSeconds = devResponse.EstimatedDurationSeconds
				};
			}
			catch (RpcException ex) { _logger.LogError(ex, "gRPC StartDeviceCalibrationProcedure failed for {DevId}", grpcRequest.DeviceId); throw; }
			catch (Exception ex) { _logger.LogError(ex, "gRPC StartDeviceCalibrationProcedure unhandled for {DevId}", grpcRequest.DeviceId); throw new RpcException(new Status(GrpcCoreStatusCode.Internal, ex.Message)); }
		}

		public override async Task<GetDeviceCalibrationStatusResponse> GetDeviceCalibrationStatus(GetDeviceCalibrationStatusRequest grpcRequest, ServerCallContext context)
		{
			_logger.LogInformation("gRPC: GetDeviceCalibrationStatus for {DeviceId}", grpcRequest.DeviceId);
			if (string.IsNullOrEmpty(grpcRequest.DeviceId)) throw new RpcException(new Status(GrpcCoreStatusCode.InvalidArgument, "DeviceId missing."));
			try
			{
				var devResponse = await _orchestrator.SendGetCalibrationStatusAndAwaitResponseAsync(grpcRequest.DeviceId, ServerIdForCommands, TimeSpan.FromSeconds(10));
				var grpcResp = new GetDeviceCalibrationStatusResponse
				{
					Success = devResponse.ResponseBase?.Status == DeviceStatusCode.StatusOk,
						Message = GetBaseMessage(devResponse.ResponseBase, "Calibration status retrieved."),
						IsCalibrating = devResponse.IsCalibrating,
						ProgressPercent = devResponse.ProgressPercent,
						TimeRemainingSeconds = devResponse.TimeRemainingSeconds
				};
				if(devResponse.ChannelsInProgress != null) grpcResp.ChannelsInProgress.AddRange(devResponse.ChannelsInProgress);
				return grpcResp;
			}
			catch (RpcException ex) { _logger.LogError(ex, "gRPC GetDeviceCalibrationStatus failed for {DevId}", grpcRequest.DeviceId); throw; }
			catch (Exception ex) { _logger.LogError(ex, "gRPC GetDeviceCalibrationStatus unhandled for {DevId}", grpcRequest.DeviceId); throw new RpcException(new Status(GrpcCoreStatusCode.Internal, ex.Message)); }
		}

		// --- Data ---
		public override async Task<GenericDeviceResponse> ConfigureDeviceDataCollection(ConfigureDeviceDataCollectionRequest grpcRequest, ServerCallContext context)
		{
			_logger.LogInformation("gRPC: ConfigureDeviceDataCollection for {DeviceId}", grpcRequest.DeviceId);
			if (string.IsNullOrEmpty(grpcRequest.DeviceId)) throw new RpcException(new Status(GrpcCoreStatusCode.InvalidArgument, "DeviceId missing."));

			var deviceConfigureRequest = new Device.ConfigureRequest
			{
				SamplingRateHz = grpcRequest.SamplingRateHz,
					       TotalStorageAllocationKb = grpcRequest.TotalStorageAllocationKb,
					       ClearExistingDatasetsOnConfig = grpcRequest.ClearExistingDatasetsOnConfig,
					       MaxConcurrentTriggeredDatasets = grpcRequest.MaxConcurrentTriggeredDatasets,
					       PreTriggerDurationSeconds = grpcRequest.PreTriggerDurationSeconds,
					       PostTriggerDurationSeconds = grpcRequest.PostTriggerDurationSeconds,
					       TotalCaptureDurationMinutes = grpcRequest.TotalCaptureDurationMinutes
			};
			if (grpcRequest.ChannelConfigs != null)
			{
				deviceConfigureRequest.ChannelConfigs.AddRange(grpcRequest.ChannelConfigs.Select(c => new Device.ChannelConfig
							{
							ChannelId = c.ChannelId, Enabled = c.Enabled, InitialPgaGain = c.InitialPgaGain,
							TriggerThresholdLow = c.TriggerThresholdLow, TriggerThresholdHigh = c.TriggerThresholdHigh,
							ReportEventOnThreshold = c.ReportEventOnThreshold
							}));
			}

			try
			{
				var devResponse = await _orchestrator.SendConfigureDataCollectionAndAwaitResponseAsync(grpcRequest.DeviceId, ServerIdForCommands, deviceConfigureRequest, TimeSpan.FromSeconds(10));
				return new GenericDeviceResponse
				{
					Success = devResponse.ResponseBase?.Status == DeviceStatusCode.StatusOk,
						Message = GetBaseMessage(devResponse.ResponseBase, $"Data collection configured. Actual Storage: {devResponse.ActualStorageAllocationKb}KB")
				};
			}
			catch (RpcException ex) { _logger.LogError(ex, "gRPC ConfigureDeviceDataCollection failed for {DevId}", grpcRequest.DeviceId); throw; }
			catch (Exception ex) { _logger.LogError(ex, "gRPC ConfigureDeviceDataCollection unhandled for {DevId}", grpcRequest.DeviceId); throw new RpcException(new Status(GrpcCoreStatusCode.Internal, ex.Message)); }
		}

		public override async Task<GetDeviceStorageInfoResponse> GetDeviceStorageInfo(GetDeviceStorageInfoRequest grpcRequest, ServerCallContext context)
		{
			_logger.LogInformation("gRPC: GetDeviceStorageInfo for {DeviceId}", grpcRequest.DeviceId);
			if (string.IsNullOrEmpty(grpcRequest.DeviceId)) throw new RpcException(new Status(GrpcCoreStatusCode.InvalidArgument, "DeviceId missing."));
			try
			{
				var devResponse = await _orchestrator.SendGetStorageInfoAndAwaitResponseAsync(grpcRequest.DeviceId, ServerIdForCommands, TimeSpan.FromSeconds(10));
				var grpcStorageInfo = new StorageInfoGrpc();
				if(devResponse.StorageInfo != null)
				{
					grpcStorageInfo.TotalStorageKb = devResponse.StorageInfo.TotalStorageKb;
					grpcStorageInfo.UsedStorageKb = devResponse.StorageInfo.UsedStorageKb;
					grpcStorageInfo.AvailableStorageKb = devResponse.StorageInfo.AvailableStorageKb;
					grpcStorageInfo.TotalDatasets = devResponse.StorageInfo.TotalDatasets;
					grpcStorageInfo.OldestDatasetStartTimeNs = devResponse.StorageInfo.OldestDatasetStartTimeNs;
					grpcStorageInfo.NewestDatasetStartTimeNs = devResponse.StorageInfo.NewestDatasetStartTimeNs;
				}
				return new GetDeviceStorageInfoResponse
				{
					Success = devResponse.ResponseBase?.Status == DeviceStatusCode.StatusOk,
						Message = GetBaseMessage(devResponse.ResponseBase, "Storage info retrieved."),
						StorageInfo = grpcStorageInfo
				};
			}
			catch (RpcException ex) { _logger.LogError(ex, "gRPC GetDeviceStorageInfo failed for {DevId}", grpcRequest.DeviceId); throw; }
			catch (Exception ex) { _logger.LogError(ex, "gRPC GetDeviceStorageInfo unhandled for {DevId}", grpcRequest.DeviceId); throw new RpcException(new Status(GrpcCoreStatusCode.Internal, ex.Message)); }
		}

		public override async Task<ListDeviceDatasetsResponse> ListDeviceDatasets(ListDeviceDatasetsRequest grpcRequest, ServerCallContext context)
		{
			_logger.LogInformation("gRPC: ListDeviceDatasets for {DeviceId}", grpcRequest.DeviceId);
			if (string.IsNullOrEmpty(grpcRequest.DeviceId)) throw new RpcException(new Status(GrpcCoreStatusCode.InvalidArgument, "DeviceId missing."));

			var deviceReadDataReq = new Device.ReadDataRequest
			{
				StartTimeFilterNs = grpcRequest.StartTimeFilterNs,
						  EndTimeFilterNs = grpcRequest.EndTimeFilterNs,
						  MaxResults = grpcRequest.MaxResults,
						  PageToken = grpcRequest.PageToken
			};
			try
			{
				var devResponse = await _orchestrator.SendListDatasetsAndAwaitResponseAsync(grpcRequest.DeviceId, ServerIdForCommands, deviceReadDataReq, TimeSpan.FromSeconds(15));
				var grpcResp = new ListDeviceDatasetsResponse
				{
					Success = devResponse.ResponseBase?.Status == DeviceStatusCode.StatusOk,
						Message = GetBaseMessage(devResponse.ResponseBase, "Datasets listed."),
						NextPageToken = devResponse.NextPageToken,
						TotalMatchingDatasets = devResponse.TotalMatchingDatasets
				};
				if(devResponse.Datasets != null)
				{
					grpcResp.Datasets.AddRange(devResponse.Datasets.Select(d => new SignalDataInfoGrpc {
								DatasetId = d.DatasetId, StartTimeNs = d.StartTimeNs, EndTimeNs = d.EndTimeNs, TriggerTimestampNs = d.TriggerTimestampNs,
								PreTriggerDurationSeconds = d.PreTriggerDurationSeconds, PostTriggerDurationSeconds = d.PostTriggerDurationSeconds,
								SampleRateHz = d.SampleRateHz, NumChannels = d.NumChannels, ApproximateSizeKb = d.ApproximateSizeKb, CaptureStatus = d.CaptureStatus.ToString()
								}));
				}
				return grpcResp;
			}
			catch (RpcException ex) { _logger.LogError(ex, "gRPC ListDeviceDatasets failed for {DevId}", grpcRequest.DeviceId); throw; }
			catch (Exception ex) { _logger.LogError(ex, "gRPC ListDeviceDatasets unhandled for {DevId}", grpcRequest.DeviceId); throw new RpcException(new Status(GrpcCoreStatusCode.Internal, ex.Message)); }
		}

		public override async Task<GetDeviceDatasetResponse> GetDeviceDataset(GetDeviceDatasetRequest grpcRequest, ServerCallContext context)
		{
			_logger.LogInformation("gRPC: GetDeviceDataset for {DeviceId}, DatasetID: {DatasetID}", grpcRequest.DeviceId, grpcRequest.DatasetId);
			if (string.IsNullOrEmpty(grpcRequest.DeviceId)) throw new RpcException(new Status(GrpcCoreStatusCode.InvalidArgument, "DeviceId missing."));

			var deviceGetDataReq = new Device.GetDataRequest
			{
				DatasetId = grpcRequest.DatasetId,
					  StartChunkSequenceNumber = grpcRequest.StartChunkSequenceNumber,
					  MaxChunksInResponse = grpcRequest.MaxChunksInResponseHint
			};
			try
			{
				var devResponse = await _orchestrator.SendGetDatasetAndAwaitResponseAsync(grpcRequest.DeviceId, ServerIdForCommands, deviceGetDataReq, TimeSpan.FromSeconds(10));
				return new GetDeviceDatasetResponse
				{
					Success = devResponse.ResponseBase?.Status == DeviceStatusCode.StatusOk,
						Message = GetBaseMessage(devResponse.ResponseBase, "Dataset retrieval initiated/info provided."),
						DatasetId = devResponse.DatasetId,
						TotalChunksInDataset = devResponse.TotalChunksInDataset
				};
			}
			catch (RpcException ex) { _logger.LogError(ex, "gRPC GetDeviceDataset failed for {DevId}", grpcRequest.DeviceId); throw; }
			catch (Exception ex) { _logger.LogError(ex, "gRPC GetDeviceDataset unhandled for {DevId}", grpcRequest.DeviceId); throw new RpcException(new Status(GrpcCoreStatusCode.Internal, ex.Message)); }
		}

		public override async Task<DeleteDeviceDatasetsResponse> DeleteDeviceDatasets(DeleteDeviceDatasetsRequest grpcRequest, ServerCallContext context)
		{
			_logger.LogInformation("gRPC: DeleteDeviceDatasets for {DeviceId}", grpcRequest.DeviceId);
			if (string.IsNullOrEmpty(grpcRequest.DeviceId)) throw new RpcException(new Status(GrpcCoreStatusCode.InvalidArgument, "DeviceId missing."));
			if (!grpcRequest.DeleteAllDatasets && (grpcRequest.DatasetIds == null || !grpcRequest.DatasetIds.Any()))
				throw new RpcException(new Status(GrpcCoreStatusCode.InvalidArgument, "Either dataset_ids or delete_all_datasets must be specified."));
			if (grpcRequest.DeleteAllDatasets && string.IsNullOrEmpty(grpcRequest.ConfirmationCode))
				throw new RpcException(new Status(GrpcCoreStatusCode.InvalidArgument, "Confirmation code required for deleting all datasets."));

			var deviceDeleteReq = new Device.DeleteDataRequest {
				DeleteAllDatasets = grpcRequest.DeleteAllDatasets,
						  ConfirmationCode = grpcRequest.ConfirmationCode ?? ""
			};
			if(grpcRequest.DatasetIds != null) deviceDeleteReq.DatasetIds.AddRange(grpcRequest.DatasetIds);

			try
			{
				var devResponse = await _orchestrator.SendDeleteDatasetsAndAwaitResponseAsync(grpcRequest.DeviceId, ServerIdForCommands, deviceDeleteReq, TimeSpan.FromSeconds(15));
				var grpcResp = new DeleteDeviceDatasetsResponse
				{
					Success = devResponse.ResponseBase?.Status == DeviceStatusCode.StatusOk,
						Message = GetBaseMessage(devResponse.ResponseBase, "Delete operation completed."),
						NotDeletedCount = devResponse.NotDeletedCount,
						FreedStorageKb = devResponse.FreedStorageKb
				};
				if(devResponse.DeletedDatasetIds != null) grpcResp.DeletedDatasetIds.AddRange(devResponse.DeletedDatasetIds);
				return grpcResp;
			}
			catch (RpcException ex) { _logger.LogError(ex, "gRPC DeleteDeviceDatasets failed for {DevId}", grpcRequest.DeviceId); throw; }
			catch (Exception ex) { _logger.LogError(ex, "gRPC DeleteDeviceDatasets unhandled for {DevId}", grpcRequest.DeviceId); throw new RpcException(new Status(GrpcCoreStatusCode.Internal, ex.Message)); }
		}

		public override async Task<ServerInitiatedStartCaptureResponse> ServerInitiatedStartCapture(ServerInitiatedStartCaptureRequest grpcRequest, ServerCallContext context)
		{
			_logger.LogInformation("gRPC: ServerInitiatedStartCapture for {DeviceId}, TriggerTimeNs: {TriggerNs}", grpcRequest.DeviceId, grpcRequest.TriggerTimestampNs);
			if (string.IsNullOrEmpty(grpcRequest.DeviceId)) throw new RpcException(new Status(GrpcCoreStatusCode.InvalidArgument, "DeviceId missing."));
			try
			{
				var devResponse = await _orchestrator.SendServerInitiatedStartCaptureAndAwaitResponseAsync(grpcRequest.DeviceId, ServerIdForCommands, grpcRequest.TriggerTimestampNs, TimeSpan.FromSeconds(10));
				return new ServerInitiatedStartCaptureResponse
				{
					Success = devResponse.ResponseBase?.Status == DeviceStatusCode.StatusOk,
						Message = GetBaseMessage(devResponse.ResponseBase, "Capture initiated by server."),
						AssignedDatasetId = devResponse.AssignedDatasetId,
						CaptureInitiated = devResponse.CaptureInitiated,
						EstimatedCaptureTimeSeconds = devResponse.EstimatedCaptureTimeSeconds
				};
			}
			catch (RpcException ex) { _logger.LogError(ex, "gRPC ServerInitiatedStartCapture failed for {DevId}", grpcRequest.DeviceId); throw; }
			catch (Exception ex) { _logger.LogError(ex, "gRPC ServerInitiatedStartCapture unhandled for {DevId}", grpcRequest.DeviceId); throw new RpcException(new Status(GrpcCoreStatusCode.Internal, ex.Message)); }
		}

		// --- Device Management ---
		public override async Task<GenericDeviceResponse> SetDeviceAssignedName(SetDeviceAssignedNameRequest grpcRequest, ServerCallContext context)
		{
			_logger.LogInformation("gRPC: SetDeviceAssignedName for {DeviceId} to '{Name}'", grpcRequest.DeviceId, grpcRequest.AssignedName);
			if (string.IsNullOrEmpty(grpcRequest.DeviceId)) throw new RpcException(new Status(GrpcCoreStatusCode.InvalidArgument, "DeviceId missing."));
			if (string.IsNullOrEmpty(grpcRequest.AssignedName)) throw new RpcException(new Status(GrpcCoreStatusCode.InvalidArgument, "AssignedName missing."));
			try
			{
				var devResponseContainer = await _orchestrator.SendSetDeviceAssignedNameAndAwaitResponseAsync(grpcRequest.DeviceId, ServerIdForCommands, grpcRequest.AssignedName, TimeSpan.FromSeconds(10));
				var specificResponseBase = devResponseContainer.SetAssignedName?.ResponseBase;
				return new GenericDeviceResponse
				{
					Success = specificResponseBase?.Status == DeviceStatusCode.StatusOk,
						Message = GetBaseMessage(specificResponseBase, "Device name set.")
				};
			}
			catch (RpcException ex) { _logger.LogError(ex, "gRPC SetDeviceAssignedName failed for {DevId}", grpcRequest.DeviceId); throw; }
			catch (Exception ex) { _logger.LogError(ex, "gRPC SetDeviceAssignedName unhandled for {DevId}", grpcRequest.DeviceId); throw new RpcException(new Status(GrpcCoreStatusCode.Internal, ex.Message)); }
		}

		public override async Task<GetDeviceNetworkConfigResponse> GetDeviceNetworkConfig(GetDeviceNetworkConfigRequest grpcRequest, ServerCallContext context)
		{
			_logger.LogInformation("gRPC: GetDeviceNetworkConfig for {DeviceId}", grpcRequest.DeviceId);
			if (string.IsNullOrEmpty(grpcRequest.DeviceId)) throw new RpcException(new Status(GrpcCoreStatusCode.InvalidArgument, "DeviceId missing."));
			try
			{
				var devResponseContainer = await _orchestrator.SendGetDeviceNetworkConfigAndAwaitResponseAsync(grpcRequest.DeviceId, ServerIdForCommands, TimeSpan.FromSeconds(10));
				var devNetSettings = devResponseContainer.GetNetworkConfig?.CurrentSettings;
				var grpcNetSettings = new NetworkSettingsGrpc();
				if(devNetSettings != null)
				{
					grpcNetSettings.UseDhcp = devNetSettings.UseDhcp;
					grpcNetSettings.StaticIpAddress = devNetSettings.StaticIpAddress;
					grpcNetSettings.SubnetMask = devNetSettings.SubnetMask;
					grpcNetSettings.Gateway = devNetSettings.Gateway;
					grpcNetSettings.PrimaryDns = devNetSettings.PrimaryDns;
					grpcNetSettings.SecondaryDns = devNetSettings.SecondaryDns;
				}
				return new GetDeviceNetworkConfigResponse
				{
					Success = devResponseContainer.GetNetworkConfig?.ResponseBase?.Status == DeviceStatusCode.StatusOk,
						Message = GetBaseMessage(devResponseContainer.GetNetworkConfig?.ResponseBase, "Network config retrieved."),
						CurrentSettings = grpcNetSettings
				};
			}
			catch (RpcException ex) { _logger.LogError(ex, "gRPC GetDeviceNetworkConfig failed for {DevId}", grpcRequest.DeviceId); throw; }
			catch (Exception ex) { _logger.LogError(ex, "gRPC GetDeviceNetworkConfig unhandled for {DevId}", grpcRequest.DeviceId); throw new RpcException(new Status(GrpcCoreStatusCode.Internal, ex.Message)); }
		}

		public override async Task<GenericDeviceResponse> SetDeviceNetworkConfig(SetDeviceNetworkConfigRequest grpcRequest, ServerCallContext context)
		{
			_logger.LogInformation("gRPC: SetDeviceNetworkConfig for {DeviceId}", grpcRequest.DeviceId);
			if (string.IsNullOrEmpty(grpcRequest.DeviceId)) throw new RpcException(new Status(GrpcCoreStatusCode.InvalidArgument, "DeviceId missing."));
			if (grpcRequest.Settings == null) throw new RpcException(new Status(GrpcCoreStatusCode.InvalidArgument, "Network settings payload missing."));

			var deviceNetSettings = new Device.NetworkSettings {
				UseDhcp = grpcRequest.Settings.UseDhcp,
					StaticIpAddress = grpcRequest.Settings.StaticIpAddress ?? "",
					SubnetMask = grpcRequest.Settings.SubnetMask ?? "",
					Gateway = grpcRequest.Settings.Gateway ?? "",
					PrimaryDns = grpcRequest.Settings.PrimaryDns ?? "",
					SecondaryDns = grpcRequest.Settings.SecondaryDns ?? ""
			};
			try
			{
				var devResponseContainer = await _orchestrator.SendSetDeviceNetworkConfigAndAwaitResponseAsync(grpcRequest.DeviceId, ServerIdForCommands, deviceNetSettings, TimeSpan.FromSeconds(15));
				var specificResponseBase = devResponseContainer.SetNetworkConfig?.ResponseBase;
				return new GenericDeviceResponse
				{
					Success = specificResponseBase?.Status == DeviceStatusCode.StatusOk,
						Message = GetBaseMessage(specificResponseBase, "Network config set.")
				};
			}
			catch (RpcException ex) { _logger.LogError(ex, "gRPC SetDeviceNetworkConfig failed for {DevId}", grpcRequest.DeviceId); throw; }
			catch (Exception ex) { _logger.LogError(ex, "gRPC SetDeviceNetworkConfig unhandled for {DevId}", grpcRequest.DeviceId); throw new RpcException(new Status(GrpcCoreStatusCode.Internal, ex.Message)); }
		}

		public override async Task<GetDeviceCertificateInfoResponse> GetDeviceCertificateInfo(GetDeviceCertificateInfoRequest grpcRequest, ServerCallContext context)
		{
			_logger.LogInformation("gRPC: GetDeviceCertificateInfo for {DeviceId}", grpcRequest.DeviceId);
			if (string.IsNullOrEmpty(grpcRequest.DeviceId)) throw new RpcException(new Status(GrpcCoreStatusCode.InvalidArgument, "DeviceId missing."));
			try
			{
				var devResponseContainer = await _orchestrator.SendGetDeviceCertificateInfoAndAwaitResponseAsync(grpcRequest.DeviceId, ServerIdForCommands, TimeSpan.FromSeconds(10));
				var devCertInfo = devResponseContainer.GetCertificateInfo?.TlsClientCertInfo;
				var grpcCertInfo = new CertificateInfoGrpc();
				if(devCertInfo != null) {
					grpcCertInfo.CertificateDer = devCertInfo.CertificateDer;
					grpcCertInfo.SubjectName = devCertInfo.SubjectName;
					grpcCertInfo.IssuerName = devCertInfo.IssuerName;
					grpcCertInfo.ValidNotBeforeMs = devCertInfo.ValidNotBeforeMs;
					grpcCertInfo.ValidNotAfterMs = devCertInfo.ValidNotAfterMs;
				}
				return new GetDeviceCertificateInfoResponse
				{
					Success = devResponseContainer.GetCertificateInfo?.ResponseBase?.Status == DeviceStatusCode.StatusOk,
						Message = GetBaseMessage(devResponseContainer.GetCertificateInfo?.ResponseBase, "Certificate info retrieved."),
						TlsClientCertInfo = grpcCertInfo
				};
			}
			catch (RpcException ex) { _logger.LogError(ex, "gRPC GetDeviceCertificateInfo failed for {DevId}", grpcRequest.DeviceId); throw; }
			catch (Exception ex) { _logger.LogError(ex, "gRPC GetDeviceCertificateInfo unhandled for {DevId}", grpcRequest.DeviceId); throw new RpcException(new Status(GrpcCoreStatusCode.Internal, ex.Message)); }
		}

		public override async Task<GenerateDeviceCSRResponse> GenerateDeviceCSR(GenerateDeviceCSRRequest grpcRequest, ServerCallContext context)
		{
			_logger.LogInformation("gRPC: GenerateDeviceCSR for {DeviceId}", grpcRequest.DeviceId);
			if (string.IsNullOrEmpty(grpcRequest.DeviceId)) throw new RpcException(new Status(GrpcCoreStatusCode.InvalidArgument, "DeviceId missing."));
			try
			{
				var devResponseContainer = await _orchestrator.SendGenerateDeviceCSRAndAwaitResponseAsync(grpcRequest.DeviceId, ServerIdForCommands, TimeSpan.FromSeconds(20)); 
				return new GenerateDeviceCSRResponse
				{
					Success = devResponseContainer.GenerateCsr?.ResponseBase?.Status == DeviceStatusCode.StatusOk,
						Message = GetBaseMessage(devResponseContainer.GenerateCsr?.ResponseBase, "CSR generated."),
						CsrDer = devResponseContainer.GenerateCsr?.CsrDer ?? ByteString.Empty
				};
			}
			catch (RpcException ex) { _logger.LogError(ex, "gRPC GenerateDeviceCSR failed for {DevId}", grpcRequest.DeviceId); throw; }
			catch (Exception ex) { _logger.LogError(ex, "gRPC GenerateDeviceCSR unhandled for {DevId}", grpcRequest.DeviceId); throw new RpcException(new Status(GrpcCoreStatusCode.Internal, ex.Message)); }
		}

		public override async Task<GenericDeviceResponse> UpdateDeviceCertificate(UpdateDeviceCertificateRequest grpcRequest, ServerCallContext context)
		{
			_logger.LogInformation("gRPC: UpdateDeviceCertificate for {DeviceId}", grpcRequest.DeviceId);
			if (string.IsNullOrEmpty(grpcRequest.DeviceId)) throw new RpcException(new Status(GrpcCoreStatusCode.InvalidArgument, "DeviceId missing."));
			if (grpcRequest.NewCertificateDer == null || grpcRequest.NewCertificateDer.IsEmpty) throw new RpcException(new Status(GrpcCoreStatusCode.InvalidArgument, "NewCertificateDer missing."));
			try
			{
				var devResponseContainer = await _orchestrator.SendUpdateDeviceCertificateAndAwaitResponseAsync(grpcRequest.DeviceId, ServerIdForCommands, grpcRequest.NewCertificateDer, TimeSpan.FromSeconds(15));
				var specificResponseBase = devResponseContainer.UpdateCertificate?.ResponseBase;
				return new GenericDeviceResponse
				{
					Success = specificResponseBase?.Status == DeviceStatusCode.StatusOk,
						Message = GetBaseMessage(specificResponseBase, "Certificate updated.")
				};
			}
			catch (RpcException ex) { _logger.LogError(ex, "gRPC UpdateDeviceCertificate failed for {DevId}", grpcRequest.DeviceId); throw; }
			catch (Exception ex) { _logger.LogError(ex, "gRPC UpdateDeviceCertificate unhandled for {DevId}", grpcRequest.DeviceId); throw new RpcException(new Status(GrpcCoreStatusCode.Internal, ex.Message)); }
		}

		public override async Task<GenericDeviceResponse> RebootDevice(RebootDeviceRequest grpcRequest, ServerCallContext context)
		{
			_logger.LogInformation("gRPC: RebootDevice for {DeviceId}, Force: {Force}, Delay: {Delay}s", grpcRequest.DeviceId, grpcRequest.ForceImmediate, grpcRequest.DelaySeconds);
			if (string.IsNullOrEmpty(grpcRequest.DeviceId)) throw new RpcException(new Status(GrpcCoreStatusCode.InvalidArgument, "DeviceId missing."));
			try
			{
				var devResponseContainer = await _orchestrator.SendRebootDeviceAndAwaitResponseAsync(grpcRequest.DeviceId, ServerIdForCommands, grpcRequest.ForceImmediate, grpcRequest.DelaySeconds, TimeSpan.FromSeconds(10));
				var specificResponseBase = devResponseContainer.ResponseBase; 
				return new GenericDeviceResponse
				{
					Success = specificResponseBase?.Status == DeviceStatusCode.StatusOk && (devResponseContainer.Reboot?.RebootScheduled ?? false),
						Message = GetBaseMessage(specificResponseBase, $"Reboot scheduled: {devResponseContainer.Reboot?.RebootScheduled}.")
				};
			}
			catch (RpcException ex) { _logger.LogError(ex, "gRPC RebootDevice failed for {DevId}", grpcRequest.DeviceId); throw; }
			catch (Exception ex) { _logger.LogError(ex, "gRPC RebootDevice unhandled for {DevId}", grpcRequest.DeviceId); throw new RpcException(new Status(GrpcCoreStatusCode.Internal, ex.Message)); }
		}

		public override async Task<GenericDeviceResponse> FactoryResetDevice(FactoryResetDeviceRequest grpcRequest, ServerCallContext context)
		{
			_logger.LogInformation("gRPC: FactoryResetDevice for {DeviceId}", grpcRequest.DeviceId);
			if (string.IsNullOrEmpty(grpcRequest.DeviceId)) throw new RpcException(new Status(GrpcCoreStatusCode.InvalidArgument, "DeviceId missing."));
			if (string.IsNullOrEmpty(grpcRequest.ConfirmationCode)) throw new RpcException(new Status(GrpcCoreStatusCode.InvalidArgument, "ConfirmationCode missing."));

			var deviceFactoryResetReq = new Device.FactoryResetRequest {
				ConfirmationCode = grpcRequest.ConfirmationCode,
						 PreserveDeviceId = grpcRequest.PreserveDeviceId,
						 PreserveNetworkConfig = grpcRequest.PreserveNetworkConfig,
						 PreserveCalibration = grpcRequest.PreserveCalibration
			};
			try
			{
				var devResponse = await _orchestrator.SendFactoryResetDeviceAndAwaitResponseAsync(grpcRequest.DeviceId, ServerIdForCommands, deviceFactoryResetReq, TimeSpan.FromSeconds(15));
				return new GenericDeviceResponse
				{
					Success = devResponse.ResponseBase?.Status == DeviceStatusCode.StatusOk && devResponse.ResetScheduled,
						Message = GetBaseMessage(devResponse.ResponseBase, $"Factory reset scheduled: {devResponse.ResetScheduled}, Delay: {devResponse.ResetDelaySeconds}s.")
				};
			}
			catch (RpcException ex) { _logger.LogError(ex, "gRPC FactoryResetDevice failed for {DevId}", grpcRequest.DeviceId); throw; }
			catch (Exception ex) { _logger.LogError(ex, "gRPC FactoryResetDevice unhandled for {DevId}", grpcRequest.DeviceId); throw new RpcException(new Status(GrpcCoreStatusCode.Internal, ex.Message)); }
		}

		public override async Task<SyncDeviceTimeResponse> SyncDeviceTime(SyncDeviceTimeRequest grpcRequest, ServerCallContext context)
		{
			_logger.LogInformation("gRPC: SyncDeviceTime for {DeviceId}", grpcRequest.DeviceId);
			if (string.IsNullOrEmpty(grpcRequest.DeviceId)) throw new RpcException(new Status(GrpcCoreStatusCode.InvalidArgument, "DeviceId missing."));
			try
			{
				var devResponse = await _orchestrator.SendSyncDeviceTimeAndAwaitResponseAsync(grpcRequest.DeviceId, ServerIdForCommands, grpcRequest.ServerTimestampMsForSync, TimeSpan.FromSeconds(10));
				return new SyncDeviceTimeResponse
				{
					Success = devResponse.ResponseBase?.Status == DeviceStatusCode.StatusOk,
						Message = GetBaseMessage(devResponse.ResponseBase, "Time sync operation completed."),
						DeviceTimeBeforeSyncMs = devResponse.DeviceTimeBeforeSyncMs,
						DeviceTimeAfterSyncMs = devResponse.DeviceTimeAfterSyncMs,
						OffsetAppliedMs = devResponse.OffsetAppliedMs,
						PtpStatus = devResponse.PtpStatus.ToString(), 
						PtpOffsetNanoseconds = devResponse.PtpOffsetNanoseconds,
						PtpMasterId = devResponse.PtpMasterId
				};
			}
			catch (RpcException ex) { _logger.LogError(ex, "gRPC SyncDeviceTime failed for {DevId}", grpcRequest.DeviceId); throw; }
			catch (Exception ex) { _logger.LogError(ex, "gRPC SyncDeviceTime unhandled for {DevId}", grpcRequest.DeviceId); throw new RpcException(new Status(GrpcCoreStatusCode.Internal, ex.Message)); }
		}

		public override async Task<DauObjects> GetAllDaus(DauList grpcRequest, ServerCallContext context)
		{
			_logger.LogInformation("gRPC: SimpleDeviceController.GetAllDaus requested. Device ID filter count: {Count}", grpcRequest.DeviceId.Count);
			var dauObjectsResponse = new DauObjects();

			List<string> deviceIdsToQuery = new List<string>();

			if (grpcRequest.DeviceId.Any()) // Use the DeviceId field from DauList message
			{
				deviceIdsToQuery.AddRange(grpcRequest.DeviceId);
			}
			else
			{
				_logger.LogInformation("GetAllDaus: No specific device IDs provided in filter. TODO: Implement logic to fetch all relevant/active device IDs from a known source (e.g., TcpConnectionManager or database).");
				// Example: If you had a way to get IDs from TcpConnectionManager:
				// var connectedIds = _tcpConnectionManager.GetActiveDeviceIds();
				// deviceIdsToQuery.AddRange(connectedIds);
				// For now, if no filter, it will process an empty list.
			}

			foreach (var deviceId in deviceIdsToQuery)
			{
				try
				{
					_logger.LogDebug("GetAllDaus: Fetching info for device {DeviceId}", deviceId);

					// Call existing public methods OF THIS SAME CLASS which use the orchestrator
					var healthStatusRespTask = this.GetDeviceHealthStatus(new GetDeviceHealthStatusRequest { DeviceId = deviceId }, context);
					var networkConfigRespTask = this.GetDeviceNetworkConfig(new GetDeviceNetworkConfigRequest { DeviceId = deviceId }, context);
					// var firmwareInfoRespTask = this.GetDeviceFirmwareInfo(new GetDeviceFirmwareInfoRequest { DeviceId = deviceId }, context);

					// Await all tasks concurrently for efficiency
					await Task.WhenAll(healthStatusRespTask, networkConfigRespTask /*, firmwareInfoRespTask */);

					var healthStatusResp = healthStatusRespTask.Result;
					var networkConfigResp = networkConfigRespTask.Result;
					// var firmwareInfoResp = firmwareInfoRespTask.Result;


					var dau = new Dau // This is DeviceCommunication.Core.Grpc.SimpleControl.Dau
					{
						DeviceId = deviceId,
							 IsOperational = healthStatusResp.Success && healthStatusResp.IsOperational,
							 LastHeartbeat = 0, // Placeholder: Server needs to track this.
									    // This could come from healthStatusResp if you add a field there.
									    // FirmwareVersion = firmwareInfoResp.Success ? firmwareInfoResp.Version : "N/A", // If Dau message had it
					};

					if (networkConfigResp.Success && networkConfigResp.CurrentSettings != null)
					{
						dau.StaticIpAddress = networkConfigResp.CurrentSettings.StaticIpAddress ?? "";
						dau.SubnetMask = networkConfigResp.CurrentSettings.SubnetMask ?? "";
						dau.Gateway = networkConfigResp.CurrentSettings.Gateway ?? "";
						dau.PrimaryDns = networkConfigResp.CurrentSettings.PrimaryDns ?? "";
						dau.SecondaryDns = networkConfigResp.CurrentSettings.SecondaryDns ?? "";
					}
					dauObjectsResponse.Dau.Add(dau);
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "GetAllDaus: Failed to retrieve full info for device {DeviceId}", deviceId);
					dauObjectsResponse.Dau.Add(new Dau { DeviceId = deviceId, IsOperational = false });
				}
			}
			// If you add success/message to DauObjects in proto:
			// dauObjectsResponse.Success = true; // Assuming partial success is still overall success
			// dauObjectsResponse.Message = "DAU list retrieval attempted.";
			return dauObjectsResponse;
		}

		public override async Task<Core.Grpc.SimpleControl.ResponseBase> UpdateFirmware(UpdatePayload grpcRequest, ServerCallContext context)
		{
			_logger.LogInformation("gRPC SimpleDeviceController: UpdateFirmware for {DeviceId}, Version: {Version}, Type: {Type}, DataSize: {Size}, Checksum: {Checksum}, Timestamp: {Timestamp}",
					grpcRequest.DeviceId, grpcRequest.Version, grpcRequest.FirmwareType, grpcRequest.FirmwareData.Length, grpcRequest.Checksum, grpcRequest.Timestamp);

			if (string.IsNullOrEmpty(grpcRequest.DeviceId))
				return new Core.Grpc.SimpleControl.ResponseBase { Status = LocalStatusCode.StatusInvalidParam, Message = "DeviceId is required." };
			if (grpcRequest.FirmwareData.IsEmpty)
				return new Core.Grpc.SimpleControl.ResponseBase { Status = LocalStatusCode.StatusInvalidParam, Message = "FirmwareData is required." };

			try
			{
				// 1. Call PrepareFirmwareUpdate (existing public method of this class)
				var prepareGrpcRequest = new PrepareFirmwareUpdateRequest // This is an existing gRPC DTO
				{
					DeviceId = grpcRequest.DeviceId,
						 FirmwareSizeBytes = (uint)grpcRequest.FirmwareData.Length,
						 FirmwareVersion = grpcRequest.Version,
						 Signature = ByteString.Empty, // Placeholder: Real signature from grpcRequest.checksum or data needed
						 BlockSizePreference = 1024
				};
				var prepareGrpcResponse = await this.PrepareFirmwareUpdate(prepareGrpcRequest, context);

				if (!prepareGrpcResponse.Success || !prepareGrpcResponse.ReadyToReceive)
				{
					return new Core.Grpc.SimpleControl.ResponseBase { Status = LocalStatusCode.StatusError, Message = $"Device failed to prepare for update: {prepareGrpcResponse.Message}" };
				}
				_logger.LogInformation("UpdateFirmware: Device {DeviceId} prepared. MaxBlockSize: {BlockSize}. Firmware Type: {FirmwareType}",
						grpcRequest.DeviceId, prepareGrpcResponse.MaxBlockSizeAccepted, grpcRequest.FirmwareType);

				_logger.LogWarning("UpdateFirmware: FIRMWARE DATA (from grpcRequest.FirmwareData) TRANSFER LOGIC IS A STUB/TODO. Device prepared. Actual data transfer (using firmware_data bytes) and apply steps need to be implemented if this RPC is to be fully functional by sending blocks to the device.");
				// In a real scenario, after Prepare, you would:
				// - Loop through grpcRequest.FirmwareData in chunks.
				// - For each chunk, you'd need a device-level command (e.g., a new case in Device.FirmwareRequest for TRANSFER_BLOCK).
				// - You'd need a new method in DeviceCommandOrchestrator to send this block and await an ACK.
				// - This UpdateFirmware method would call that new orchestrator method repeatedly.
				// - Finally, call this.ExecuteFirmwareUpdateOperation for VERIFY and APPLY.

				return new Core.Grpc.SimpleControl.ResponseBase { Status = LocalStatusCode.StatusOk, Message = "Firmware update process initiated (Prepare OK). Data transfer and apply are subsequent/separate steps not yet implemented by this specific RPC call." };
			}
			catch (RpcException ex)
			{
				_logger.LogError(ex, "gRPC SimpleDeviceController: UpdateFirmware for {DeviceId} failed during a sub-RPC call.", grpcRequest.DeviceId);
				return new Core.Grpc.SimpleControl.ResponseBase { Status = LocalStatusCode.StatusError, Message = $"gRPC error: {ex.Status.Detail}" };
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "gRPC SimpleDeviceController: UpdateFirmware for {DeviceId} failed.", grpcRequest.DeviceId);
				return new Core.Grpc.SimpleControl.ResponseBase { Status = LocalStatusCode.StatusError, Message = $"Internal server error: {ex.Message}" };
			}
		}

		public override async Task<Core.Grpc.SimpleControl.ResponseBase> ConfigureDau(Configuration grpcRequest, ServerCallContext context)
		{
			// 'Configuration' is your request type from the proto
			// 'Core.Grpc.SimpleControl.ResponseBase' is your response type from the proto
			if (grpcRequest.Dau == null || string.IsNullOrEmpty(grpcRequest.Dau.DeviceId))
			{
				return new Core.Grpc.SimpleControl.ResponseBase { Status = LocalStatusCode.StatusInvalidParam, Message = "DAU object or DAU.device_id cannot be null/empty." };
			}
			var deviceId = grpcRequest.Dau.DeviceId;
			_logger.LogInformation("gRPC SimpleDeviceController: ConfigureDau for {DeviceId}", deviceId);

			bool overallSuccess = true;
			StringBuilder messages = new StringBuilder();

			try
			{
				// For this quick solution, we assume Configuration.Dau contains fields that map to
				// existing individual device configuration RPCs.
				// The Dau message currently has network fields. Let's use those.

				bool hasNetworkSettingsToApply =
					!string.IsNullOrEmpty(grpcRequest.Dau.StaticIpAddress) ||
					!string.IsNullOrEmpty(grpcRequest.Dau.SubnetMask) ||
					!string.IsNullOrEmpty(grpcRequest.Dau.Gateway) ||
					!string.IsNullOrEmpty(grpcRequest.Dau.PrimaryDns) ||
					!string.IsNullOrEmpty(grpcRequest.Dau.SecondaryDns);

				if (hasNetworkSettingsToApply)
				{
					_logger.LogInformation("ConfigureDau: Applying network settings for {DeviceId}", deviceId);
					var networkSettingsDto = new NetworkSettingsGrpc // This DTO is already defined for SimpleDeviceController
					{
						UseDhcp = string.IsNullOrEmpty(grpcRequest.Dau.StaticIpAddress), // Infer UseDhcp
							StaticIpAddress = grpcRequest.Dau.StaticIpAddress ?? "",
							SubnetMask = grpcRequest.Dau.SubnetMask ?? "",
							Gateway = grpcRequest.Dau.Gateway ?? "",
							PrimaryDns = grpcRequest.Dau.PrimaryDns ?? "",
							SecondaryDns = grpcRequest.Dau.SecondaryDns ?? ""
					};
					var setNetworkGrpcRequest = new SetDeviceNetworkConfigRequest // Existing gRPC DTO
					{
						DeviceId = deviceId,
							 Settings = networkSettingsDto
					};
					// Call existing public method of this class
					var networkGrpcResponse = await this.SetDeviceNetworkConfig(setNetworkGrpcRequest, context);
					if (!networkGrpcResponse.Success) // Assuming SetDeviceNetworkConfig returns GenericDeviceResponse
					{
						overallSuccess = false;
						messages.AppendLine($"Network config failed: {networkGrpcResponse.Message}");
					}
					else
					{
						messages.AppendLine("Network config successful.");
					}
				} else {
					messages.AppendLine("No specific network settings provided in Dau message to apply for ConfigureDau.");
				}

				// TODO: If 'Configuration' or 'Dau' message gets more fields (e.g., assigned_name, data collection params),
				// add logic here to call other existing methods like this.SetDeviceAssignedName(), this.ConfigureDeviceDataCollection(), etc.

				return new Core.Grpc.SimpleControl.ResponseBase
				{
					Status = overallSuccess ? LocalStatusCode.StatusOk : LocalStatusCode.StatusError,
					       Message = messages.Length > 0 ? messages.ToString().Trim() : (overallSuccess ? "Configuration applied (or no changes specified)." : "Configuration partial failure.")
				};
			}
			catch (RpcException ex)
			{
				_logger.LogError(ex, "gRPC SimpleDeviceController: ConfigureDau for {DeviceId} failed during a sub-RPC call.", deviceId);
				return new Core.Grpc.SimpleControl.ResponseBase { Status = LocalStatusCode.StatusError, Message = $"gRPC error: {ex.Status.Detail}" };
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "gRPC SimpleDeviceController: ConfigureDau for {DeviceId} failed.", deviceId);
				return new Core.Grpc.SimpleControl.ResponseBase { Status = LocalStatusCode.StatusError, Message = $"Internal server error: {ex.Message}" };
			}
		}

		private string FormatApiGatewayResponseMessage(Core.Grpc.SimpleControl.ResponseBase apiResponse)
		{
			if (apiResponse == null) return "Unknown error (null response).";
			return apiResponse.Message ?? (apiResponse.Status == LocalStatusCode.StatusOk ? "Operation successful." : $"Operation failed with status: {apiResponse.Status}");
		}

	}
}
