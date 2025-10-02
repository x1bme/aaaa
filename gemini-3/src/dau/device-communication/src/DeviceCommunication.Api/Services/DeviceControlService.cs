using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DeviceCommunication.Infrastructure.Services;
using Device;
using Google.Protobuf;

namespace DeviceCommunication.Api.Services
{
	public class DeviceControlService : IHostedService, IDisposable
	{
		private readonly ILogger<DeviceControlService> _logger;
		private readonly TcpConnectionManager _tcpConnectionManager;
		private Timer? _timer;
		private static long _sequenceCounter = 80000;
		private int _actionIndex = 0;

		private const string TargetDeviceId = "test-device-001";
		private const string ServerId = "server-gemini-01";

		public DeviceControlService(ILogger<DeviceControlService> logger, TcpConnectionManager tcpConnectionManager)
		{
			_logger = logger;
			_tcpConnectionManager = tcpConnectionManager;
		}

		public Task StartAsync(CancellationToken cancellationToken)
		{
			_logger.LogInformation("Periodic Device Control Service starting.");
			_timer = new Timer(DoDeviceControlAction, null, TimeSpan.FromSeconds(28), TimeSpan.FromSeconds(45));
			return Task.CompletedTask;
		}

		private void DoDeviceControlAction(object? state)
		{
			try
			{
				var currentSequence = (uint)Interlocked.Increment(ref _sequenceCounter);
				var deviceControlRequest = new Device.DeviceControlRequest();
				string requestDescription = "DeviceControlRequest";

				switch (_actionIndex % 4) // Cycle through Reset, Reboot, SetPowerMode, SetSafeMode
				{
					case 0:
						deviceControlRequest.Action = Device.DeviceControlAction.DeviceActionReset;
						deviceControlRequest.Reset = new Device.ResetPayload { Reason = "Simulated server request", DelayMs = 1000 };
						requestDescription += " (Reset)";
						break;
					case 1:
						deviceControlRequest.Action = Device.DeviceControlAction.DeviceActionReboot;
						deviceControlRequest.Reboot = new Device.RebootPayload { ForceImmediate = false, DelaySeconds = 5 };
						requestDescription += " (Reboot)";
						break;
					case 2:
						deviceControlRequest.Action = Device.DeviceControlAction.DeviceActionSetPowerMode;
						deviceControlRequest.SetPowerMode = new Device.SetPowerModePayload { Mode = Device.PowerMode.PowerLow, DurationSeconds = 3600 };
						requestDescription += " (SetPowerMode Low)";
						break;
					case 3:
						deviceControlRequest.Action = Device.DeviceControlAction.DeviceActionSetSafeMode;
						deviceControlRequest.SetSafeMode = new Device.SetSafeModePayload { Enable = true };
						requestDescription += " (SetSafeMode Enable)";
						break;
				}
				_actionIndex++;

				var requestToSend = new Device.Main
				{
					Header = new Device.Header { DeviceId = ServerId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
					       DeviceRequest = new Device.DeviceRequest
					       {
						       CommandType = Device.DeviceCommandType.DeviceControl,
						       Control = deviceControlRequest
					       }
				};

				_logger.LogInformation("Attempting to send {RequestDescription} (Seq: {Seq}) to {DeviceId}",
						requestDescription, requestToSend.Header.SequenceNumber, TargetDeviceId);

				byte[] requestBytes = requestToSend.ToByteArray();
				_ = _tcpConnectionManager.SendCommandAsync(TargetDeviceId, requestBytes);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error occurred during periodic device control action.");
			}
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			_logger.LogInformation("Periodic Device Control Service stopping.");
			_timer?.Change(Timeout.Infinite, 0);
			return Task.CompletedTask;
		}

		public void Dispose()
		{
			_timer?.Dispose();
			GC.SuppressFinalize(this);
		}
	}
}
