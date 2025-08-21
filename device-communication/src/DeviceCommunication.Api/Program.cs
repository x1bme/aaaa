using DeviceCommunication.Api.Services;
using DeviceCommunication.Core.Interfaces;
using DeviceCommunication.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();

// Register our services
builder.Services.AddSingleton<IDeviceConnectionManager, MockDeviceConnectionManager>(); // For gRPC service

// Register Message Handlers (Currently inactive in TCP flow but kept for reference)
builder.Services.AddSingleton<HealthCommandHandler>();
builder.Services.AddSingleton<CalibrationHandler>();
builder.Services.AddSingleton<FirmwareHandler>();
// builder.Services.AddSingleton<DataHandler>();
// builder.Services.AddSingleton<DeviceHandler>();

// Register the MessageRouter (Currently inactive in TCP flow)
// builder.Services.AddSingleton<IMessageHandler, MessageRouter>();

// Register the TcpConnectionManager as a singleton
builder.Services.AddSingleton<TcpConnectionManager>();
// Register the DeviceCommandOrchestrator as a singleton
builder.Services.AddSingleton<DeviceCommandOrchestrator>();

builder.Services.AddSingleton<PtpManagementService>();
builder.Services.AddHostedService<PtpManagementService>();

// Register Background Services
// To enable all, uncomment them. For focused testing, keep some commented.
//builder.Services.AddHostedService<HeartbeatService>();
//builder.Services.AddHostedService<FirmwareInfoService>();
//builder.Services.AddHostedService<FirmwareUpdateService>(); // This one is quite active
//builder.Services.AddHostedService<HealthStatusService>();
//builder.Services.AddHostedService<CalibrationService>();
//builder.Services.AddHostedService<DataConfigService>();
//builder.Services.AddHostedService<ManageDataService>();
//builder.Services.AddHostedService<DeviceConfigService>();
//builder.Services.AddHostedService<DeviceControlService>();  
//builder.Services.AddHostedService<FactoryResetService>();   
//builder.Services.AddHostedService<SyncTimeService>();       


// Add logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});

var app = builder.Build();

// Configure TCP Connection Manager
var tcpManager = app.Services.GetRequiredService<TcpConnectionManager>();
// Start the TCP listener in the background
_ = tcpManager.StartAsync();

// Ensure the orchestrator is instantiated so it subscribes to events.
// If TcpConnectionManager is resolved first (as above), and Orchestrator's constructor
// subscribes, this explicit GetRequiredService might not be strictly necessary for subscription,
// but it's good for clarity and ensures it's created.
app.Services.GetRequiredService<DeviceCommandOrchestrator>();

// Configure the HTTP request pipeline (for gRPC)
//app.MapGrpcService<DeviceCommunicationService>();
app.MapGrpcService<SimpleDeviceControllerImpl>();
//app.MapGrpcService<GreeterService>();
app.MapGrpcReflectionService();

app.MapGet("/", () => "Device Communication Server is running. gRPC endpoints are available. TCP listener active.");

// Start the web host and block the main thread
app.Run();
