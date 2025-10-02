using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using ApiGateway.Models;

namespace ApiGateway.Repositories
{
    public interface IValveRepository
    {
        Task<IEnumerable<Valve>> GetAllVavlesAsync();
        Task<Valve> GetValveByIdAsync(int id);
        Task<IEnumerable<ValveConfiguration>> GetAllConfigurationsAsync();
        Task<IEnumerable<ValveLog>> GetAllLogsAsync();
        Task<Valve> AddValveAsync(Valve valve);
        Task UpdateValveAsync(Valve valve);
        Task DeleteValveAsync(int id);
    }
}