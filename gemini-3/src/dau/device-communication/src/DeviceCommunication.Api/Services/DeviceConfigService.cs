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
	public class DeviceConfigService : IHostedService, IDisposable
	{
		private readonly ILogger<DeviceConfigService> _logger;
		private readonly TcpConnectionManager _tcpConnectionManager;
		private Timer? _timer;
		private static long _sequenceCounter = 70000; // Separate sequence counter
		private int _operationIndex = 0; // To cycle through different config operations

		private const string TargetDeviceId = "test-device-001";
		private const string ServerId = "server-gemini-01";

		public DeviceConfigService(ILogger<DeviceConfigService> logger, TcpConnectionManager tcpConnectionManager)
		{
			_logger = logger;
			_tcpConnectionManager = tcpConnectionManager;
		}

		public Task StartAsync(CancellationToken cancellationToken)
		{
			_logger.LogInformation("Periodic Device Config Service starting.");
			// Start timer, e.g., every 35 seconds, with an initial delay
			_timer = new Timer(DoDeviceConfigCheck, null, TimeSpan.FromSeconds(22), TimeSpan.FromSeconds(35));
			return Task.CompletedTask;
		}

		private void DoDeviceConfigCheck(object? state)
		{
			try
			{
				var currentSequence = (uint)Interlocked.Increment(ref _sequenceCounter);
				Device.Main requestToSend;
				string requestDescription = "DeviceConfigRequest";

				var deviceConfigRequest = new Device.DeviceConfigRequest();

				// Cycle through operations for demonstration
				switch (_operationIndex % 6) // We have 6 operations in DeviceConfigOperation
				{
					case 0:
						deviceConfigRequest.Operation = Device.DeviceConfigOperation.SetAssignedName;
						deviceConfigRequest.SetAssignedName = new Device.SetAssignedNamePayload { AssignedName = $"SimDevice_{DateTime.Now:HHmmss}" };
						requestDescription += " (SetAssignedName)";
						break;
					case 1:
						deviceConfigRequest.Operation = Device.DeviceConfigOperation.GetNetworkConfig;
						deviceConfigRequest.GetNetworkConfig = new Device.GetNetworkConfigRequest();
						requestDescription += " (GetNetworkConfig)";
						break;
					case 2:
						deviceConfigRequest.Operation = Device.DeviceConfigOperation.SetNetworkConfig;
						deviceConfigRequest.SetNetworkConfig = new Device.SetNetworkConfigRequest
						{
							Settings = new Device.NetworkSettings
							{
								UseDhcp = false,
									StaticIpAddress = "192.168.1.123",
									SubnetMask = "255.255.255.0",
									Gateway = "192.168.1.1",
									PrimaryDns = "8.8.8.8"
							}
						};
						requestDescription += " (SetNetworkConfig)";
						break;
					case 3:
						deviceConfigRequest.Operation = Device.DeviceConfigOperation.GetCertificateInfo;
						deviceConfigRequest.GetCertificateInfo = new Device.GetCertificateInfoRequest();
						requestDescription += " (GetCertificateInfo)";
						break;
					case 4:
						deviceConfigRequest.Operation = Device.DeviceConfigOperation.GenerateCsr;
						deviceConfigRequest.GenerateCsr = new Device.GenerateCsrRequest();
						requestDescription += " (GenerateCSR)";
						break;
					case 5:
						deviceConfigRequest.Operation = Device.DeviceConfigOperation.UpdateCertificate;
						deviceConfigRequest.UpdateCertificate = new Device.UpdateCertificateRequest
						{
							NewCertificateDer = ByteString.CopyFrom(System.Text.Encoding.ASCII.GetBytes("---DUMMY CERT DATA---"))
						};
						requestDescription += " (UpdateCertificate)";
						break;
				}
				_operationIndex++;

				requestToSend = new Device.Main
				{
					Header = new Device.Header { DeviceId = ServerId, SequenceNumber = currentSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
					       DeviceRequest = new Device.DeviceRequest
					       {
						       CommandType = Device.DeviceCommandType.DeviceConfig,
						       Config = deviceConfigRequest
					       }
				};

				_logger.LogInformation("Attempting to send {RequestDescription} (Seq: {Seq}) to {DeviceId}",
						requestDescription, requestToSend.Header.SequenceNumber, TargetDeviceId);

				byte[] requestBytes = requestToSend.ToByteArray();
				_ = _tcpConnectionManager.SendCommandAsync(TargetDeviceId, requestBytes); // Fire and forget
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error occurred during periodic device config check.");
			}
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			_logger.LogInformation("Periodic Device Config Service stopping.");
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
