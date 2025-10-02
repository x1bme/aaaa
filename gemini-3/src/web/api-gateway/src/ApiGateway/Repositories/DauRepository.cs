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
    public class DauRepository : IDauRepository
    {
        private readonly GeminiDbContext _context;
        private readonly ILogger<DauRepository> _logger;

        public DauRepository(GeminiDbContext context, ILogger<DauRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Dau> AddDauAsync(Dau dau)
        {
            if (dau == null) {
                throw new KeyNotFoundException($"Provided DAU does not meet required format");
            }
            _context.Daus.Add(dau);
            await _context.SaveChangesAsync();
            return dau;
        }
        public async Task<IEnumerable<Dau>> GetAllDausAsync()
        {
            return await _context.Daus
            .ToListAsync();
        }

        public async Task DeleteDauAsync(int id)
        {
            var dau = await _context.Daus.FindAsync(id);
            if (dau != null)
            {
                _context.Daus.Remove(dau);
                await _context.SaveChangesAsync();
            } else {
                throw new KeyNotFoundException($"DAU with ID {dau.Id} not found");
            }
        }

        public async Task UpdateDau(Dau dau) {
            var existingDau = await _context.Daus
            .FirstOrDefaultAsync(v => v.Id == dau.Id);
            if (existingDau == null)
            {
                throw new KeyNotFoundException($"One or more of the selected DAU(s) was not valid");
            }
            existingDau.ValveId = dau.ValveId;
            await _context.SaveChangesAsync();
        }
        public async Task SetDauValveAsync(int valveId, int dauId) {
            var dau = await _context.Daus.FindAsync(dauId);
            if (dau == null)
            {
                throw new KeyNotFoundException($"One or more of the selected DAU(s) was not valid");
            }
            dau.ValveId = valveId;
            await _context.SaveChangesAsync();
        }

        public async Task<Dau> GetDauByIdAsync(int id)
        {
            return await _context.Daus
            .FirstOrDefaultAsync(v => v.Id == id);
        }
    }
}