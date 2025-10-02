using ArchiverService.Services;
using DataAccess;
using Microsoft.OpenApi.Models;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure; 
using Microsoft.EntityFrameworkCore;                   
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Add gRPC services
builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Archiver Service API", Version = "v1" });
});

// Database connection
var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING") ?? 
                       builder.Configuration.GetConnectionString("DefaultConnection");
                       
builder.Services.AddDbContext<GeminiDbContext>(options => 
{
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
});

builder.Services.AddScoped<IArchiveService, ArchiveService>();
builder.Services.AddScoped<ITestService, TestService>();
builder.Services.AddAntiforgery();

// Add health checks for better K8s readiness probes
builder.Services.AddGrpcHealthChecks()
    .AddCheck("", () => HealthCheckResult.Healthy());

// cors
var  AllowSpecificOrigins = "AllowFrontendApp";
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: AllowSpecificOrigins,
                      policy  =>
                      {
                        policy.WithOrigins("https://gemini.local", "http://localhost:8080")
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                      });
});

// Get port configuration from environment variables with defaults
int httpPort = int.TryParse(Environment.GetEnvironmentVariable("HTTP_PORT"), out var port) ? port : 6001;
int grpcPort = int.TryParse(Environment.GetEnvironmentVariable("GRPC_PORT"), out port) ? port : 50051;

// Configure Kestrel with ports from environment
builder.WebHost.ConfigureKestrel(options => {
    // HTTP port for REST APIs
    options.ListenAnyIP(httpPort, listenOptions => {
        listenOptions.Protocols = HttpProtocols.Http1;
    });
    
    // gRPC port
    options.ListenAnyIP(grpcPort, listenOptions => {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Map gRPC services
app.MapGrpcService<ArchiverServiceImpl>();
app.MapGrpcReflectionService();
app.MapGrpcHealthChecksService();

//cors
app.UseCors(AllowSpecificOrigins);

// REST endpoints
app.MapGet("/archives/{valveId}", async (int valveId, IArchiveService archiveService) => 
    await archiveService.GetArchiveForValveAsync(valveId));

app.MapGet("/", () => $"ArchiverService is running! HTTP on port {httpPort}, gRPC on port {grpcPort}");

app.UseAntiforgery();

app.MapPost("/api/tests", async (ITestService testService, int valveId, IFormFile file) =>
{
    return await testService.UploadTestAsync(valveId, file);
})
.WithName("UploadTest")
.WithDescription("Upload a new test file for a valve")
.DisableAntiforgery()
.WithOpenApi();

app.MapGet("/api/tests/{testId}", async (ITestService testService, int testId) =>
{
    return await testService.GetTestByIdAsync(testId);
})
.WithName("GetTest")
.WithDescription("Get test metadata by ID")
.WithOpenApi();

app.MapGet("/api/tests/{testId}/download", async (ITestService testService, int testId) =>
{
    return await testService.DownloadTestFileAsync(testId);
})
.WithName("DownloadTestFile")
.WithDescription("Download the file data for a specific test")
.WithOpenApi();

app.MapGet("/api/database/backup", () => 
{
//  Assuming this service will always return the current version of the db and not
//  track changes, replace the file below with the real db backup.
//  THIS IS A STUB AND WILL NEED TO BE REPLACED
    string sqlContent = "SQL stub file";
    
    return Results.File(
        fileContents: System.Text.Encoding.UTF8.GetBytes(sqlContent),
        contentType: "application/sql",
        fileDownloadName: "stub.sql"
    );
})
.WithName("GetDbBackup")
.WithDescription("Return DB Backup to user")
.WithOpenApi();

app.Run();