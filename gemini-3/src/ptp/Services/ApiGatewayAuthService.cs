using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PtpService.Services;

public interface IApiGatewayAuthService
{
    Task<string> GetAccessTokenAsync();
}

public class ApiGatewayAuthService : IApiGatewayAuthService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApiGatewayAuthService> _logger;
    private string? _cachedToken;
    private DateTime _tokenExpiry = DateTime.MinValue;
    private readonly SemaphoreSlim _tokenLock = new SemaphoreSlim(1, 1);

    public ApiGatewayAuthService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ApiGatewayAuthService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync()
    {
        // Return cached token if still valid
        if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiry)
        {
            _logger.LogDebug("Using cached access token");
            return _cachedToken;
        }

        await _tokenLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiry)
            {
                return _cachedToken;
            }

            _logger.LogInformation("Requesting new access token from API Gateway");

            var client = _httpClientFactory.CreateClient();
            var apiGatewayUrl = _configuration["ApiGateway:Url"];
            var clientId = _configuration["ApiGateway:ClientId"];
            var clientSecret = _configuration["ApiGateway:ClientSecret"];

            if (string.IsNullOrEmpty(apiGatewayUrl) || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                throw new InvalidOperationException("API Gateway authentication configuration is missing");
            }

            var tokenEndpoint = $"{apiGatewayUrl.TrimEnd('/')}/connect/token";
            _logger.LogDebug("Token endpoint: {TokenEndpoint}", tokenEndpoint);

            var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["scope"] = "api"
            });

            var response = await client.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to get access token. Status: {StatusCode}, Response: {Response}", 
                    response.StatusCode, errorContent);
                throw new Exception($"Failed to get access token: {response.StatusCode}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (tokenResponse?.AccessToken == null)
            {
                _logger.LogError("Token response did not contain access_token");
                throw new Exception("Failed to get access token - invalid response");
            }

            _cachedToken = tokenResponse.AccessToken;
            // Refresh token 60 seconds before expiry
            _tokenExpiry = DateTime.UtcNow.AddSeconds(Math.Max(tokenResponse.ExpiresIn - 60, 0));

            _logger.LogInformation("Successfully obtained access token, expires in {ExpiresIn} seconds", tokenResponse.ExpiresIn);

            return _cachedToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get access token from API Gateway");
            throw;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; } = 3600;

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }
    }
}
