using Grpc.Core;
using ArchiverService.Grpc; // Changed from Archiver.Api.Grpc
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DataAccess; // Direct namespace for GeminiDbContext
using DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace ArchiverService.Services; // Changed from Archiver.Api.Services

public class ArchiverServiceImpl : Grpc.ArchiverService.ArchiverServiceBase // Fully qualified
{
    private readonly ILogger<ArchiverServiceImpl> _logger;
    private readonly GeminiDbContext _dbContext;

    public ArchiverServiceImpl(ILogger<ArchiverServiceImpl> logger, GeminiDbContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
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

        // Create a new dataset entry in the database
        var acquisitionDataset = new AcquisitionDataset
        {
            Id = metadata.DatasetId,
            DeviceId = metadata.DeviceId,
            SampleRateHz = metadata.SampleRateHz,
            NumChannels = metadata.NumChannels,
            StartTimeUtc = metadata.StartTimeUtc?.ToDateTime() ?? DateTime.UtcNow,
            Status = "Processing"
        };
        
        await _dbContext.AcquisitionDatasets.AddAsync(acquisitionDataset);
        await _dbContext.SaveChangesAsync();

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

                // Convert int32 array to binary data
                byte[] binaryData;
                using (var memoryStream = new System.IO.MemoryStream())
                {
                    using (var writer = new System.IO.BinaryWriter(memoryStream))
                    {
                        foreach (var value in dataChunk.RawAdcValues)
                        {
                            writer.Write(value);
                        }
                    }
                    binaryData = memoryStream.ToArray();
                }

                // Store the chunk as a blob
                var chunkBlob = new DataChunkBlob
                {
                    AcquisitionDatasetId = metadata.DatasetId,
                    SequenceNumber = dataChunk.SequenceNumber,
                    BinaryData = binaryData,
                    SampleCount = dataChunk.RawAdcValues.Count
                };
                
                await _dbContext.DataChunkBlobs.AddAsync(chunkBlob);
                await _dbContext.SaveChangesAsync();
            }

            // --- loop finished ---
            _logger.LogInformation(
                    "Client has completed the stream for dataset '{DatasetId}'. Committing final data.", 
                    metadata.DatasetId);

            // Update the dataset record with final stats
            acquisitionDataset.Status = "Completed";
            acquisitionDataset.TotalChunksReceived = totalChunksReceived;
            acquisitionDataset.TotalSamplesReceived = totalSamplesReceived;
            await _dbContext.SaveChangesAsync();

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

            // Mark the dataset as failed
            acquisitionDataset.Status = "Failed";
            await _dbContext.SaveChangesAsync();

            throw new RpcException(new Status(StatusCode.Internal, $"Failed to archive dataset: {ex.Message}"));
        }
    }
}
