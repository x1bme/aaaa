using PtpService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add HttpClient for API Gateway calls
builder.Services.AddHttpClient();

// Register authentication service
builder.Services.AddSingleton<IApiGatewayAuthService, ApiGatewayAuthService>();

// Register PTP services
builder.Services.AddSingleton<INetworkInterfaceService, NetworkInterfaceService>();
builder.Services.AddSingleton<IPtpConfigManager, PtpConfigManager>();
builder.Services.AddSingleton<DauSyncService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<DauSyncService>());

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.MapGet("/", () => "PTP Master Configuration Service is running");

app.Run();
