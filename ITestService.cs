using DataAccess;
using DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace ArchiverService.Services;

public interface ITestService
{
    Task<IResult> UploadTestAsync(int valveId, IFormFile file);
    Task<IResult> GetTestByIdAsync(int testId);
    Task<IResult> DownloadTestFileAsync(int testId);
    Task<IResult> GetTestsByValveAsync(int valveId); // New method
}

public class TestService : ITestService
{
    private readonly GeminiDbContext _dbContext;
    private readonly ILogger<TestService> _logger;

    public TestService(GeminiDbContext dbContext, ILogger<TestService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IResult> UploadTestAsync(int valveId, IFormFile file)
    {
        try
        {
            // Verify valve exists
            var valve = await _dbContext.Valves.FindAsync(valveId);
            if (valve == null)
            {
                return Results.NotFound($"Valve with ID {valveId} not found");
            }

            // Verify file exists and has content
            if (file == null || file.Length == 0)
            {
                return Results.BadRequest("No file was uploaded or file is empty");
            }

            // Check file size (MEDIUMBLOB can store up to 16MB)
            if (file.Length > 16 * 1024 * 1024)
            {
                return Results.BadRequest("File size exceeds the maximum allowed size (16MB)");
            }

            // Create new Test record
            var test = new Test
            {
                ValveId = valveId,
                DataAcquisitionDate = DateTime.UtcNow
            };

            // Add and save the Test to get its TestId
            _dbContext.Tests.Add(test);
            await _dbContext.SaveChangesAsync();

            // Read file into byte array
            byte[] fileData;
            using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);
                fileData = memoryStream.ToArray();
            }

            // Create TestBlob record
            var testBlob = new TestBlob
            {
                TestId = test.TestId,
                BlobData = fileData
            };

            // Add and save the TestBlob
            _dbContext.TestBlobs.Add(testBlob);
            await _dbContext.SaveChangesAsync();

            return Results.Created($"/api/tests/{test.TestId}", new 
            { 
                testId = test.TestId,
                valveId = test.ValveId,
                acquisitionDate = test.DataAcquisitionDate,
                fileSize = fileData.Length
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error uploading test for valve {valveId}");
            return Results.Problem($"Error uploading test: {ex.Message}");
        }
    }

    public async Task<IResult> GetTestByIdAsync(int testId)
    {
        try
        {
            var test = await _dbContext.Tests
                .Include(t => t.Valve)
                .FirstOrDefaultAsync(t => t.TestId == testId);

            if (test == null)
            {
                return Results.NotFound($"Test with ID {testId} not found");
            }

            var testBlob = await _dbContext.TestBlobs
                .AsNoTracking()
                .FirstOrDefaultAsync(tb => tb.TestId == testId);

            return Results.Ok(new
            {
                testId = test.TestId,
                valveId = test.ValveId,
                valveName = test.Valve?.Name,
                acquisitionDate = test.DataAcquisitionDate,
                hasBlobData = testBlob != null && testBlob.BlobData != null && testBlob.BlobData.Length > 0,
                blobSize = testBlob?.BlobData?.Length ?? 0
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error retrieving test {testId}");
            return Results.Problem($"Error retrieving test: {ex.Message}");
        }
    }

    public async Task<IResult> GetTestsByValveAsync(int valveId)
    {
        try
        {
            _logger.LogInformation($"Retrieving all tests for valve {valveId}");
            
            // Verify valve exists
            var valve = await _dbContext.Valves.FindAsync(valveId);
            if (valve == null)
            {
                return Results.NotFound($"Valve with ID {valveId} not found");
            }

            // Get all tests for this valve with their blob information
            var tests = await _dbContext.Tests
                .Where(t => t.ValveId == valveId)
                .OrderByDescending(t => t.DataAcquisitionDate) // Newest first
                .Select(t => new
                {
                    testId = t.TestId,
                    valveId = t.ValveId,
                    acquisitionDate = t.DataAcquisitionDate,
                    hasBlobData = t.TestBlob != null && t.TestBlob.BlobData != null && t.TestBlob.BlobData.Length > 0,
                    blobSize = t.TestBlob != null ? t.TestBlob.BlobData.Length : 0,
                    blobSizeFormatted = t.TestBlob != null ? FormatBytes(t.TestBlob.BlobData.Length) : "0 B"
                })
                .ToListAsync();

            return Results.Ok(tests);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error retrieving tests for valve {valveId}");
            return Results.Problem($"Error retrieving tests: {ex.Message}");
        }
    }

    public async Task<IResult> DownloadTestFileAsync(int testId)
    {
        try
        {
            // Get the test to verify it exists and get valve information
            var test = await _dbContext.Tests
                .Include(t => t.Valve)
                .FirstOrDefaultAsync(t => t.TestId == testId);

            if (test == null)
            {
                return Results.NotFound($"Test with ID {testId} not found");
            }

            // Get the associated blob data
            var testBlob = await _dbContext.TestBlobs
                .AsNoTracking()
                .FirstOrDefaultAsync(tb => tb.TestId == testId);

            if (testBlob == null)
            {
                return Results.NotFound($"No blob data found for test {testId}");
            }

            if (testBlob.BlobData == null || testBlob.BlobData.Length == 0)
            {
                return Results.NotFound($"Blob data is empty for test {testId}");
            }

            // Create a descriptive filename
            string fileName = $"valve-{test.ValveId}-test-{test.TestId}.vitda";

            // Return the file
            return Results.File(
                fileContents: testBlob.BlobData,
                contentType: "application/octet-stream", 
                fileDownloadName: fileName
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error downloading file for test {testId}");
            return Results.Problem($"Error downloading file: {ex.Message}");
        }
    }
    
    // Helper method to format bytes to human-readable format
    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
