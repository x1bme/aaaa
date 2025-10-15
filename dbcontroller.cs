using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using OpenIddict.Validation.AspNetCore;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using Microsoft.Extensions.Configuration;

namespace ApiGateway.Controllers
{
    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    [ApiController]
    [Route("api/[controller]")]
    public class DatabaseController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DatabaseController> _logger;
        private readonly string _archiverServiceUrl;

        public DatabaseController(
            IHttpClientFactory httpClientFactory, 
            ILogger<DatabaseController> logger,
            IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            // Use the ArchiverService URL from configuration
            _archiverServiceUrl = configuration["ArchiverService:Url"] ?? "http://localhost:6001";
        }

        // GET: api/database/status
        // Maps to: GET / on ArchiverService
        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            try
            {
                _logger.LogInformation("Checking archiver service status");
                
                var client = _httpClientFactory.CreateClient();
                var response = await client.GetAsync($"{_archiverServiceUrl}/");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return Ok(new { status = "connected", message = content });
                }
                
                return StatusCode((int)response.StatusCode, "Archiver service unavailable");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting to archiver service");
                return StatusCode(500, new { error = "Failed to connect to archiver service", details = ex.Message });
            }
        }

        // GET: api/database/archives/{valveId}
        // Maps to: GET /archives/{valveId} on ArchiverService
        [HttpGet("archives/{valveId}")]
        public async Task<IActionResult> GetValveArchive(int valveId)
        {
            try
            {
                _logger.LogInformation("Retrieving archive for valve {valveId}", valveId);
                
                var client = _httpClientFactory.CreateClient();
                var response = await client.GetAsync($"{_archiverServiceUrl}/archives/{valveId}");
                
                if (response.IsSuccessStatusCode)
                {
                    var fileBytes = await response.Content.ReadAsByteArrayAsync();
                    var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                    
                    // Get filename from Content-Disposition header if available
                    var contentDisposition = response.Content.Headers.ContentDisposition;
                    var fileName = contentDisposition?.FileName?.Trim('"') ?? $"valve-{valveId}-archive.vitda";
                    
                    return File(fileBytes, contentType, fileName);
                }
                
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return NotFound($"No archive found for valve {valveId}");
                }
                
                return StatusCode((int)response.StatusCode, "Failed to retrieve archive");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving valve archive for valve {valveId}", valveId);
                return StatusCode(500, new { error = "Failed to retrieve valve archive", details = ex.Message });
            }
        }

        // GET: api/database/tests/{testId}
        // Maps to: GET /api/tests/{testId} on ArchiverService
        [HttpGet("tests/{testId}")]
        public async Task<IActionResult> GetTestById(int testId)
        {
            try
            {
                _logger.LogInformation("Retrieving test {testId}", testId);
                
                var client = _httpClientFactory.CreateClient();
                var response = await client.GetAsync($"{_archiverServiceUrl}/api/tests/{testId}");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return Content(content, "application/json");
                }
                
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return NotFound($"Test {testId} not found");
                }
                
                return StatusCode((int)response.StatusCode, "Failed to retrieve test");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving test {testId}", testId);
                return StatusCode(500, new { error = "Failed to retrieve test", details = ex.Message });
            }
        }

        // GET: api/database/tests/{testId}/download
        // Maps to: GET /api/tests/{testId}/download on ArchiverService
        [HttpGet("tests/{testId}/download")]
        public async Task<IActionResult> DownloadTest(int testId)
        {
            try
            {
                _logger.LogInformation("Downloading test {testId}", testId);
                
                var client = _httpClientFactory.CreateClient();
                var response = await client.GetAsync($"{_archiverServiceUrl}/api/tests/{testId}/download");
                
                if (response.IsSuccessStatusCode)
                {
                    var fileBytes = await response.Content.ReadAsByteArrayAsync();
                    var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                    
                    // Get filename from Content-Disposition header if available
                    var contentDisposition = response.Content.Headers.ContentDisposition;
                    var fileName = contentDisposition?.FileName?.Trim('"') ?? $"test-{testId}.vitda";
                    
                    return File(fileBytes, contentType, fileName);
                }
                
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return NotFound($"Test {testId} not found");
                }
                
                return StatusCode((int)response.StatusCode, "Failed to download test");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading test {testId}", testId);
                return StatusCode(500, new { error = "Failed to download test", details = ex.Message });
            }
        }

        // POST: api/database/tests
        // Maps to: POST /api/tests on ArchiverService
        [HttpPost("tests")]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> UploadTest([FromQuery] int valveId, IFormFile file)
        {
            try
            {
                _logger.LogInformation("Uploading test for valve {valveId}", valveId);
                
                if (file == null || file.Length == 0)
                {
                    return BadRequest("No file was uploaded or file is empty");
                }

                var client = _httpClientFactory.CreateClient();
                
                using var content = new MultipartFormDataContent();
                using var fileStream = file.OpenReadStream();
                using var streamContent = new StreamContent(fileStream);
                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
                content.Add(streamContent, "file", file.FileName);
                
                var response = await client.PostAsync($"{_archiverServiceUrl}/api/tests?valveId={valveId}", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return Content(responseContent, "application/json");
                }
                
                return StatusCode((int)response.StatusCode, "Failed to upload test");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading test for valve {valveId}", valveId);
                return StatusCode(500, new { error = "Failed to upload test", details = ex.Message });
            }
        }

        // GET: api/database/backup
        // Maps to: GET /api/database/backup on ArchiverService
        [HttpGet("backup")]
        public async Task<IActionResult> GetDatabaseBackup()
        {
            try
            {
                _logger.LogInformation("Retrieving database backup");
                
                var client = _httpClientFactory.CreateClient();
                var response = await client.GetAsync($"{_archiverServiceUrl}/api/database/backup");
                
                if (response.IsSuccessStatusCode)
                {
                    var fileBytes = await response.Content.ReadAsByteArrayAsync();
                    var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/sql";
                    var fileName = "database_backup.sql";
                    
                    return File(fileBytes, contentType, fileName);
                }
                
                return StatusCode((int)response.StatusCode, "Failed to retrieve database backup");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving database backup");
                return StatusCode(500, new { error = "Failed to retrieve database backup", details = ex.Message });
            }
        }
    }
}
