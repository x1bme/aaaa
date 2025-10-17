using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace PtpService.Services;

public class DauSyncService : BackgroundService
{
    private readonly ILogger<DauSyncService> _logger;
    private readonly IPtpConfigManager _configManager;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IApiGatewayAuthService _authService;
    private readonly string _apiGatewayUrl;

    public DauSyncService(
        ILogger<DauSyncService> logger,
        IPtpConfigManager configManager,
        IHttpClientFactory httpClientFactory,
        IApiGatewayAuthService authService,
        IConfiguration configuration)
    {
        _logger = logger;
        _configManager = configManager;
        _httpClientFactory = httpClientFactory;
        _authService = authService;
        _apiGatewayUrl = configuration["ApiGateway:Url"] ?? "http://localhost:5000";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DAU Sync Service starting");

        // Wait a few seconds for API Gateway to be ready
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        // Initial sync on startup
        await SyncDausAsync(stoppingToken);

        // Optional: Periodic sync every hour as backup
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
                await SyncDausAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("DAU Sync Service stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DAU sync loop");
            }
        }
    }

    public async Task SyncDausAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Syncing DAUs from API Gateway");

            // Get OAuth access token
            string token;
            try
            {
                token = await _authService.GetAccessTokenAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to obtain access token");
                return;
            }

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            
            var dauEndpoint = $"{_apiGatewayUrl.TrimEnd('/')}/api/dau";
            _logger.LogDebug("Calling DAU endpoint: {Endpoint}", dauEndpoint);

            var response = await client.GetAsync(dauEndpoint, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch DAUs from API Gateway. Status: {StatusCode}", response.StatusCode);
                
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Unauthorized - token may be invalid or expired");
                }
                
                return;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var daus = JsonSerializer.Deserialize<List<DauDto>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (daus == null || !daus.Any())
            {
                _logger.LogWarning("No DAUs found");
                await _configManager.UpdateConfigurationAsync(new List<string>());
                return;
            }

            // Extract IP addresses from registered DAUs only
            var ipAddresses = daus
                .Where(d => d.Registered)
                .Select(d => d.DauIPAddress)
                .Where(ip => !string.IsNullOrWhiteSpace(ip))
                .ToList();

            _logger.LogInformation("Found {Count} registered DAU IP addresses", ipAddresses.Count);

            await _configManager.UpdateConfigurationAsync(ipAddresses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync DAUs");
        }
    }
}

// DTO for deserializing DAU data
public class DauDto
{
    public int Id { get; set; }
    public string DauId { get; set; } = string.Empty;
    public string? DauTag { get; set; }
    public string DauIPAddress { get; set; } = string.Empty;
    public bool Registered { get; set; }
    public int? ValveId { get; set; }
}
