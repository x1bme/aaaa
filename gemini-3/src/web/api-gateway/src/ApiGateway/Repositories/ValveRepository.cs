using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApiGateway.Data;
using ApiGateway.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ApiGateway.Repositories
{
    public class ValveRepository : IValveRepository
    {
        private readonly GeminiDbContext _context;
        private readonly ILogger<ValveRepository> _logger;

        public ValveRepository(GeminiDbContext context, ILogger<ValveRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Valve> AddValveAsync(Valve valve)
        {
            _context.Valves.Add(valve);
            await _context.SaveChangesAsync();
            return valve;
        }

        public async Task DeleteValveAsync(int id)
        {
            var valve = await _context.Valves.FindAsync(id);
            if (valve != null)
            {
                _context.Valves.Remove(valve);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<ValveConfiguration>> GetAllConfigurationsAsync()
        {
            return await _context.ValveConfigurations.ToListAsync();
        }

        public async Task<IEnumerable<ValveLog>> GetAllLogsAsync()
        {
            return await _context.ValveLogs.ToListAsync();
        }

        public async Task<IEnumerable<Valve>> GetAllVavlesAsync()
        {
            return await _context.Valves
            .Include(v => v.Atv)
            .Include(v => v.Remote)
            .Include(v => v.Configurations)
            .Include(v => v.Logs)
            .ToListAsync();
        }

        public async Task<Valve> GetValveByIdAsync(int id)
        {
            return await _context.Valves
            .Include(v => v.Atv)
            .Include(v => v.Remote)
            .Include(v => v.Configurations)
            .Include(v => v.Logs)
            .FirstOrDefaultAsync(v => v.Id == id);
        }

        public async Task UpdateValveAsync(Valve valve)
        {
            var existingValve = await _context.Valves
            .Include(v => v.Configurations)
            .Include(v => v.Logs)
            .FirstOrDefaultAsync(v => v.Id == valve.Id);

            if (existingValve == null)
            {
                throw new KeyNotFoundException($"Value with ID {valve.Id} not found");
            }

            existingValve.Name = valve.Name;
            existingValve.Location = valve.Location;
            existingValve.InstallationDate = valve.InstallationDate;
            existingValve.IsActive = valve.IsActive;
            existingValve.Atv = valve.Atv;
            existingValve.Remote = valve.Remote;
            existingValve.AtvId = valve.AtvId;
            existingValve.RemoteId = valve.RemoteId;

            existingValve.Logs.Add(new ValveLog
            {
                LogType = LogType.Info,
                Message = "Valve updated",
                TimeStamp = DateTime.UtcNow
            });

            if (valve.Configurations != null && valve.Configurations.Count != 0)
            {
                var existingConfigs = existingValve.Configurations.ToList();
                foreach (var config in valve.Configurations.Where(c => c.Id > 0))
                {
                    var existingConfig = existingConfigs.FirstOrDefault(c => c.Id == config.Id);
                    if (existingConfig != null)
                    {
                        existingConfig.ConfigurationType = config.ConfigurationType;
                        existingConfig.ConfigurationValue = config.ConfigurationValue;
                        existingConfig.UpdatedAt = DateTime.UtcNow;
                    }
                }
                var newConfigs = valve.Configurations
                .Where(c => c.Id <= 0)
                .Select(c => new ValveConfiguration
                {
                    ValveId = existingValve.Id,
                    ConfigurationType = c.ConfigurationType,
                    ConfigurationValue = c.ConfigurationValue,
                    CreatedAt = DateTime.UtcNow
                }).ToList();
                foreach (var newConfig in newConfigs)
                {
                    _context.ValveConfigurations.Add(newConfig);
                }
            }
            await _context.SaveChangesAsync();
        }
    }
}