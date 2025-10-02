using System;
using System.Threading;
using System.Threading.Tasks;
using ApiGateway.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ApiGateway.Data
{
    public class ValveData
    {
        public static void ConfigureValveEntities(ModelBuilder builder)
        {
            // valve configs
            builder.Entity<Valve>()
            .HasOne(v => v.Atv)
            .WithOne()
            .HasForeignKey<Valve>(d => d.AtvId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
            
            builder.Entity<Valve>()
            .HasOne(v => v.Remote)
            .WithOne()
            .HasForeignKey<Valve>(d => d.RemoteId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Valve>()
            .HasMany(v => v.Configurations)
            .WithOne()
            .HasForeignKey(l => l.ValveId)
            .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Valve>()
            .HasMany(v => v.Logs)
            .WithOne()
            .HasForeignKey(l => l.ValveId)
            .OnDelete(DeleteBehavior.Cascade);

            // dau configs
            builder.Entity<Dau>()
            .Property(d => d.DauIPAddress)
            .IsRequired();
            
            builder.Entity<ValveConfiguration>()
            .Property(c => c.ConfigurationType)
            .IsRequired();

            builder.Entity<ValveConfiguration>()
            .Property(c => c.ConfigurationValue)
            .IsRequired();

            builder.Entity<ValveLog>()
            .Property(l => l.Message)
            .IsRequired();
        }
    }
}