using DeviceCommunication.Core.Interfaces;
using DeviceCommunication.Grpc;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace DeviceCommunication.Api.Services;

public class DeviceCommunicationService : Grpc.DeviceCommunicationService.DeviceCommunicationServiceBase
{
	private readonly ILogger<DeviceCommunicationService> _logger;
	private readonly IDeviceConnectionManager _connectionManager;

	public DeviceCommunicationService(
			ILogger<DeviceCommunicationService> logger,
			IDeviceConnectionManager connectionManager)
	{
		_logger = logger;
		_connectionManager = connectionManager;
	}

	public override async Task<ConnectDeviceResponse> ConnectDevice(
			ConnectDeviceRequest request, ServerCallContext context)
	{
		try
		{
			var connection = await _connectionManager.ConnectDeviceAsync(
					request.DeviceId, 
					request.ConnectionParams);

			return new ConnectDeviceResponse
			{
				Success = connection.IsConnected,
					Message = connection.IsConnected ? "Device connected successfully" : connection.LastError ?? "Unknown error",
					ConnectionId = connection.ConnectionId
			};
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error connecting device {DeviceId}", request.DeviceId);
			return new ConnectDeviceResponse
			{
				Success = false,
					Message = "Internal error occurred while connecting device"
			};
		}
	}

	public override async Task<SendToDeviceResponse> SendToDevice(
			SendToDeviceRequest request, ServerCallContext context)
	{
		try
		{
			var success = await _connectionManager.SendDataToDeviceAsync(
					request.DeviceId,
					request.Data.ToByteArray());

			return new SendToDeviceResponse
			{
				Success = success,
					Message = success ? $"Data sent successfully|{request.ToString()}" : "Failed to send data"
			};
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error sending data to device {DeviceId}", request.DeviceId);
			return new SendToDeviceResponse
			{
				Success = false,
					Message = "Internal error occurred while sending data"
			};
		}
	}

	public override async Task<ReceiveFromDeviceResponse> ReceiveFromDevice(
			ReceiveFromDeviceRequest request, ServerCallContext context)
	{
		try
		{
			var data = await _connectionManager.ReceiveDataFromDeviceAsync(request.DeviceId);

			return new ReceiveFromDeviceResponse
			{
				Success = data != null,
					Message = data != null ? "Data received successfully" : "No data available",
					Data = data != null ? ByteString.CopyFrom(data) : ByteString.Empty
			};
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error receiving data from device {DeviceId}", request.DeviceId);
			return new ReceiveFromDeviceResponse
			{
				Success = false,
					Message = "Internal error occurred while receiving data"
			};
		}
	}

	public override async Task<DeviceStatusResponse> GetDeviceStatus(
			DeviceStatusRequest request, ServerCallContext context)
	{
		try
		{
			var connection = await _connectionManager.GetDeviceConnectionAsync(request.DeviceId);

			if (connection == null)
			{
				return new DeviceStatusResponse
				{
					Status = DeviceStatusResponse.Types.Status.Disconnected,
					       Message = "Device not found"
				};
			}

			return new DeviceStatusResponse
			{
				Status = connection.IsConnected 
					? DeviceStatusResponse.Types.Status.Connected 
					: DeviceStatusResponse.Types.Status.Disconnected,
					Message = connection.LastError ?? "Device status retrieved successfully"
			};
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error getting status for device {DeviceId}", request.DeviceId);
			return new DeviceStatusResponse
			{
				Status = DeviceStatusResponse.Types.Status.Error,
				       Message = "Internal error occurred while getting device status"
			};
		}
	}
}
