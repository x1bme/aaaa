using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using ApiGateway.Models;

namespace ApiGateway.Repositories
{
    public interface IDauRepository
    {
        Task<IEnumerable<Dau>> GetAllDausAsync();
        Task<Dau> AddDauAsync(Dau dau);
        Task DeleteDauAsync(int id);
        Task<Dau> GetDauByIdAsync(int id);
        Task UpdateDau(Dau dau);
        Task SetDauValveAsync(int valveId, int dauId);
    }
}