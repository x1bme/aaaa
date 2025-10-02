using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DataAccess
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<GeminiDbContext>
    {
        public GeminiDbContext CreateDbContext(string[] args)
        {
            var builder = new DbContextOptionsBuilder<GeminiDbContext>();
            // For design-time migrations, connect as root using host/port/credentials from env
            var host = Environment.GetEnvironmentVariable("MYSQL_HOST") ?? "127.0.0.1";
            var port = Environment.GetEnvironmentVariable("MYSQL_PORT") ?? "5050";
            var db   = Environment.GetEnvironmentVariable("DBNAME")     ?? "LoomDB";
            var pwd  = Environment.GetEnvironmentVariable("ROOTPASSWORD") ?? "rootpassword";
            var conn = Environment.GetEnvironmentVariable("CONNECTION_STRING")
                       ?? $"Server={host};Port={port};Database={db};User=root;Password={pwd}";
            builder.UseMySql(conn, ServerVersion.AutoDetect(conn));
            return new GeminiDbContext(builder.Options);
        }
    }
}