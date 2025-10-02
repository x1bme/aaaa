using DataAccess;
using DataAccess.Data;
using DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace ArchiverService.Services;

public interface IArchiveService
{
    Task<IResult> GetArchiveForValveAsync(int valveId);
}

public class ArchiveService : IArchiveService
{
    private readonly GeminiDbContext _dbContext;
    private readonly ILogger<ArchiveService> _logger;

    public ArchiveService(GeminiDbContext dbContext, ILogger<ArchiveService> logger, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IResult> GetArchiveForValveAsync(int valveId)
    {
        try
        {
            // Look up the valve
            var valve = await _dbContext.Valves.FindAsync(valveId);
            if (valve == null)
            {
                return Results.NotFound($"Valve with ID {valveId} not found");
            }

            // Find the latest test for this valve
            var latestTest = await _dbContext.Tests
                .Where(t => t.ValveId == valveId)
                .OrderByDescending(t => t.DataAcquisitionDate)
                .FirstOrDefaultAsync();

            if (latestTest == null)
            {
                return Results.NotFound($"No tests found for valve {valveId}");
            }

            // Get the TestBlob associated with the latest test
            var testBlob = await _dbContext.TestBlobs
                .AsNoTracking()
                .FirstOrDefaultAsync(tb => tb.TestId == latestTest.TestId);

            if (testBlob == null)
            {
                return Results.NotFound($"No blob data found for the latest test (TestId: {latestTest.TestId})");
            }

            if (testBlob.BlobData == null || testBlob.BlobData.Length == 0)
            {
                return Results.NotFound($"Blob data is empty for test {latestTest.TestId}");
            }

            // Generate a filename for the download
            string fileName = $"valve-{valveId}-test-{latestTest.TestId}.vitda";

            // Return the blob data as a file
            return Results.File(
                fileContents: testBlob.BlobData,
                contentType: "application/octet-stream", 
                fileDownloadName: fileName
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error retrieving blob data for valve {valveId}");
            return Results.Problem($"Error retrieving blob data: {ex.Message}");
        }
    }
}