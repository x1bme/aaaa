using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ApiGateway.Controllers;
using ApiGateway.Services;
using FluentAssertions;
using DeviceCommunication.Core.Grpc.SimpleControl;
using Google.Protobuf;

namespace ApiGateway.Tests.Controllers
{
    public class GrpcControllerTests
    {
        private readonly Mock<IGrpcClientService> _mockGrpcClient;
        private readonly Mock<ILogger<GrpcController>> _mockLogger;
        private readonly GrpcController _controller;

        public GrpcControllerTests()
        {
            _mockGrpcClient = new Mock<IGrpcClientService>();
            _mockLogger = new Mock<ILogger<GrpcController>>();
            _controller = new GrpcController(_mockGrpcClient.Object, _mockLogger.Object);
        }

        #region GetDausFromGrpc Tests

        [Fact]
        public async Task GetDausFromGrpc_ReturnsOkResult_WithDauList()
        {
            // Arrange
            var deviceIds = new[] { "device1", "device2" };
            var expectedResponse = new DauObjects();
            expectedResponse.Dau.Add(new Dau 
            { 
                DeviceId = "device1",
                LastHeartbeat = 1234567890,
                IsOperational = true,
                StaticIpAddress = "192.168.1.100"
            });
            expectedResponse.Dau.Add(new Dau 
            { 
                DeviceId = "device2",
                LastHeartbeat = 1234567891,
                IsOperational = false,
                StaticIpAddress = "192.168.1.101"
            });

            _mockGrpcClient.Setup(client => client.GetAllDausAsync(deviceIds))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetDausFromGrpc(deviceIds);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value;
            response.Should().BeEquivalentTo(new
            {
                success = true,
                count = 2,
                daus = expectedResponse.Dau
            });
        }

        [Fact]
        public async Task GetDausFromGrpc_ReturnsInternalServerError_WhenExceptionOccurs()
        {
            // Arrange
            var deviceIds = new[] { "device1" };
            _mockGrpcClient.Setup(client => client.GetAllDausAsync(deviceIds))
                .ThrowsAsync(new Exception("gRPC connection failed"));

            // Act
            var result = await _controller.GetDausFromGrpc(deviceIds);

            // Assert
            var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
            statusCodeResult.StatusCode.Should().Be(500);
            statusCodeResult.Value.Should().BeEquivalentTo(new { error = "gRPC connection failed" });
        }

        #endregion

        #region ConfigureDau Tests

        [Fact]
        public async Task ConfigureDau_ReturnsOkResult_WhenConfigurationSuccessful()
        {
            // Arrange
            var request = new DauConfigRequest
            {
                DeviceId = "device1",
                IpAddress = "192.168.1.100",
                IsOperational = true,
                Gateway = "192.168.1.1"
            };

            var expectedResponse = new ResponseBase
            {
                Status = StatusCode.StatusOk,
                Message = "Configuration successful"
            };

            _mockGrpcClient.Setup(client => client.ConfigureDauAsync(It.IsAny<Dau>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.ConfigureDau(request);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(new
            {
                success = true,
                message = "Configuration successful",
                status = "StatusOk"
            });
        }

        [Fact]
        public async Task ConfigureDau_ReturnsOkResultWithFailure_WhenConfigurationFails()
        {
            // Arrange
            var request = new DauConfigRequest
            {
                DeviceId = "device1",
                IpAddress = "192.168.1.100",
                IsOperational = true
            };

            var expectedResponse = new ResponseBase
            {
                Status = StatusCode.StatusError,
                Message = "Configuration failed"
            };

            _mockGrpcClient.Setup(client => client.ConfigureDauAsync(It.IsAny<Dau>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.ConfigureDau(request);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(new
            {
                success = false,
                message = "Configuration failed",
                status = "StatusError"
            });
        }

        [Fact]
        public async Task ConfigureDau_UsesDefaultGateway_WhenGatewayNotProvided()
        {
            // Arrange
            var request = new DauConfigRequest
            {
                DeviceId = "device1",
                IpAddress = "192.168.1.100",
                IsOperational = true,
                Gateway = null
            };

            Dau capturedDau = null;
            _mockGrpcClient.Setup(client => client.ConfigureDauAsync(It.IsAny<Dau>()))
                .Callback<Dau>(dau => capturedDau = dau)
                .ReturnsAsync(new ResponseBase { Status = StatusCode.StatusOk });

            // Act
            await _controller.ConfigureDau(request);

            // Assert
            capturedDau.Should().NotBeNull();
            capturedDau.Gateway.Should().Be("192.168.1.1");
        }

        #endregion

        #region UpdateFirmware Tests

        [Fact]
        public async Task UpdateFirmware_ReturnsOkResult_WhenUpdateSuccessful()
        {
            // Arrange
            var request = new FirmwareUpdateRequest
            {
                DeviceId = "device1",
                Version = "2.0.0",
                FirmwareDataBase64 = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5 })
            };

            var expectedResponse = new ResponseBase
            {
                Status = StatusCode.StatusOk,
                Message = "Firmware update initiated"
            };

            _mockGrpcClient.Setup(client => client.UpdateFirmwareAsync(
                    request.DeviceId, 
                    request.Version, 
                    It.IsAny<byte[]>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.UpdateFirmware(request);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(new
            {
                success = true,
                message = "Firmware update initiated",
                status = "StatusOk"
            });
        }

        [Fact]
        public async Task UpdateFirmware_HandlesEmptyFirmwareData()
        {
            // Arrange
            var request = new FirmwareUpdateRequest
            {
                DeviceId = "device1",
                Version = "2.0.0",
                FirmwareDataBase64 = null
            };

            byte[] capturedFirmwareData = null;
            _mockGrpcClient.Setup(client => client.UpdateFirmwareAsync(
                    It.IsAny<string>(), 
                    It.IsAny<string>(), 
                    It.IsAny<byte[]>()))
                .Callback<string, string, byte[]>((id, ver, data) => capturedFirmwareData = data)
                .ReturnsAsync(new ResponseBase { Status = StatusCode.StatusOk });

            // Act
            await _controller.UpdateFirmware(request);

            // Assert
            capturedFirmwareData.Should().NotBeNull();
            capturedFirmwareData.Should().BeEmpty();
        }

        #endregion

        #region SendHeartbeat Tests

        [Fact]
        public async Task SendHeartbeat_ReturnsOkResult_WhenHeartbeatSuccessful()
        {
            // Arrange
            var deviceId = "device1";
            var expectedResponse = new SendDeviceHeartbeatResponse
            {
                Success = true,
                Message = "Heartbeat received",
                DeviceTimestampMs = 1234567890
            };

            _mockGrpcClient.Setup(client => client.SendDeviceHeartbeatAsync(deviceId))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.SendHeartbeat(deviceId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(new
            {
                success = true,
                message = "Heartbeat received",
                deviceTimestamp = (ulong)1234567890,
                deviceId = deviceId
            });
        }

        [Fact]
        public async Task SendHeartbeat_ReturnsInternalServerError_WhenExceptionOccurs()
        {
            // Arrange
            var deviceId = "device1";
            _mockGrpcClient.Setup(client => client.SendDeviceHeartbeatAsync(deviceId))
                .ThrowsAsync(new Exception("Network timeout"));

            // Act
            var result = await _controller.SendHeartbeat(deviceId);

            // Assert
            var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
            statusCodeResult.StatusCode.Should().Be(500);
            statusCodeResult.Value.Should().BeEquivalentTo(new { error = "Network timeout" });
        }

        #endregion

        #region GetHealthStatus Tests

        [Fact]
        public async Task GetHealthStatus_ReturnsOkResult_WithHealthData()
        {
            // Arrange
            var deviceId = "device1";
            var expectedResponse = new GetDeviceHealthStatusResponse
            {
                Success = true,
                Message = "Health check complete",
                IsOperational = true,
                SystemState = "Running",
                TemperatureCelsius = 35.5f,
                UptimeSeconds = 86400,
                CpuUsagePercent = 45.2f,
                PtpLocked = true,
                LastResetReason = "PowerOn"
            };

            _mockGrpcClient.Setup(client => client.GetDeviceHealthStatusAsync(deviceId))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetHealthStatus(deviceId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(new
            {
                success = true,
                message = "Health check complete",
                deviceId = deviceId,
                health = new
                {
                    isOperational = true,
                    systemState = "Running",
                    temperatureCelsius = 35.5f,
                    uptimeSeconds = (ulong)86400,
                    cpuUsagePercent = 45.2f,
                    ptpLocked = true,
                    lastResetReason = "PowerOn"
                }
            });
        }

        [Fact]
        public async Task GetHealthStatus_ReturnsOkResult_WhenDeviceNotOperational()
        {
            // Arrange
            var deviceId = "device1";
            var expectedResponse = new GetDeviceHealthStatusResponse
            {
                Success = false,
                Message = "Device offline",
                IsOperational = false,
                SystemState = "Offline",
                TemperatureCelsius = 0,
                UptimeSeconds = 0,
                CpuUsagePercent = 0,
                PtpLocked = false,
                LastResetReason = "Unknown"
            };

            _mockGrpcClient.Setup(client => client.GetDeviceHealthStatusAsync(deviceId))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetHealthStatus(deviceId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value;
            response.Should().NotBeNull();
            response.GetType().GetProperty("success").GetValue(response).Should().Be(false);
            response.GetType().GetProperty("message").GetValue(response).Should().Be("Device offline");
        }

        #endregion
    }
}
