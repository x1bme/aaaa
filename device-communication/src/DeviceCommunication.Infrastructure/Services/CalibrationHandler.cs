using DeviceCommunication.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Device;

namespace DeviceCommunication.Infrastructure.Services;

public class CalibrationHandler : BaseMessageHandler
{
	public CalibrationHandler(ILogger<CalibrationHandler> logger) : base(logger)
	{
	}

	public override async Task<byte[]> HandleMessageAsync(byte[] message)
	{
		var mainMessage = DeserializeMessage<Device.Main>(message);

		if (mainMessage.PayloadCase == Device.Main.PayloadOneofCase.CalibrationRequest)
		{
			return await HandleCalibrationRequestAsync(mainMessage);
		}

		_logger.LogWarning("Received non-calibration message in CalibrationHandler");
		return Array.Empty<byte>();
	}

	private async Task<byte[]> HandleCalibrationRequestAsync(Device.Main message)
	{
		var request = message.CalibrationRequest;
		var response = new Device.Main { Header = message.Header };

		var calibrationResponse = new CalibrationResponse
		{
			ManageCalibration = new ManageCalibrationResponse
			{
				Operation = request.ManageCalibration.Operation
			}
		};

		switch (request.ManageCalibration.Operation)
		{
			case CalibrationOperation.ReadParams:
				calibrationResponse.ManageCalibration.ReadParams = await HandleReadParamsAsync(
						request.ManageCalibration.ReadParams);
				break;

			case CalibrationOperation.StartProcedure:
				calibrationResponse.ManageCalibration.StartProcedure = await HandleStartProcedureAsync(
						request.ManageCalibration.StartProcedure);
				break;

			case CalibrationOperation.GetStatus:
				calibrationResponse.ManageCalibration.GetStatus = await HandleGetStatusAsync(
						request.ManageCalibration.GetStatus);
				break;
		}

		response.CalibrationResponse = calibrationResponse;
		return SerializeMessage(response);
	}

	private async Task<ReadCalibrationParamsResponse> HandleReadParamsAsync(ReadCalibrationParamsRequest request)
	{
		return new ReadCalibrationParamsResponse
		{
			ResponseBase = new ResponseBase { Status = StatusCode.StatusOk },
				     Parameters = 
				     {
					     new AdcChannelCalibrationParams
					     {
						     ChannelId = 1,
						     Offset = 0.001f,
						     Gain = 1.0f,
						     LastUpdatedMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
						     CalibrationExpiresMs = (ulong)DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeMilliseconds(),
						     TemperatureAtCalCelsius = 25.0f
					     }
				     }
		};
	}

	private async Task<StartCalibrationProcedureResponse> HandleStartProcedureAsync(StartCalibrationProcedureRequest request)
	{
		return new StartCalibrationProcedureResponse
		{
			ResponseBase = new ResponseBase { Status = StatusCode.StatusOk },
				     ProcedureStarted = true,
				     EstimatedDurationSeconds = 300
		};
	}

	private async Task<GetCalibrationStatusResponse> HandleGetStatusAsync(GetCalibrationStatusRequest request)
	{
		return new GetCalibrationStatusResponse
		{
			ResponseBase = new ResponseBase { Status = StatusCode.StatusOk },
				     IsCalibrating = true,
				     ProgressPercent = 50,
				     TimeRemainingSeconds = 150,
				     ChannelsInProgress = { 1, 2 }
		};
	}
}
