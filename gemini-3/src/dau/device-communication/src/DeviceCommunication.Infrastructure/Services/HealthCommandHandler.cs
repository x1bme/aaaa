using DeviceCommunication.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Device;

namespace DeviceCommunication.Infrastructure.Services;

public class HealthCommandHandler : BaseMessageHandler
{
	public HealthCommandHandler(ILogger<HealthCommandHandler> logger) : base(logger)
	{
	}

	public override async Task<byte[]> HandleMessageAsync(byte[] message)
	{
		var mainMessage = DeserializeMessage<Main>(message);

		if (mainMessage.PayloadCase == Main.PayloadOneofCase.HealthRequest)
		{
			return await HandleHealthRequestAsync(mainMessage);
		}

		_logger.LogWarning("Received non-health message in HealthCommandHandler");
		return Array.Empty<byte>();
	}

	private async Task<byte[]> HandleHealthRequestAsync(Main message)
	{
		var healthRequest = message.HealthRequest;
		var response = new Main
		{
			Header = message.Header
		};

		switch (healthRequest.CommandType)
		{
			case HealthCommandType.Heartbeat:
				response.HealthResponse = new HealthResponse
				{
					CommandType = HealthCommandType.Heartbeat,
						    Heartbeat = await HandleHeartbeatAsync(healthRequest.Heartbeat)
				};
				break;

			case HealthCommandType.HealthStatus:
				response.HealthResponse = new HealthResponse
				{
					CommandType = HealthCommandType.HealthStatus,
						    HealthStatus = await HandleHealthStatusAsync(healthRequest.HealthStatus)
				};
				break;
		}

		return SerializeMessage(response);
	}

	private async Task<HeartbeatResponse> HandleHeartbeatAsync(HeartbeatRequest request)
	{
		return new HeartbeatResponse
		{
			ResponseBase = new ResponseBase
			{
				Status = StatusCode.StatusOk
			},
				DeviceTimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
		};
	}

	private async Task<HealthStatusResponse> HandleHealthStatusAsync(HealthStatusRequest request)
	{
		return new HealthStatusResponse
		{
			Operation = request.Operation,
				  GetCurrent = new GetCurrentStatusResponse
				  {
					  ResponseBase = new ResponseBase { Status = StatusCode.StatusOk },
					  IsOperational = true,
					  SystemState = "RUNNING",
					  TemperatureCelsius = 25.5f,
					  CpuUsagePercent = 30.5f,
					  UptimeSeconds = 3600
				  }
		};
	}
}
