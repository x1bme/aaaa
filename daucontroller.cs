using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using System.Collections.Generic;
using ApiGateway.Models;
using OpenIddict.Validation.AspNetCore;
using ApiGateway.Repositories;
using ApiGateway.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;

namespace ApiGateway.Controllers
{
    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    [ApiController]
    [Route("api/[controller]")]
    public class DauController : ControllerBase
    {
        private readonly IDauRepository _dauRepository;
        private readonly IGrpcClientService _grpcClient;
        private readonly ILogger<DauController> _logger;
        
        public DauController(
            IDauRepository dauRepository,
            IGrpcClientService grpcClient,
            ILogger<DauController> logger)
        {
            _dauRepository = dauRepository;
            _grpcClient = grpcClient;
            _logger = logger;
        }

        // GET: api/dau - Get all registered DAUs (with live data from gRPC)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Dau>>> GetAllDaus()
        {
            try
            {
                _logger.LogInformation("Retrieving all registered DAUs from database");
                var daus = (await _dauRepository.GetAllDausAsync()).ToList();
                
                // Enrich with live data from gRPC server
                await EnrichDausWithServerData(daus);
                
                return Ok(daus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving DAUs");
                return StatusCode(500, "Internal server error");
            }
        }
        
        // GET: api/dau/server - Get all DAUs from the DAU Server via gRPC
        [HttpGet("server")]
        public async Task<ActionResult> GetAllDausFromServer()
        {
            try
            {
                _logger.LogInformation("Retrieving all DAUs from DAU Server via gRPC");
                
                // Get all DAUs from server (pass empty array to get all)
                var result = await _grpcClient.GetAllDausAsync(new string[] {});
                
                // Transform gRPC response to simplified DTO
                var daus = result.Dau.Select(d => new
                {
                    dauId = d.DeviceId,
                    status = MapGrpcStatusToDauStatus(d.IsOperational),
                    ipAddress = d.StaticIpAddress,
                    lastHeartbeat = DateTimeOffset.FromUnixTimeSeconds((long)d.LastHeartbeat).DateTime,
                    // TODO: Add these when available in gRPC
                    // lastCalibration = DateTimeOffset.FromUnixTimeSeconds((long)d.LastCalibration).DateTime,
                    // nextCalibration = DateTimeOffset.FromUnixTimeSeconds((long)d.NextCalibration).DateTime
                });
                
                return Ok(daus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving DAUs from server");
                return StatusCode(500, new { error = "Failed to retrieve DAUs from server", details = ex.Message });
            }
        }
        
        // GET: api/dau/unregistered - Get DAUs that exist on server but not in database
        [HttpGet("unregistered")]
        public async Task<ActionResult> GetUnregisteredDaus()
        {
            try
            {
                _logger.LogInformation("Retrieving unregistered DAUs");
                
                // Get DAUs from server
                var serverDaus = await _grpcClient.GetAllDausAsync(new string[] {});
                
                // Get registered DAUs from database
                var registeredDaus = await _dauRepository.GetAllDausAsync();
                var registeredDauIds = registeredDaus
                    .Where(d => d.Registered)
                    .Select(d => d.DauId)
                    .ToHashSet();
                
                // Filter for unregistered DAUs
                var unregisteredDaus = serverDaus.Dau
                    .Where(d => !registeredDauIds.Contains(d.DeviceId))
                    .Select(d => new
                    {
                        dauId = d.DeviceId,
                        status = MapGrpcStatusToDauStatus(d.IsOperational),
                        ipAddress = d.StaticIpAddress,
                        lastHeartbeat = DateTimeOffset.FromUnixTimeSeconds((long)d.LastHeartbeat).DateTime
                    });
                
                return Ok(unregisteredDaus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving unregistered DAUs");
                return StatusCode(500, new { error = "Failed to retrieve unregistered DAUs", details = ex.Message });
            }
        }
        
        // GET: api/dau/id - Get single DAU with live data
        [HttpGet("{id}")]
        public async Task<ActionResult<Dau>> GetDauById(int id)
        {
            try
            {
                _logger.LogInformation("Retrieving DAU with id: {id}", id);
                var dau = await _dauRepository.GetDauByIdAsync(id);
                if (dau == null)
                {
                    _logger.LogWarning("DAU with id: {id} not found", id);
                    return NotFound();
                }
                
                // Enrich with live data from gRPC server
                await EnrichDauWithServerData(dau);
                
                return Ok(dau);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving DAU");
                return StatusCode(500, "Internal server error");
            }
        }
        
        // POST: api/dau - Register a new DAU
        [HttpPost]
        public async Task<ActionResult<Dau>> CreateDau(Dau dau)
        {
            try
            {
                if (dau == null)
                {
                    return BadRequest("DAU data is null");
                }
                
                if (string.IsNullOrEmpty(dau.DauId))
                {
                    return BadRequest("DauId is required");
                }
                
                // Check if DAU already registered
                var existingDaus = await _dauRepository.GetAllDausAsync();
                if (existingDaus.Any(d => d.DauId == dau.DauId && d.Registered))
                {
                    return BadRequest("DAU is already registered");
                }
                
                // Verify DAU exists on server
                var serverDaus = await _grpcClient.GetAllDausAsync(new string[] { dau.DauId });
                var serverDau = serverDaus.Dau.FirstOrDefault(d => d.DeviceId == dau.DauId);
                
                if (serverDau == null)
                {
                    return BadRequest($"DAU with ID {dau.DauId} not found on DAU Server");
                }
                
                _logger.LogInformation("Registering new DAU with DauId: {dauId}", dau.DauId);
                
                // Set registered flag to true
                dau.Registered = true;
                
                // Only store user-provided fields in database
                // LastHeartbeat, Status, and Calibration data will be fetched from gRPC
                await _dauRepository.AddDauAsync(dau);
                
                // Enrich the response with live server data
                await EnrichDauWithServerData(dau);
                
                _logger.LogInformation("DAU registered with id: {id}", dau.Id);
                
                return CreatedAtAction(nameof(GetDauById), new {id = dau.Id}, dau);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering DAU");
                return StatusCode(500, "Internal server error");
            }
        }
        
        [HttpPut]
        public async Task<IActionResult> UpdateDau(int dauId, Dau dau)
        {
            try
            {
                if (dau == null)
                {
                    return BadRequest("DAU data is null");
                }
                if (dau.Id != dauId)
                {
                    return BadRequest("DAU ID mismatch between URL and body");
                }
                
                _logger.LogInformation($"Updating DAU with id: {dauId}");
                
                // Verify DAU still exists on server
                var serverDaus = await _grpcClient.GetAllDausAsync(new string[] { dau.DauId });
                var serverDau = serverDaus.Dau.FirstOrDefault(d => d.DeviceId == dau.DauId);
                
                if (serverDau == null)
                {
                    return BadRequest($"DAU with ID {dau.DauId} not found on DAU Server");
                }

                // Only update fields stored in database
                var updatedDau = new Dau
                {
                    Id = dauId,
                    ValveId = dau.ValveId,
                    DauId = dau.DauId,
                    DauTag = dau.DauTag,
                    Location = dau.Location,
                    DauIPAddress = dau.DauIPAddress,
                    Registered = dau.Registered
                }; 
                
                await _dauRepository.UpdateDau(updatedDau);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating DAU with id: {id}", dauId);
                return StatusCode(500, "Internal server error");
            }
        }

        // DELETE: api/dau/id - Unregister a DAU
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDau(int id)
        {
            try
            {
                var dau = await _dauRepository.GetDauByIdAsync(id);
                if (dau == null)
                {
                    _logger.LogWarning("DAU with id: {id} could not be deleted because the DAU was not found.", id);
                    return NotFound();
                }
                
                _logger.LogInformation("Unregistering DAU with id: {id}", id);
                await _dauRepository.DeleteDauAsync(id);
                _logger.LogInformation("DAU with id: {id} unregistered", id);
                
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unregistering DAU with id: {id}", id);
                return StatusCode(500, "Internal server error");
            }
        }
        
        // Helper method to enrich a single DAU with server data
        private async Task EnrichDauWithServerData(Dau dau)
        {
            try
            {
                var serverDaus = await _grpcClient.GetAllDausAsync(new string[] { dau.DauId });
                var serverDau = serverDaus.Dau.FirstOrDefault(d => d.DeviceId == dau.DauId);
                
                if (serverDau != null)
                {
                    dau.LastHeartbeat = DateTimeOffset.FromUnixTimeSeconds((long)serverDau.LastHeartbeat).DateTime;
                    dau.Status = MapGrpcStatusToDauStatus(serverDau.IsOperational);
                    
                    // TODO: Add these when available in gRPC proto
                    // dau.LastCalibration = DateTimeOffset.FromUnixTimeSeconds((long)serverDau.LastCalibration).DateTime;
                    // dau.NextCalibration = DateTimeOffset.FromUnixTimeSeconds((long)serverDau.NextCalibration).DateTime;
                    
                    // For now, use mock data for calibration
                    dau.LastCalibration = DateTime.UtcNow.AddDays(-30);
                    dau.NextCalibration = DateTime.UtcNow.AddDays(60);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enrich DAU {dauId} with server data", dau.DauId);
                // Don't throw - just leave the fields null/default
            }
        }
        
        // Helper method to enrich multiple DAUs with server data
        private async Task EnrichDausWithServerData(List<Dau> daus)
        {
            try
            {
                // Get all DAU IDs
                var dauIds = daus.Select(d => d.DauId).ToArray();
                
                // Fetch all at once from server
                var serverDaus = await _grpcClient.GetAllDausAsync(dauIds);
                
                // Create a lookup dictionary
                var serverDauLookup = serverDaus.Dau.ToDictionary(d => d.DeviceId);
                
                // Enrich each DAU
                foreach (var dau in daus)
                {
                    if (serverDauLookup.TryGetValue(dau.DauId, out var serverDau))
                    {
                        dau.LastHeartbeat = DateTimeOffset.FromUnixTimeSeconds((long)serverDau.LastHeartbeat).DateTime;
                        dau.Status = MapGrpcStatusToDauStatus(serverDau.IsOperational);
                        
						// TODO: Add these when available in gRPC proto
                        // dau.LastCalibration = DateTimeOffset.FromUnixTimeSeconds((long)serverDau.LastCalibration).DateTime;
                        // dau.NextCalibration = DateTimeOffset.FromUnixTimeSeconds((long)serverDau.NextCalibration).DateTime;
                        
                        // For now, use mock data for calibration
                        dau.LastCalibration = DateTime.UtcNow.AddDays(-30);
                        dau.NextCalibration = DateTime.UtcNow.AddDays(60);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enrich DAUs with server data");
                // Don't throw - just leave the fields null/default
            }
        }
        
        // Helper method to map gRPC operational status to DauStatus enum
        private DauStatus MapGrpcStatusToDauStatus(bool isOperational)
        {
            // This is a simple mapping - you might want to get more detailed status from gRPC
            return isOperational ? DauStatus.Operational : DauStatus.Offline;
        }
    }
}
