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
            // Valve to ATV DAU relationship
            builder.Entity<Valve>()
                .HasOne(v => v.Atv)
                .WithMany() // One DAU can be referenced by many valves (though logically only one at a time)
                .HasForeignKey(v => v.AtvId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict); // Prevent cascade delete

            // Valve to Remote DAU relationship
            builder.Entity<Valve>()
                .HasOne(v => v.Remote)
                .WithMany()
                .HasForeignKey(v => v.RemoteId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict); // Prevent cascade delete

            // Valve configurations relationship
            builder.Entity<Valve>()
                .HasMany(v => v.Configurations)
                .WithOne()
                .HasForeignKey(l => l.ValveId)
                .OnDelete(DeleteBehavior.Cascade); // Delete configs when valve is deleted

            // Valve logs relationship
            builder.Entity<Valve>()
                .HasMany(v => v.Logs)
                .WithOne()
                .HasForeignKey(l => l.ValveId)
                .OnDelete(DeleteBehavior.Cascade); // Delete logs when valve is deleted

            // DAU configuration
            builder.Entity<Dau>()
                .Property(d => d.DauIPAddress)
                .IsRequired();

            // Add index on ValveId in DAU table for faster lookups
            builder.Entity<Dau>()
                .HasIndex(d => d.ValveId)
                .IsUnique(false); // Multiple DAUs can have ValveId = null/0, but only one DAU per non-zero ValveId
            
            // ValveConfiguration configuration
            builder.Entity<ValveConfiguration>()
                .Property(c => c.ConfigurationType)
                .IsRequired();

            builder.Entity<ValveConfiguration>()
                .Property(c => c.ConfigurationValue)
                .IsRequired();

            // ValveLog configuration
            builder.Entity<ValveLog>()
                .Property(l => l.Message)
                .IsRequired();
        }
    }
}
