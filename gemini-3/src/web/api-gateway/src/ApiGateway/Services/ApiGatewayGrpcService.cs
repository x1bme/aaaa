using System;
using System.Threading.Tasks;
using DeviceCommunication.Core.Grpc.SimpleControl;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ApiGateway.Services
{
    public interface IGrpcClientService
    {
        Task<DauObjects> GetAllDausAsync(params string[] deviceIds);
        Task<ResponseBase> UpdateFirmwareAsync(string deviceId, string version, byte[] firmwareData);
        Task<ResponseBase> ConfigureDauAsync(Dau dauConfig);
        Task<SendDeviceHeartbeatResponse> SendDeviceHeartbeatAsync(string deviceId);
        Task<GetDeviceHealthStatusResponse> GetDeviceHealthStatusAsync(string deviceId);
    }

    public class GrpcClientService : IGrpcClientService, IDisposable
    {
        private readonly ILogger<GrpcClientService> _logger;
        private readonly GrpcChannel _channel;
        private readonly SimpleDeviceController.SimpleDeviceControllerClient _client;

        public GrpcClientService(IConfiguration configuration, ILogger<GrpcClientService> logger)
        {
            _logger = logger;
            
            var grpcEndpoint = configuration["GrpcService:Endpoint"] ?? "https://localhost:5001";
            
            _channel = GrpcChannel.ForAddress(grpcEndpoint, new GrpcChannelOptions
            {
                // Configure options as needed
                // HttpHandler = new HttpClientHandler
                // {
                //     ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                // }
            });
            
            _client = new SimpleDeviceController.SimpleDeviceControllerClient(_channel);
        }

        public async Task<DauObjects> GetAllDausAsync(params string[] deviceIds)
        {
            try
            {
                var request = new DauList();
                foreach (var deviceId in deviceIds)
                {
                    request.DeviceId.Add(deviceId);
                }

                _logger.LogInformation("Calling gRPC GetAllDaus with {Count} device IDs", deviceIds.Length);
                var response = await _client.GetAllDausAsync(request);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling GetAllDaus");
                throw;
            }
        }

        public async Task<ResponseBase> UpdateFirmwareAsync(string deviceId, string version, byte[] firmwareData)
        {
            try
            {
                var request = new UpdatePayload
                {
                    DeviceId = deviceId,
                    Version = version,
                    FirmwareData = Google.Protobuf.ByteString.CopyFrom(firmwareData),
                    Timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                _logger.LogInformation("Calling gRPC UpdateFirmware for device {DeviceId}", deviceId);
                var response = await _client.UpdateFirmwareAsync(request);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling UpdateFirmware");
                throw;
            }
        }

        public async Task<ResponseBase> ConfigureDauAsync(Dau dauConfig)
        {
            try
            {
                var request = new Configuration
                {
                    Dau = dauConfig
                };

                _logger.LogInformation("Calling gRPC ConfigureDau for device {DeviceId}", dauConfig.DeviceId);
                var response = await _client.ConfigureDauAsync(request);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling ConfigureDau");
                throw;
            }
        }

        public async Task<SendDeviceHeartbeatResponse> SendDeviceHeartbeatAsync(string deviceId)
        {
            try
            {
                var request = new SendDeviceHeartbeatRequest
                {
                    DeviceId = deviceId
                };

                _logger.LogInformation("Sending heartbeat for device {DeviceId}", deviceId);
                var response = await _client.SendDeviceHeartbeatAsync(request);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending heartbeat for device {DeviceId}", deviceId);
                throw;
            }
        }

        public async Task<GetDeviceHealthStatusResponse> GetDeviceHealthStatusAsync(string deviceId)
        {
            try
            {
                var request = new GetDeviceHealthStatusRequest
                {
                    DeviceId = deviceId
                };

                _logger.LogInformation("Getting health status for device {DeviceId}", deviceId);
                var response = await _client.GetDeviceHealthStatusAsync(request);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting health status for device {DeviceId}", deviceId);
                throw;
            }
        }

        public void Dispose()
        {
            _channel?.Dispose();
        }
    }
}