using DeviceCommunication.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Device;

namespace DeviceCommunication.Infrastructure.Services;

public class FirmwareHandler : BaseMessageHandler
{
	public FirmwareHandler(ILogger<FirmwareHandler> logger) : base(logger)
	{
	}

	public override async Task<byte[]> HandleMessageAsync(byte[] message)
	{
		var mainMessage = DeserializeMessage<Device.Main>(message);

		if (mainMessage.PayloadCase == Device.Main.PayloadOneofCase.FirmwareRequest)
		{
			return await HandleFirmwareRequestAsync(mainMessage);
		}

		_logger.LogWarning("Received non-firmware message in FirmwareHandler");
		return Array.Empty<byte>();
	}

	private async Task<byte[]> HandleFirmwareRequestAsync(Device.Main message)
	{
		var request = message.FirmwareRequest;
		var response = new Device.Main { Header = message.Header };

		var firmwareResponse = new FirmwareResponse
		{
			CommandType = request.CommandType
		};

		switch (request.CommandType)
		{
			case FirmwareCommandType.GetInfo:
				firmwareResponse.GetInfo = await HandleGetInfoAsync(request.GetInfo);
				break;

			case FirmwareCommandType.Update:
				firmwareResponse.Update = await HandleUpdateAsync(request.Update);
				break;
		}

		response.FirmwareResponse = firmwareResponse;
		return SerializeMessage(response);
	}

	private async Task<GetFirmwareInfoResponse> HandleGetInfoAsync(GetFirmwareInfoRequest request)
	{
		return new GetFirmwareInfoResponse
		{
			ResponseBase = new ResponseBase { Status = StatusCode.StatusOk },
				     Version = "1.0.0",
				     BuildDate = "2024-04-24",
				     BuildHash = "abc123",
				     SecureBootEnabled = true,
				     CurrentImageSlot = "A"
		};
	}

	private async Task<UpdateFirmwareResponse> HandleUpdateAsync(UpdateFirmwareRequest request)
	{
		var response = new UpdateFirmwareResponse
		{
			ResponseBase = new ResponseBase { Status = StatusCode.StatusOk },
				     Operation = request.Operation
		};

		switch (request.Operation)
		{
			case FirmwareUpdateOperation.FirmwareOpPrepare:
				response.Prepare = new FirmwarePrepareResponsePayload
				{
					ReadyToReceive = true,
						       MaxBlockSize = 1024,
						       EstimatedStorageTimeSeconds = 60
				};
				break;

			case FirmwareUpdateOperation.FirmwareOpTransfer:
				response.Transfer = new FirmwareTransferResponsePayload
				{
					BlockSequenceNumber = request.Transfer.BlockSequenceNumber,
							    CrcOk = true
				};
				break;

			case FirmwareUpdateOperation.FirmwareOpVerify:
				response.Verify = new FirmwareVerifyResponsePayload
				{
					VerificationPassed = true
				};
				break;

			case FirmwareUpdateOperation.FirmwareOpApply:
				response.Apply = new FirmwareApplyResponsePayload
				{
					ApplicationScheduled = true
				};
				break;
		}

		return response;
	}
}
