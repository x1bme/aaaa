using Grpc.Core;
using Archiver.Api.Grpc;

namespace Archiver.Api.Services;

public class ArchiverServiceImpl : ArchiverService.ArchiverServiceBase
{
	private readonly ILogger<ArchiverServiceImpl> _logger;

	public ArchiverServiceImpl(ILogger<ArchiverServiceImpl> logger)
	{
		_logger = logger;
	}

	public override async Task<ArchiveResponse> ArchiveDataset(
			IAsyncStreamReader<ArchiveRequest> requestStream, 
			ServerCallContext context)
	{
		_logger.LogInformation("New archive stream initiated by a client.");

		// Wait for and process the initial metadata message ---
		if (!await requestStream.MoveNext())
		{
			_logger.LogError("Client disconnected before sending any data.");
			return new ArchiveResponse { Success = false, Message = "Stream was empty." };
		}

		var metadata = requestStream.Current.Metadata;
		if (metadata == null)
		{
			_logger.LogError("The first message in the stream was not DatasetMetadata.");
			return new ArchiveResponse { Success = false, Message = "Protocol error: Metadata must be the first message." };
		}

		_logger.LogInformation(
				"Receiving dataset '{DatasetId}' from device '{DeviceId}'. Sample Rate: {RateHz} Hz, Channels: {Channels}", 
				metadata.DatasetId, metadata.DeviceId, metadata.SampleRateHz, metadata.NumChannels);

		// TODO: Hudson/Khalid to put database connection or queuing mechanism here!

		long totalChunksReceived = 0;
		long totalSamplesReceived = 0;

		try
		{
			// --- Loop  ---
			while (await requestStream.MoveNext())
			{
				var dataChunk = requestStream.Current.DataChunk;
				if (dataChunk == null)
				{
					_logger.LogWarning("Received a message in the stream that was not a SignalDataChunk. Ignoring.");
					continue;
				}

				totalChunksReceived++;
				totalSamplesReceived += dataChunk.RawAdcValues.Count;

				_logger.LogTrace(
						"Processing chunk #{Sequence}. Contains {SampleCount} samples.", 
						dataChunk.SequenceNumber, dataChunk.RawAdcValues.Count);

				// TODO: write to internal database or queue
			}

			// --- loop finished ---
			_logger.LogInformation(
					"Client has completed the stream for dataset '{DatasetId}'. Committing final data.", 
					metadata.DatasetId);

			// TODO: Commit the final transaction to the database. Close connection too perhaps

			return new ArchiveResponse
			{
				DatasetId = metadata.DatasetId,
					  Success = true,
					  ChunksReceived = totalChunksReceived,
					  SamplesReceived = totalSamplesReceived,
					  Message = "Dataset archived successfully."
			};
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "An error occurred while processing the archive stream for dataset '{DatasetId}'.", metadata.DatasetId);

			throw new RpcException(new Status(StatusCode.Internal, $"Failed to archive dataset: {ex.Message}"));
		}
	}
}
