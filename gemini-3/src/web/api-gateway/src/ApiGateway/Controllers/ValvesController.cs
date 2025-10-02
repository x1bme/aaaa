using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using System.Collections.Generic;
using ApiGateway.Models;
using OpenIddict.Validation.AspNetCore;
using ApiGateway.Repositories;
using Microsoft.Extensions.Logging;
using System;

namespace ApiGateway.Controllers
{
    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    [ApiController]
    [Route("api/[controller]")]
    public class ValvesController : ControllerBase
    {
        private readonly IValveRepository _valveRepository;
        private readonly IDauRepository _dauRepository;
        private readonly ILogger<ValvesController> _logger;
        public ValvesController(IValveRepository valveRepository, IDauRepository dauRepository,ILogger<ValvesController> logger)
        {
            _dauRepository = dauRepository;
            _valveRepository = valveRepository;
            _logger = logger;
        }

        // GET: api/valves
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Valve>>> GetAllValves()
        {
            try
            {
                _logger.LogInformation("Retrieving all valves");
                var vavles = await _valveRepository.GetAllVavlesAsync();
                return Ok(vavles);
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
                _logger.LogInformation("Retreiving all valve configurations");
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
                _logger.LogInformation("Retreiving all valve logs");
                var configs = await _valveRepository.GetAllLogsAsync();
                return Ok(configs);
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

                if (valve.Atv == null || valve.Remote == null)
                {
                    if (valve.AtvId == 0 || valve.RemoteId == 0){
                        return BadRequest("Valve must have both ATV and Remote DAUs");
                    }
                }
                _logger.LogInformation("Creating new valve");
                valve.Atv = await _dauRepository.GetDauByIdAsync(valve.AtvId.Value);
                valve.Remote = await _dauRepository.GetDauByIdAsync(valve.RemoteId.Value);
                valve.InstallationDate = valve.InstallationDate != default ? valve.InstallationDate : DateTime.UtcNow;
                valve.IsActive = true;
                valve.Configurations ??= new List<ValveConfiguration>();
                valve.Logs ??= new List<ValveLog>();
                valve.Logs.Add(new ValveLog
                {
                    LogType = LogType.Info,
                    Message = "Valve created",
                    TimeStamp = DateTime.UtcNow
                });
                await _valveRepository.AddValveAsync(valve);
                await _dauRepository.SetDauValveAsync(valve.Id, valve.Atv.Id);
                await _dauRepository.SetDauValveAsync(valve.Id, valve.Remote.Id);
                // ADD VALVEID TO DAU LOGIC HERE


                _logger.LogInformation("Valve created with id: {id}", valve.Id);
                return CreatedAtAction(nameof(GetValveById), new {id = valve.Id}, valve);
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
                _logger.LogInformation($"Updating valve with id: {valve.Id}");

                var existingValve = await _valveRepository.GetValveByIdAsync(id);
                if (existingValve == null) {
                    return NotFound($"Valve with id {id} not found");
                }

                if (valve.AtvId == 0 || valve.AtvId == null) {
                    if (existingValve.AtvId.HasValue)
                    {
                        await _dauRepository.SetDauValveAsync(0, existingValve.AtvId.Value);
                    }
                    valve.AtvId = null;
                    valve.Atv = null;
                } else if (valve.AtvId != existingValve.AtvId)
                {
                    if (existingValve.AtvId.HasValue && existingValve.AtvId.Value != 0) {
                        await _dauRepository.SetDauValveAsync(0, existingValve.AtvId.Value);
                    }
                    if (valve.AtvId.HasValue)
                    {
                        await _dauRepository.SetDauValveAsync(id, valve.AtvId.Value);
                        valve.Atv = await _dauRepository.GetDauByIdAsync(valve.AtvId.Value);
                    }
                } else if (valve.AtvId == existingValve.AtvId) {
                    if (valve.AtvId.HasValue && valve.AtvId.Value != 0 && existingValve.AtvId.HasValue && existingValve.AtvId.Value != 0) {
                        valve.AtvId = existingValve.AtvId;
                        valve.Atv = existingValve.Atv;
                    }
                }

                if (valve.RemoteId == 0 || valve.RemoteId == null) {
                    if (existingValve.RemoteId.HasValue)
                    {
                        await _dauRepository.SetDauValveAsync(0, existingValve.RemoteId.Value);
                    }
                    valve.RemoteId = null;
                    valve.Remote = null;
                } else if (valve.RemoteId != existingValve.RemoteId)
                {
                    if (existingValve.RemoteId.HasValue && existingValve.RemoteId.Value != 0) {
                        await _dauRepository.SetDauValveAsync(0, existingValve.RemoteId.Value);
                    }
                    if (valve.RemoteId.HasValue)
                    {
                        await _dauRepository.SetDauValveAsync(id, valve.RemoteId.Value);
                        valve.Remote = await _dauRepository.GetDauByIdAsync(valve.RemoteId.Value);
                    }
                } else if (valve.RemoteId == existingValve.RemoteId) {
                    if (valve.RemoteId.HasValue && valve.RemoteId.Value != 0 && existingValve.RemoteId.HasValue && existingValve.RemoteId.Value != 0) {
                        valve.RemoteId = existingValve.RemoteId;
                        valve.Remote = existingValve.Remote;
                    }
                }

                var v = new Valve
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
                    Configurations = valve.Configurations,
                    Logs = valve.Logs
                }; 
                await _valveRepository.UpdateValveAsync(v);
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
                _logger.LogInformation("Detaching Atv and Remote from valve: {id}", id);
                await _dauRepository.SetDauValveAsync(0, valve.AtvId.Value);
                await _dauRepository.SetDauValveAsync(0, valve.RemoteId.Value);
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
    }
}