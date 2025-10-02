using DeviceProxy.Api.Services;
using DeviceProxy.Core.Interfaces;
using DeviceProxy.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();

// Register our services
builder.Services.AddSingleton<IMessageRelayService, MessageRelayService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<DeviceProxyService>();
app.MapGrpcReflectionService();

app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client.");

app.Run();
