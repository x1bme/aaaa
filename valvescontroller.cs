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
    public class ValvesController : ControllerBase
    {
        private readonly IValveRepository _valveRepository;
        private readonly IDauRepository _dauRepository;
        private readonly IGrpcClientService _grpcClient;
        private readonly ILogger<ValvesController> _logger;
        
        public ValvesController(
            IValveRepository valveRepository, 
            IDauRepository dauRepository,
            IGrpcClientService grpcClient,
            ILogger<ValvesController> logger)
        {
            _dauRepository = dauRepository;
            _valveRepository = valveRepository;
            _grpcClient = grpcClient;
            _logger = logger;
        }

        // GET: api/valves
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Valve>>> GetAllValves()
        {
            try
            {
                _logger.LogInformation("Retrieving all valves");
                var valves = await _valveRepository.GetAllVavlesAsync();
                
                // Enrich all DAUs with live gRPC data
                foreach (var valve in valves)
                {
                    if (valve.Atv != null)
                    {
                        await EnrichDauWithServerData(valve.Atv);
                    }
                    if (valve.Remote != null)
                    {
                        await EnrichDauWithServerData(valve.Remote);
                    }
                }
                
                return Ok(valves);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving valves");
                return StatusCode(500, "Internal server error");
            }
        }
        
        // GET: api/valves/config
        [HttpGet("configurations")]
        public async Task<ActionResult<IEnumerable<ValveConfiguration>>> GetAllValveConfigurations()
        {
            try
            {
                _logger.LogInformation("Retrieving all valve configurations");
                var configs = await _valveRepository.GetAllConfigurationsAsync();
                return Ok(configs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving valve configurations");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/valves/logs
        [HttpGet("logs")]
        public async Task<ActionResult<IEnumerable<ValveLog>>> GetValveLogs()
        {
            try
            {
                _logger.LogInformation("Retrieving all valve logs");
                var logs = await _valveRepository.GetAllLogsAsync();
                return Ok(logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving valve logs");
                return StatusCode(500, "Internal server error");
            }
        }
       
        // GET: api/valves/id
        [HttpGet("{id}")]
        public async Task<ActionResult<Valve>> GetValveById(int id)
        {
            try
            {
                _logger.LogInformation("Retrieving valve with id: {id}", id);
                var valve = await _valveRepository.GetValveByIdAsync(id);
                if (valve == null)
                {
                    _logger.LogWarning("Valve with id: {id} not found", id);
                    return NotFound();
                }
                
                // Enrich DAUs with live gRPC data
                if (valve.Atv != null)
                {
                    await EnrichDauWithServerData(valve.Atv);
                }
                if (valve.Remote != null)
                {
                    await EnrichDauWithServerData(valve.Remote);
                }
                
                return Ok(valve);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving valve");
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/valves
        [HttpPost]
        public async Task<ActionResult<Valve>> CreateValve(Valve valve)
        {
            try
            {
                if (valve == null)
                {
                    return BadRequest("Valve data is null");
                }

                // Validate that both DAU IDs are provided
                if (!valve.AtvId.HasValue || !valve.RemoteId.HasValue)
                {
                    return BadRequest("Both ATV and Remote DAU IDs are required");
                }

                if (valve.AtvId == valve.RemoteId)
                {
                    return BadRequest("ATV and Remote cannot be the same DAU");
                }

                _logger.LogInformation("Creating new valve");

                // Fetch the DAUs to ensure they exist and are available
                var atvDau = await _dauRepository.GetDauByIdAsync(valve.AtvId.Value);
                var remoteDau = await _dauRepository.GetDauByIdAsync(valve.RemoteId.Value);

                if (atvDau == null)
                {
                    return BadRequest($"ATV DAU with ID {valve.AtvId.Value} not found");
                }

                if (remoteDau == null)
                {
                    return BadRequest($"Remote DAU with ID {valve.RemoteId.Value} not found");
                }

                // SINGLE SOURCE OF TRUTH CHECK: Ensure DAUs are not already attached
                if (atvDau.ValveId.HasValue && atvDau.ValveId.Value != 0)
                {
                    return BadRequest($"ATV DAU {atvDau.DauTag ?? atvDau.DauId} is already attached to valve {atvDau.ValveId.Value}");
                }

                if (remoteDau.ValveId.HasValue && remoteDau.ValveId.Value != 0)
                {
                    return BadRequest($"Remote DAU {remoteDau.DauTag ?? remoteDau.DauId} is already attached to valve {remoteDau.ValveId.Value}");
                }

                // Set valve properties
                valve.Atv = atvDau;
                valve.Remote = remoteDau;
                valve.InstallationDate = valve.InstallationDate != default ? valve.InstallationDate : DateTime.UtcNow;
                valve.IsActive = true;
                valve.Configurations ??= new List<ValveConfiguration>();
                valve.Logs ??= new List<ValveLog>();
                
                valve.Logs.Add(new ValveLog
                {
                    LogType = LogType.Info,
                    Message = $"Valve created with ATV: {atvDau.DauTag ?? atvDau.DauId}, Remote: {remoteDau.DauTag ?? remoteDau.DauId}",
                    TimeStamp = DateTime.UtcNow
                });

                // Create the valve first
                await _valveRepository.AddValveAsync(valve);

                // SINGLE SOURCE OF TRUTH: Update DAU.ValveId (this is the authoritative link)
                await _dauRepository.SetDauValveAsync(valve.Id, atvDau.Id);
                await _dauRepository.SetDauValveAsync(valve.Id, remoteDau.Id);

                // IMPORTANT: Enrich the DAUs with live gRPC data before returning
                await EnrichDauWithServerData(valve.Atv);
                await EnrichDauWithServerData(valve.Remote);

                _logger.LogInformation("Valve created with id: {id}", valve.Id);
                return CreatedAtAction(nameof(GetValveById), new { id = valve.Id }, valve);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating valve");
                return StatusCode(500, "Internal server error");
            }
        }

        // PUT: api/valves/id
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateValveConfig(int id, Valve valve)
        {
            try
            {
                if (valve == null)
                {
                    return BadRequest("Valve data is null");
                }
                
                if (valve.Id != id && valve.Id != 0)
                {
                    return BadRequest("ID mismatch between URL and body");
                }

                _logger.LogInformation($"Updating valve with id: {id}");

                var existingValve = await _valveRepository.GetValveByIdAsync(id);
                if (existingValve == null)
                {
                    return NotFound($"Valve with id {id} not found");
                }

                // Validate that the new DAUs are not the same
                if (valve.AtvId.HasValue && valve.RemoteId.HasValue && valve.AtvId == valve.RemoteId)
                {
                    return BadRequest("ATV and Remote cannot be the same DAU");
                }

                // Handle ATV changes
                if (valve.AtvId != existingValve.AtvId)
                {
                    // Detach old ATV if exists
                    if (existingValve.AtvId.HasValue && existingValve.AtvId.Value != 0)
                    {
                        _logger.LogInformation($"Detaching old ATV (ID: {existingValve.AtvId.Value}) from valve {id}");
                        await _dauRepository.SetDauValveAsync(0, existingValve.AtvId.Value);
                    }

                    // Attach new ATV if provided
                    if (valve.AtvId.HasValue && valve.AtvId.Value != 0)
                    {
                        var newAtvDau = await _dauRepository.GetDauByIdAsync(valve.AtvId.Value);
                        if (newAtvDau == null)
                        {
                            return BadRequest($"ATV DAU with ID {valve.AtvId.Value} not found");
                        }

                        // Check if new DAU is available
                        if (newAtvDau.ValveId.HasValue && newAtvDau.ValveId.Value != 0)
                        {
                            return BadRequest($"ATV DAU {newAtvDau.DauTag ?? newAtvDau.DauId} is already attached to valve {newAtvDau.ValveId.Value}");
                        }

                        _logger.LogInformation($"Attaching new ATV (ID: {valve.AtvId.Value}) to valve {id}");
                        await _dauRepository.SetDauValveAsync(id, valve.AtvId.Value);
                        valve.Atv = newAtvDau;
                        
                        // Enrich with live data
                        await EnrichDauWithServerData(valve.Atv);
                    }
                    else
                    {
                        valve.Atv = null;
                    }
                }
                else
                {
                    // Keep existing ATV and enrich it
                    valve.Atv = existingValve.Atv;
                    if (valve.Atv != null)
                    {
                        await EnrichDauWithServerData(valve.Atv);
                    }
                }

                // Handle Remote changes
                if (valve.RemoteId != existingValve.RemoteId)
                {
                    // Detach old Remote if exists
                    if (existingValve.RemoteId.HasValue && existingValve.RemoteId.Value != 0)
                    {
                        _logger.LogInformation($"Detaching old Remote (ID: {existingValve.RemoteId.Value}) from valve {id}");
                        await _dauRepository.SetDauValveAsync(0, existingValve.RemoteId.Value);
                    }

                    // Attach new Remote if provided
                    if (valve.RemoteId.HasValue && valve.RemoteId.Value != 0)
                    {
                        var newRemoteDau = await _dauRepository.GetDauByIdAsync(valve.RemoteId.Value);
                        if (newRemoteDau == null)
                        {
                            return BadRequest($"Remote DAU with ID {valve.RemoteId.Value} not found");
                        }

                        // Check if new DAU is available
                        if (newRemoteDau.ValveId.HasValue && newRemoteDau.ValveId.Value != 0)
                        {
                            return BadRequest($"Remote DAU {newRemoteDau.DauTag ?? newRemoteDau.DauId} is already attached to valve {newRemoteDau.ValveId.Value}");
                        }

                        _logger.LogInformation($"Attaching new Remote (ID: {valve.RemoteId.Value}) to valve {id}");
                        await _dauRepository.SetDauValveAsync(id, valve.RemoteId.Value);
                        valve.Remote = newRemoteDau;
                        
                        // Enrich with live data
                        await EnrichDauWithServerData(valve.Remote);
                    }
                    else
                    {
                        valve.Remote = null;
                    }
                }
                else
                {
                    // Keep existing Remote and enrich it
                    valve.Remote = existingValve.Remote;
                    if (valve.Remote != null)
                    {
                        await EnrichDauWithServerData(valve.Remote);
                    }
                }

                // Update the valve
                var updatedValve = new Valve
                {
                    Id = id,
                    Name = valve.Name,
                    Location = valve.Location,
                    InstallationDate = valve.InstallationDate,
                    IsActive = valve.IsActive,
                    AtvId = valve.AtvId,
                    RemoteId = valve.RemoteId,
                    Atv = valve.Atv,
                    Remote = valve.Remote,
                    Configurations = valve.Configurations ?? existingValve.Configurations,
                    Logs = valve.Logs ?? existingValve.Logs
                };

                await _valveRepository.UpdateValveAsync(updatedValve);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating valve with id: {id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // DELETE: api/valves/id
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteValve(int id)
        {
            try
            {
                var valve = await _valveRepository.GetValveByIdAsync(id);
                if (valve == null)
                {
                    _logger.LogWarning("Valve with id: {id} could not be deleted because the valve was not found.", id);
                    return NotFound();
                }

                _logger.LogInformation("Detaching DAUs from valve: {id}", id);
                
                // SINGLE SOURCE OF TRUTH: Clear DAU.ValveId
                if (valve.AtvId.HasValue && valve.AtvId.Value != 0)
                {
                    await _dauRepository.SetDauValveAsync(0, valve.AtvId.Value);
                }
                
                if (valve.RemoteId.HasValue && valve.RemoteId.Value != 0)
                {
                    await _dauRepository.SetDauValveAsync(0, valve.RemoteId.Value);
                }

                _logger.LogInformation("Deleting valve with id: {id}", id);
                await _valveRepository.DeleteValveAsync(id);
                _logger.LogInformation("Valve with id: {id} deleted", id);
                
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting valve with id: {id}", id);
                return StatusCode(500, "Internal server error");
            }
        }
        
        // Helper method to enrich a single DAU with server data
        private async Task EnrichDauWithServerData(Dau dau)
        {
            if (dau == null) return;
            
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
        
        // Helper method to map gRPC operational status to DauStatus enum
        private DauStatus MapGrpcStatusToDauStatus(bool isOperational)
        {
            return isOperational ? DauStatus.Operational : DauStatus.Offline;
        }
    }
}
