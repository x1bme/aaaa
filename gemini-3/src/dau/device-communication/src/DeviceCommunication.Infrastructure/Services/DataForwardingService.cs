using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Device;
using Archiver.Api.Grpc;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace DeviceCommunication.Infrastructure.Services
{
	public class DataForwardingService : IDisposable
	{
		private class ActiveArchiveStream
		{
			public IClientStreamWriter<ArchiveRequest> RequestStream { get; init; }
			public Task<ArchiveResponse> ResponseTask { get; init; }
			public uint TotalChunksExpected { get; init; }
			public uint ChunksSent { get; set; } = 0;
		}

		private readonly ILogger<DataForwardingService> _logger;
		private readonly TcpConnectionManager _tcpConnectionManager;
		private readonly ArchiverService.ArchiverServiceClient _archiverClient;

		private readonly ConcurrentDictionary<uint, ActiveArchiveStream> _activeStreams = new();

		public DataForwardingService(
				ILogger<DataForwardingService> logger,
				TcpConnectionManager tcpConnectionManager,
				ArchiverService.ArchiverServiceClient archiverClient)
		{
			_logger = logger;
			_tcpConnectionManager = tcpConnectionManager;
			_archiverClient = archiverClient;

			_tcpConnectionManager.DeviceMessageReceivedAsync += OnDeviceMessageReceived;
			_logger.LogInformation("DataForwardingService initialized and subscribed to TcpConnectionManager events.");
		}

		private async Task OnDeviceMessageReceived(string deviceId, Main message)
		{
			if (message.PayloadCase != Main.PayloadOneofCase.DataResponse ||
					message.DataResponse.CommandResponsePayloadCase != DataResponse.CommandResponsePayloadOneofCase.ManageData ||
					message.DataResponse.ManageData.OperationResponsePayloadCase != ManageDataResponse.OperationResponsePayloadOneofCase.Get)
			{
				return;
			}

			var getDataResp = message.DataResponse.ManageData.Get;
			var datasetId = getDataResp.DatasetId;

			try
			{
				if (!_activeStreams.ContainsKey(datasetId))
				{
					_logger.LogInformation("First chunk received for new DatasetID {DatasetId}. Initiating gRPC stream to Archiver.", datasetId);

					var call = _archiverClient.ArchiveDataset();

					var newStream = new ActiveArchiveStream
					{
						RequestStream = call.RequestStream,
							      ResponseTask = call.ResponseAsync,
							      TotalChunksExpected = getDataResp.TotalChunksInDataset
					};

					if (!_activeStreams.TryAdd(datasetId, newStream))
					{
						await call.RequestStream.CompleteAsync();
						_logger.LogWarning("Failed to add new archive stream for DatasetID {DatasetId} due to race condition.", datasetId);
						return;
					}

					var metadata = new DatasetMetadata
					{
						DatasetId = datasetId.ToString(),
							  DeviceId = deviceId,
							  SampleRateHz = 1000, 
							  NumChannels = 2,
							  StartTimeUtc = Timestamp.FromDateTime(DateTime.UtcNow) // Approximate start time
					};
					await newStream.RequestStream.WriteAsync(new ArchiveRequest { Metadata = metadata });
				}

				if (_activeStreams.TryGetValue(datasetId, out var activeStream))
				{
					foreach (var deviceChunk in getDataResp.DataChunks)
					{
						var archiverChunk = new Archiver.Api.Grpc.SignalDataChunk
						{
							SequenceNumber = deviceChunk.SequenceNumber
						};
						archiverChunk.RawAdcValues.AddRange(deviceChunk.RawAdcValues);

						await activeStream.RequestStream.WriteAsync(new ArchiveRequest { DataChunk = archiverChunk });
						activeStream.ChunksSent++;
					}
					_logger.LogDebug("Forwarded {ChunkCount} chunks for DatasetID {DatasetId}. Total sent: {Sent}/{Expected}", 
							getDataResp.DataChunks.Count, datasetId, activeStream.ChunksSent, activeStream.TotalChunksExpected);

					if (activeStream.ChunksSent >= activeStream.TotalChunksExpected)
					{
						await CompleteAndCleanupStream(datasetId, activeStream);
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error forwarding data for DatasetID {DatasetId}. The stream will be aborted.", datasetId);
				if (_activeStreams.TryGetValue(datasetId, out var streamToAbort))
				{
					await CompleteAndCleanupStream(datasetId, streamToAbort, success: false);
				}
			}
		}

		private async Task CompleteAndCleanupStream(uint datasetId, ActiveArchiveStream stream, bool success = true)
		{
			_logger.LogInformation("Completing archive stream for DatasetID {DatasetId}. Success: {Success}", datasetId, success);

			await stream.RequestStream.CompleteAsync();

			_ = Task.Run(async () =>
					{
					try
					{
					var summary = await stream.ResponseTask;
					_logger.LogInformation("Archiver summary for DatasetID {DatasetId}: Chunks={Chunks}, Samples={Samples}, Success={Success}, Msg='{Message}'",
							summary.DatasetId, summary.ChunksReceived, summary.SamplesReceived, summary.Success, summary.Message);
					}
					catch (RpcException ex)
					{
					_logger.LogError(ex, "Archiver returned an error for DatasetID {DatasetId}: {Status}", datasetId, ex.Status);
					}
					});

			_activeStreams.TryRemove(datasetId, out _);
		}

		public void Dispose()
		{
			_logger.LogInformation("Disposing DataForwardingService.");
			if (_tcpConnectionManager != null)
			{
				_tcpConnectionManager.DeviceMessageReceivedAsync -= OnDeviceMessageReceived;
			}
			foreach (var streamPair in _activeStreams)
			{
				_ = CompleteAndCleanupStream(streamPair.Key, streamPair.Value, success: false);
			}
		}
	}
}
