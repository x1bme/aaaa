using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ApiGateway.Models;
using Microsoft.AspNetCore.Identity;

namespace ApiGateway.Data
{
    public class GeminiDbContext : IdentityDbContext<User>
    {
        public GeminiDbContext(DbContextOptions<GeminiDbContext> options)
            : base(options)
        {
        }
        public DbSet<Valve> Valves {get; set;}
        public DbSet<Dau> Daus {get; set;}
        public DbSet<ValveConfiguration> ValveConfigurations {get; set;}
        public DbSet<ValveLog> ValveLogs {get; set;}

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            ValveData.ConfigureValveEntities(builder);
        }
    }
}