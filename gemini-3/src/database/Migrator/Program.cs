using System;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using DataAccess;

var dbName = Environment.GetEnvironmentVariable("DBNAME");
var dbUser = Environment.GetEnvironmentVariable("DBUSER");
var dbPass = Environment.GetEnvironmentVariable("DBPASS");
var connStr = $"Server=mysql;Port=3306;Database={dbName};User={dbUser};Password={dbPass}";

var host = Host.CreateDefaultBuilder()
    .ConfigureServices((ctx, services) =>
        services.AddDbContext<GeminiDbContext>(opts =>
            opts.UseMySql(
                connStr,
                ServerVersion.AutoDetect(connStr))))
    .Build();

// apply migrations
using var scope = host.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<GeminiDbContext>();
db.Database.Migrate();

Console.WriteLine("Migrations applied.");