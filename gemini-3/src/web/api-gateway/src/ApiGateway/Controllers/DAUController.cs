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
    public class DauController : ControllerBase
    {
        private readonly IDauRepository _dauRepository;
        private readonly ILogger<DauController> _logger;
        public DauController(IDauRepository dauRepository, ILogger<DauController> logger)
        {
            _dauRepository = dauRepository;
            _logger = logger;
        }

        // GET: api/dau
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Dau>>> GetAllDaus()
        {
            try
            {
                _logger.LogInformation("Retrieving all Daus");
                var daus = await _dauRepository.GetAllDausAsync();
                return Ok(daus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Daus");
                return StatusCode(500, "Internal server error");
            }
        }
        
        // GET: api/dau/id
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
                return Ok(dau);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving valve");
                return StatusCode(500, "Internal server error");
            }
        }
        
        // POST: api/dau
        [HttpPost]
        public async Task<ActionResult<Dau>> CreateDau(Dau dau)
        {
            try
            {
                if (dau == null)
                {
                    return BadRequest("Dau data is null");
                }
                _logger.LogInformation("Creating new dau");
                await _dauRepository.AddDauAsync(dau);
                _logger.LogInformation("Valve created with id: {id}", dau.Id);
                return CreatedAtAction(nameof(GetDauById), new {id = dau.Id}, dau);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating valve");
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
                _logger.LogInformation($"Updating dau with id: {dauId}");

                {};
                var d = new Dau
                {
                    Id = dauId,
                    ValveId = dau.ValveId,
                    SerialNumber = dau.SerialNumber,
                    Location = dau.Location,
                    DauIPAddress = dau.DauIPAddress,
                    HeartBeatAlerts = dau.HeartBeatAlerts,
                    LastHeartbeat = dau.LastHeartbeat,
                    LastCalibration = dau.LastCalibration,
                    NextCalibration = dau.NextCalibration,
                    Status = dau.Status,
                    TcpPort = dau.TcpPort,
                    UdpPort = dau.UdpPort
                }; 
                await _dauRepository.UpdateDau(d);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating dau with id: {id}", dauId);
                return StatusCode(500, "Internal server error");
            }
        }

        // DELETE: api/dau/id
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteValve(int id)
        {
            try
            {
                var dau = await _dauRepository.GetDauByIdAsync(id);
                if (dau == null)
                {
                    _logger.LogWarning("DAU with id: {id} could not be deleted because the dau was not found.", id);
                    return NotFound();
                }
                _logger.LogInformation("Deleting DAU with id: {id}", id);
                await _dauRepository.DeleteDauAsync(id);
                _logger.LogInformation("DAU with id: {id} deleted", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting DAU with id: {id}", id);
                return StatusCode(500, "Internal server error");
            }
        }
    }
}