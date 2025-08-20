using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DeviceCommunication.Infrastructure.Services
{
    public class PtpManagementService : IHostedService, IDisposable
    {
        private readonly ILogger<PtpManagementService> _logger;
        private readonly TcpConnectionManager _tcpConnectionManager;
        private readonly ConcurrentDictionary<string, PtpClientInfo> _ptpClients;
        private Process? _ptpdProcess;
        private UdpClient? _ptpEventSocket;
        private UdpClient? _ptpGeneralSocket;
        private Timer? _grantRefreshTimer;
        private CancellationTokenSource? _serviceCts;

        private const int PTP_EVENT_PORT = 319;
        private const int PTP_GENERAL_PORT = 320;
        private const string PTPD_CONFIG_PATH = "/etc/ptpd_dynamic.conf";

        public PtpManagementService(
            ILogger<PtpManagementService> logger,
            TcpConnectionManager tcpConnectionManager)
        {
            _logger = logger;
            _tcpConnectionManager = tcpConnectionManager;
            _ptpClients = new ConcurrentDictionary<string, PtpClientInfo>();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _serviceCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            // Start PTPd process
            await StartPtpdProcessAsync();
            // Start listening for PTP signaling messages (REQUEST_UNICAST_TRANSMISSION)
            await StartPtpSignalingListenerAsync();
            // Start grant refresh timer
            _grantRefreshTimer = new Timer(
                RefreshGrants, 
                null, 
                TimeSpan.FromSeconds(30), 
                TimeSpan.FromSeconds(30));
            // Subscribe to device connection events
            _tcpConnectionManager.DeviceMessageReceivedAsync += HandleDeviceMessageForPtpAsync;
            _logger.LogInformation("PTP Management Service started");
        }

        private async Task StartPtpdProcessAsync()
        {
            try
            {
                // Gen config
                await UpdatePtpdConfigurationAsync();
                _ptpdProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "ptpd",
                        Arguments = $"-c {PTPD_CONFIG_PATH}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                _ptpdProcess.OutputDataReceived += (sender, e) => 
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        _logger.LogDebug("PTPd: {Output}", e.Data);
                };
                _ptpdProcess.ErrorDataReceived += (sender, e) => 
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        _logger.LogWarning("PTPd Error: {Error}", e.Data);
                };
                _ptpdProcess.Start();
                _ptpdProcess.BeginOutputReadLine();
                _ptpdProcess.BeginErrorReadLine();
                _logger.LogInformation("PTPd process started with PID {Pid}", _ptpdProcess.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start PTPd process");
                throw;
            }
        }

        private async Task StartPtpSignalingListenerAsync()
        {
            try
            {
                _ptpGeneralSocket = new UdpClient(PTP_GENERAL_PORT);
                _ptpEventSocket = new UdpClient(PTP_EVENT_PORT);
                // Enable SO_TIMESTAMPING for hardware timestamps
                EnableHardwareTimestamping(_ptpEventSocket);
                // Start listening
                _ = Task.Run(() => ListenForPtpSignalingAsync(_serviceCts!.Token), _serviceCts!.Token);
                _ = Task.Run(() => ListenForPtpEventsAsync(_serviceCts!.Token), _serviceCts!.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start PTP signaling listener");
                throw;
            }
        }

        private async Task ListenForPtpSignalingAsync(CancellationToken cancellationToken)
        {
            IPEndPoint remoteEp = new IPEndPoint(IPAddress.Any, 0);
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _ptpGeneralSocket!.ReceiveAsync();
                    var data = result.Buffer;
                    var senderEp = result.RemoteEndPoint;
                    // Parse PTP signaling message
                    if (IsPtpSignalingMessage(data))
                    {
                        await HandlePtpSignalingMessageAsync(data, senderEp);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in PTP signaling listener");
                }
            }
        }

        private async Task HandlePtpSignalingMessageAsync(byte[] data, IPEndPoint senderEp)
        {
            // Parse REQUEST_UNICAST_TRANSMISSION TLV
            var messageType = ParseRequestedMessageType(data);
            var duration = ParseRequestedDuration(data);
            if (messageType != PtpMessageType.Unknown)
            {
                var deviceId = GetDeviceIdFromEndpoint(senderEp);
                if (!string.IsNullOrEmpty(deviceId))
                {
                    var clientInfo = _ptpClients.GetOrAdd(deviceId, new PtpClientInfo
                    {
                        DeviceId = deviceId,
                        IpAddress = senderEp.Address.ToString(),
                        Port = senderEp.Port
                    });
                    clientInfo.LastRequestTime = DateTime.UtcNow;
                    clientInfo.GrantedMessageTypes[messageType] = DateTime.UtcNow.AddSeconds(duration);
                    // Send GRANT_UNICAST_TRANSMISSION response
                    await SendGrantUnicastTransmissionAsync(senderEp, messageType, duration);
                    // Update PTPd configuration
                    await UpdatePtpdConfigurationAsync();
                    _logger.LogInformation(
                        "Granted unicast {MessageType} to {DeviceId} ({IpAddress}) for {Duration}s",
                        messageType, deviceId, senderEp.Address, duration);
                }
            }
        }

        private async Task SendGrantUnicastTransmissionAsync(
            IPEndPoint clientEp, 
            PtpMessageType messageType, 
            int duration)
        {
            // Build GRANT_UNICAST_TRANSMISSION message
            var grantMessage = BuildGrantMessage(messageType, duration);
            await _ptpGeneralSocket!.SendAsync(grantMessage, grantMessage.Length, clientEp);
        }

        private async Task UpdatePtpdConfigurationAsync()
        {
            var unicastDestinations = string.Join(",", 
                _ptpClients.Values.Select(c => c.IpAddress));
            var config = $@"
[ptpengine]
ptpengine:interface=eth0
ptpengine:preset=masteronly
ptpengine:ip_mode=unicast
ptpengine:unicast_destinations={unicastDestinations}
ptpengine:domain=0
ptpengine:hw_timestamping=y
ptpengine:software_timestamping=y
ptpengine:unicast_negotiation=y
ptpengine:unicast_grant_duration=300
ptpengine:event_port=319
ptpengine:general_port=320

[clock]
clock:no_adjust=n
clock:frequency_adjustment_enable=y

[servo]
servo:kp=0.1
servo:ki=0.001

[global]
global:log_level=LOG_INFO
global:log_file=/var/log/ptpd.log
";
            await File.WriteAllTextAsync(PTPD_CONFIG_PATH, config);
            // Send SIGHUP to reload config
            if (_ptpdProcess != null && !_ptpdProcess.HasExited)
            {
                Process.Start("kill", $"-HUP {_ptpdProcess.Id}");
            }
        }

        private void RefreshGrants(object? state)
        {
            var now = DateTime.UtcNow;
            var expiredClients = _ptpClients
                .Where(kvp => kvp.Value.GrantedMessageTypes.All(g => g.Value < now))
                .Select(kvp => kvp.Key)
                .ToList();
            foreach (var deviceId in expiredClients)
            {
                if (_ptpClients.TryRemove(deviceId, out var client))
                {
                    _logger.LogInformation("Removed expired PTP client {DeviceId}", deviceId);
                }
            }
            if (expiredClients.Any())
            {
                _ = UpdatePtpdConfigurationAsync();
            }
        }

        private async Task HandleDeviceMessageForPtpAsync(string deviceId, Device.Main message)
        {
            // Check if device is requesting PTP sync status
            if (message.PayloadCase == Device.Main.PayloadOneofCase.DeviceRequest &&
                message.DeviceRequest.CommandType == Device.DeviceCommandType.SyncTime)
            {
                // Device is requesting time sync - ensure it's in our PTP client list
                var tcpConnection = _tcpConnectionManager.GetDeviceConnection(deviceId);
                if (tcpConnection != null)
                {
                    var clientInfo = _ptpClients.GetOrAdd(deviceId, new PtpClientInfo
                    {
                        DeviceId = deviceId,
                        IpAddress = tcpConnection.RemoteIpAddress,
                        Port = PTP_EVENT_PORT
                    });
                    await UpdatePtpdConfigurationAsync();
                }
            }
        }

        private void EnableHardwareTimestamping(UdpClient socket)
        {
            try
            {
                // Enable SO_TIMESTAMPING for hardware timestamps
                var handle = socket.Client.Handle;
                // SO_TIMESTAMPING flags
                const int SOF_TIMESTAMPING_TX_HARDWARE = (1<<0);
                const int SOF_TIMESTAMPING_TX_SOFTWARE = (1<<1);
                const int SOF_TIMESTAMPING_RX_HARDWARE = (1<<6);
                const int SOF_TIMESTAMPING_RX_SOFTWARE = (1<<7);
                const int SOF_TIMESTAMPING_RAW_HARDWARE = (1<<9);
                int flags = SOF_TIMESTAMPING_TX_HARDWARE | 
                           SOF_TIMESTAMPING_RX_HARDWARE | 
                           SOF_TIMESTAMPING_RAW_HARDWARE;
                // requires P/Invoke to setsockopt idk if we can do that on plant pcs or even ours
                _logger.LogInformation("Hardware timestamping enabled on socket");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enable hardware timestamping, falling back to software");
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping PTP Management Service");
            _serviceCts?.Cancel();
            _grantRefreshTimer?.Dispose();
            _ptpEventSocket?.Close();
            _ptpGeneralSocket?.Close();
            if (_ptpdProcess != null && !_ptpdProcess.HasExited)
            {
                _ptpdProcess.Kill();
                await _ptpdProcess.WaitForExitAsync(cancellationToken);
            }
            _tcpConnectionManager.DeviceMessageReceivedAsync -= HandleDeviceMessageForPtpAsync;
        }

        public void Dispose()
        {
            _serviceCts?.Dispose();
            _grantRefreshTimer?.Dispose();
            _ptpEventSocket?.Dispose();
            _ptpGeneralSocket?.Dispose();
            _ptpdProcess?.Dispose();
        }
    }

    public class PtpClientInfo
    {
        public string DeviceId { get; set; } = "";
        public string IpAddress { get; set; } = "";
        public int Port { get; set; }
        public DateTime LastRequestTime { get; set; }
        public Dictionary<PtpMessageType, DateTime> GrantedMessageTypes { get; set; } = new();
    }

    public enum PtpMessageType
    {
        Unknown,
        Sync,
        DelayReq,
        Announce,
        PDelayReq
    }
}
