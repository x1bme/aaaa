// DeviceCommunication.Infrastructure/Services/PtpManagementService.cs
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DeviceCommunication.Infrastructure.Services
{
    public class PtpManagementService : IHostedService, IDisposable
    {
        private readonly ILogger<PtpManagementService> _logger;
        private readonly ConcurrentDictionary<string, PtpClientInfo> _ptpClients;
        private Process? _ptpdProcess;
        private UdpClient? _ptpEventSocket;
        private UdpClient? _ptpGeneralSocket;
        private Timer? _grantRefreshTimer;
        private CancellationTokenSource? _serviceCts;

        private const int PTP_EVENT_PORT = 319;
        private const int PTP_GENERAL_PORT = 320;
        private const string PTPD_CONFIG_PATH = "/etc/ptpd_dynamic.conf";

        public PtpManagementService(ILogger<PtpManagementService> logger)
        {
            _logger = logger;
            _ptpClients = new ConcurrentDictionary<string, PtpClientInfo>();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _serviceCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            
            try
            {
                // Start PTPd process
                await StartPtpdProcessAsync();
                
                // Start listening for PTP signaling messages
                await StartPtpSignalingListenerAsync();
                
                // Start grant refresh timer
                _grantRefreshTimer = new Timer(
                    RefreshGrants, 
                    null, 
                    TimeSpan.FromSeconds(30), 
                    TimeSpan.FromSeconds(30));
                
                _logger.LogInformation("PTP Management Service started");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start PTP Management Service");
                throw;
            }
        }

        private async Task StartPtpdProcessAsync()
        {
            try
            {
                // Generate initial config
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
                
                // Start listening tasks
                _ = Task.Run(() => ListenForPtpSignalingAsync(_serviceCts!.Token), _serviceCts!.Token);
                _ = Task.Run(() => ListenForPtpEventsAsync(_serviceCts!.Token), _serviceCts!.Token);
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start PTP signaling listener");
                throw;
            }
        }

        private async Task ListenForPtpSignalingAsync(CancellationToken cancellationToken)
        {
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
                catch (ObjectDisposedException)
                {
                    // Socket was closed, exit gracefully
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in PTP signaling listener");
                }
            }
        }

        private async Task ListenForPtpEventsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _ptpEventSocket!.ReceiveAsync();
                    // Process PTP event messages if needed
                    // For now, we're just listening to keep the port open
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in PTP event listener");
                }
            }
        }

        private bool IsPtpSignalingMessage(byte[] data)
        {
            // Check if this is a PTP signaling message
            // PTP messages have specific header format
            if (data.Length < 34) return false;
            
            // Check message type (byte 0, bits 0-3)
            var messageType = data[0] & 0x0F;
            
            // Signaling message type is 0x0C
            return messageType == 0x0C;
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

        private PtpMessageType ParseRequestedMessageType(byte[] data)
        {
            // Parse TLV to find requested message type
            // This is a simplified implementation
            if (data.Length < 44) return PtpMessageType.Unknown;
            
            // TLV starts at byte 34 for signaling messages
            var tlvType = (data[34] << 8) | data[35];
            
            // REQUEST_UNICAST_TRANSMISSION TLV type is 0x0004
            if (tlvType == 0x0004)
            {
                // Message type is at byte 40
                var msgType = data[40];
                return msgType switch
                {
                    0x00 => PtpMessageType.Sync,
                    0x01 => PtpMessageType.DelayReq,
                    0x0B => PtpMessageType.Announce,
                    _ => PtpMessageType.Unknown
                };
            }
            
            return PtpMessageType.Unknown;
        }

        private int ParseRequestedDuration(byte[] data)
        {
            // Parse duration from TLV
            if (data.Length < 44) return 300; // Default duration
            
            // Duration is at bytes 41-44 (4 bytes)
            var duration = (data[41] << 24) | (data[42] << 16) | (data[43] << 8) | data[44];
            
            // Limit duration to reasonable values
            return Math.Min(Math.Max(duration, 60), 3600);
        }

        private string GetDeviceIdFromEndpoint(IPEndPoint endpoint)
        {
            // Map IP address to device ID
            // In a real implementation, you would look this up from your device registry
            // For now, we'll use a simple mapping based on IP
            return $"device_{endpoint.Address.ToString().Replace(".", "_")}";
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

        private byte[] BuildGrantMessage(PtpMessageType messageType, int duration)
        {
            // Build a basic PTP signaling message with GRANT_UNICAST_TRANSMISSION TLV
            var message = new byte[44];
            
            // PTP header (34 bytes)
            message[0] = 0x0C; // Signaling message type
            message[1] = 0x02; // PTP version
            
            // Message length (bytes 2-3)
            message[2] = 0x00;
            message[3] = 0x2C; // 44 bytes
            
            // Domain number (byte 4)
            message[4] = 0x00;
            
            // Flags (bytes 6-7)
            message[6] = 0x00;
            message[7] = 0x00;
            
            // Correction field (bytes 8-15)
            for (int i = 8; i < 16; i++)
                message[i] = 0x00;
            
            // Source port identity (bytes 20-29)
            // This should be your clock identity
            for (int i = 20; i < 28; i++)
                message[i] = 0xFF;
            
            // Sequence ID (bytes 30-31)
            message[30] = 0x00;
            message[31] = 0x00;
            
            // Control field (byte 32)
            message[32] = 0x05; // For signaling
            
            // Log message interval (byte 33)
            message[33] = 0x7F;
            
            // TLV: GRANT_UNICAST_TRANSMISSION (10 bytes)
            // TLV type (bytes 34-35)
            message[34] = 0x00;
            message[35] = 0x05; // GRANT_UNICAST_TRANSMISSION
            
            // TLV length (bytes 36-37)
            message[36] = 0x00;
            message[37] = 0x06;
            
            // Message type (byte 38)
            message[38] = messageType switch
            {
                PtpMessageType.Sync => 0x00,
                PtpMessageType.DelayReq => 0x01,
                PtpMessageType.Announce => 0x0B,
                _ => 0x00
            };
            
            // Log inter message period (byte 39)
            message[39] = 0x00;
            
            // Duration (bytes 40-43)
            message[40] = (byte)(duration >> 24);
            message[41] = (byte)(duration >> 16);
            message[42] = (byte)(duration >> 8);
            message[43] = (byte)duration;
            
            return message;
        }

        private async Task UpdatePtpdConfigurationAsync()
        {
            var unicastDestinations = string.Join(",", 
                _ptpClients.Values.Select(c => c.IpAddress));
            
            var config = $@"[ptpengine]
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
global:log_file=/var/log/ptpd.log";

            await File.WriteAllTextAsync(PTPD_CONFIG_PATH, config);
            
            // Send SIGHUP to reload config
            if (_ptpdProcess != null && !_ptpdProcess.HasExited)
            {
                try
                {
                    Process.Start("kill", $"-HUP {_ptpdProcess.Id}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send SIGHUP to PTPd");
                }
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
                if (_ptpClients.TryRemove(deviceId, out _))
                {
                    _logger.LogInformation("Removed expired PTP client {DeviceId}", deviceId);
                }
            }
            
            if (expiredClients.Any())
            {
                _ = UpdatePtpdConfigurationAsync();
            }
        }

        private void EnableHardwareTimestamping(UdpClient socket)
        {
            try
            {
                // This would require P/Invoke to setsockopt for full implementation
                // For now, we'll rely on PTPd to handle hardware timestamping
                _logger.LogInformation("Hardware timestamping configuration delegated to PTPd");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to configure hardware timestamping");
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
