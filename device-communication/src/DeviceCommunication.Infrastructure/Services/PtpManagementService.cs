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
        
        // Use a writable path instead of /etc/
        private readonly string PTPD_CONFIG_PATH;

        public PtpManagementService(ILogger<PtpManagementService> logger)
        {
            _logger = logger;
            _ptpClients = new ConcurrentDictionary<string, PtpClientInfo>();
            
            // Use a writable directory - either temp or current directory
            var configDir = Environment.GetEnvironmentVariable("PTPD_CONFIG_DIR") ?? Path.GetTempPath();
            PTPD_CONFIG_PATH = Path.Combine(configDir, "ptpd_dynamic.conf");
            
            _logger.LogInformation("PTPd config will be written to: {ConfigPath}", PTPD_CONFIG_PATH);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _serviceCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            
            try
            {
                if (!IsPtpdAvailable())
                {
                    _logger.LogWarning("PTPd is not available on this system. PTP management will be disabled.");
                    return;
                }
                
                await StartPtpdProcessAsync();
                
                // Add this delay and logging
                await Task.Delay(3000, cancellationToken); // Wait for PTPd to fully start
                
                await LogPtpStartupStatus();
                
                _grantRefreshTimer = new Timer(
                    RefreshGrants, 
                    null, 
                    TimeSpan.FromSeconds(30), 
                    TimeSpan.FromSeconds(30));
                
                _logger.LogInformation("PTP Management Service started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start PTP Management Service");
                _logger.LogWarning("Application will continue without PTP management");
            }
        }

        private async Task LogPtpStartupStatus()
        {
            try
            {
                _logger.LogInformation("=== PTP Master Status ===");
                
                var networkInterface = await DetectNetworkInterfaceAsync();
                _logger.LogInformation("PTP Interface: {Interface}", networkInterface);
                
                var ipAddress = await GetInterfaceIpAddress(networkInterface);
                if (!string.IsNullOrEmpty(ipAddress))
                {
                    _logger.LogInformation("PTP Master IP: {IpAddress}", ipAddress);
                    _logger.LogInformation("🎯 Clients should connect to: {IpAddress}", ipAddress);
                }
                
                // Check if PTPd process is running
                if (_ptpdProcess != null && !_ptpdProcess.HasExited)
                {
                    _logger.LogInformation("✅ PTPd Master process running (PID: {Pid})", _ptpdProcess.Id);
                }
                else
                {
                    _logger.LogWarning("✗ PTPd Master process not running");
                }
                
                // Check ports with a delay to ensure PTPd has bound them
                await Task.Delay(2000);
                var port319Bound = await IsPortBound(319);
                var port320Bound = await IsPortBound(320);
                
                _logger.LogInformation("PTP Event Port 319: {Status}", port319Bound ? "✅ BOUND" : "❌ NOT BOUND");
                _logger.LogInformation("PTP General Port 320: {Status}", port320Bound ? "✅ BOUND" : "❌ NOT BOUND");
                
                if (port319Bound && port320Bound && _ptpdProcess != null && !_ptpdProcess.HasExited)
                {
                    _logger.LogInformation("🚀 PTP Master is READY for client connections!");
                    _logger.LogInformation("📡 Test with: sudo ptpd2 -g -i <interface> -u {IpAddress}", ipAddress ?? "YOUR_IP");
                }
                else
                {
                    _logger.LogError("❌ PTP Master is not ready - check process and port status above");
                }
                
                _logger.LogInformation("========================");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log PTP startup status");
            }
        }

        private async Task<string?> GetInterfaceIpAddress(string interfaceName)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "ip",
                    Arguments = $"addr show {interfaceName}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });

                if (process != null)
                {
                    await process.WaitForExitAsync();
                    var output = await process.StandardOutput.ReadToEndAsync();
                    
                    // Look for inet line
                    var lines = output.Split('\n');
                    foreach (var line in lines)
                    {
                        if (line.Trim().StartsWith("inet ") && !line.Contains("127.0.0.1"))
                        {
                            var parts = line.Trim().Split(' ');
                            if (parts.Length >= 2)
                            {
                                var ipWithMask = parts[1];
                                var ip = ipWithMask.Split('/')[0];
                                return ip;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not get IP address for interface {Interface}", interfaceName);
            }
            return null;
        }

        private async Task<bool> IsPortBound(int port)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "netstat",
                    Arguments = "-ulnp",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });

                if (process != null)
                {
                    await process.WaitForExitAsync();
                    var output = await process.StandardOutput.ReadToEndAsync();
                    return output.Contains($":{port} ");
                }
            }
            catch
            {
                // Fallback: try to bind to the port briefly
                try
                {
                    using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    socket.Bind(new IPEndPoint(IPAddress.Any, port));
                    return false; // Port was available (not bound by PTPd)
                }
                catch
                {
                    return true; // Port was in use (probably by PTPd)
                }
            }
            return false;
        }

        private bool IsPtpdAvailable()
        {
            try
            {
                // Check for ptpd
                var ptpdResult = CheckPtpdCommand("ptpd");
                if (ptpdResult.available)
                {
                    _logger.LogInformation("Found ptpd: {Version}", ptpdResult.version);
                    return true;
                }
                
                // Check for ptpd2
                var ptpd2Result = CheckPtpdCommand("ptpd2");
                if (ptpd2Result.available)
                {
                    _logger.LogInformation("Found ptpd2: {Version}", ptpd2Result.version);
                    _logger.LogWarning("Using ptpd2, but configuration expects 'ptpd' command");
                    return true;
                }
                
                _logger.LogError("Neither 'ptpd' nor 'ptpd2' commands are available");
                _logger.LogInformation("Install PTPd with: sudo apt-get install ptpd");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking PTPd availability");
                return false;
            }
        }

        private (bool available, string version) CheckPtpdCommand(string command)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });
                
                if (process != null)
                {
                    process.WaitForExit(3000); // 3 second timeout
                    if (process.ExitCode == 0)
                    {
                        var output = process.StandardOutput.ReadToEnd();
                        return (true, output.Trim());
                    }
                }
            }
            catch
            {
                // Command not found or other error
            }
            
            // Try alternative check
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = command,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });
                
                if (process != null)
                {
                    process.WaitForExit(3000);
                    if (process.ExitCode == 0)
                    {
                        var path = process.StandardOutput.ReadToEnd().Trim();
                        return (true, $"Found at {path}");
                    }
                }
            }
            catch
            {
                // Command not found
            }
            
            return (false, "");
        }
        private async Task StartPtpdProcessAsync()
        {
            try
            {
                await UpdatePtpdConfigurationAsync();
                
                if (!File.Exists(PTPD_CONFIG_PATH))
                {
                    throw new FileNotFoundException($"Failed to create PTPd config file at {PTPD_CONFIG_PATH}");
                }

                _logger.LogInformation("PTPd configuration file contents:");
                var configContent = await File.ReadAllTextAsync(PTPD_CONFIG_PATH);
                foreach (var line in configContent.Split('\n'))
                {
                    if (!string.IsNullOrWhiteSpace(line))
                        _logger.LogInformation("  {Line}", line.Trim());
                }
                
                _ptpdProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "ptpd",
                        Arguments = $"-c {PTPD_CONFIG_PATH}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    },
                    EnableRaisingEvents = true
                };
                
                // Track if PTPd has successfully started
                bool ptpdStartedSuccessfully = false;
                
                _ptpdProcess.OutputDataReceived += (sender, e) => 
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        LogPtpdMessage(e.Data, false);
                        
                        // Check for successful startup indicators
                        if (e.Data.Contains("PTPDv2 started successfully") || 
                            e.Data.Contains("Now in state: PTP_LISTENING"))
                        {
                            ptpdStartedSuccessfully = true;
                        }
                    }
                };
                
                _ptpdProcess.ErrorDataReceived += (sender, e) => 
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        LogPtpdMessage(e.Data, true);
                        
                        // Check for successful startup indicators (PTPd sends info to stderr too)
                        if (e.Data.Contains("PTPDv2 started successfully") || 
                            e.Data.Contains("Now in state: PTP_LISTENING") ||
                            e.Data.Contains("Configuration OK"))
                        {
                            ptpdStartedSuccessfully = true;
                        }
                    }
                };

                _ptpdProcess.Exited += (sender, e) =>
                {
                    var process = sender as Process;
                    if (process != null)
                    {
                        _logger.LogError("🚨 PTPd process EXITED! Exit code: {ExitCode}", process.ExitCode);
                    }
                };
                
                _logger.LogInformation("Starting PTPd with command: ptpd -c {ConfigPath}", PTPD_CONFIG_PATH);
                
                _ptpdProcess.Start();
                _ptpdProcess.BeginOutputReadLine();
                _ptpdProcess.BeginErrorReadLine();
                
                _logger.LogInformation("PTPd process started with PID {Pid}", _ptpdProcess.Id);
                
                // Wait for PTPd to initialize and check success
                await Task.Delay(5000);
                
                if (_ptpdProcess.HasExited)
                {
                    _logger.LogError("❌ PTPd process exited after start! Exit code: {ExitCode}", _ptpdProcess.ExitCode);
                }
                else if (ptpdStartedSuccessfully)
                {
                    _logger.LogInformation("🎉 PTPd started successfully and is in LISTENING state!");
                    _logger.LogInformation("✅ PTP Master is ready to accept client connections");
                }
                else
                {
                    _logger.LogInformation("ℹ️ PTPd process is running (PID: {Pid}) - waiting for startup confirmation...", _ptpdProcess.Id);
                }
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start PTPd process");
                throw;
            }
        }

        private void LogPtpdMessage(string message, bool fromStderr)
        {
            // Parse PTPd log levels from the message
            // PTPd format: timestamp ptpd2[pid].module (level) message
            
            if (string.IsNullOrEmpty(message)) return;
            
            // Extract log level from PTPd message
            var logLevel = ExtractPtpdLogLevel(message);
            var cleanMessage = message.Trim();
            
            switch (logLevel.ToLower())
            {
                case "error":
                case "critical":
                    _logger.LogError("PTPd: {Message}", cleanMessage);
                    break;
                case "warning":
                case "warn":
                    _logger.LogWarning("PTPd: {Message}", cleanMessage);
                    break;
                case "notice":
                case "info":
                    _logger.LogInformation("PTPd: {Message}", cleanMessage);
                    break;
                case "debug":
                    _logger.LogDebug("PTPd: {Message}", cleanMessage);
                    break;
                default:
                    // If we can't determine the level, use Info for stdout, Warning for stderr
                    if (fromStderr)
                        _logger.LogInformation("PTPd: {Message}", cleanMessage);  // Most PTPd info comes via stderr
                    else
                        _logger.LogInformation("PTPd: {Message}", cleanMessage);
                    break;
            }
        }
        private string ExtractPtpdLogLevel(string message)
        {
            // Look for pattern like "(info)" or "(notice)" in PTPd log messages
            var match = System.Text.RegularExpressions.Regex.Match(message, @"\((\w+)\)");
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value;
            }
            return "info"; // default
        }
        // Add this diagnostic method
        private async Task DiagnosePtpdIssue()
        {
            try
            {
                // Try running ptpd with verbose output to see what's wrong
                using var diagProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = "ptpd",
                    Arguments = $"-c {PTPD_CONFIG_PATH} ",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });

                if (diagProcess != null)
                {
                    // Give it a few seconds to show error messages
                    var outputTask = diagProcess.StandardOutput.ReadToEndAsync();
                    var errorTask = diagProcess.StandardError.ReadToEndAsync();
                    
                    var completed = await Task.WhenAny(
                        Task.WhenAll(outputTask, errorTask),
                        Task.Delay(5000) // 5 second timeout
                    );
                    
                    if (completed != Task.WhenAll(outputTask, errorTask))
                    {
                        // Timeout - kill the process
                        diagProcess.Kill();
                        _logger.LogWarning("PTPd diagnostic process timed out");
                    }
                    else
                    {
                        var stdout = await outputTask;
                        var stderr = await errorTask;
                        
                        if (!string.IsNullOrEmpty(stdout))
                        {
                            _logger.LogInformation("PTPd diagnostic stdout: {Output}", stdout);
                        }
                        if (!string.IsNullOrEmpty(stderr))
                        {
                            _logger.LogError("PTPd diagnostic stderr: {Error}", stderr);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to run PTPd diagnostic");
            }
        }

        // Also add this method to check what's actually using the ports
        private async Task CheckWhatIsUsingPorts()
        {
            try
            {
                _logger.LogInformation("Checking what processes are using PTP ports...");
                
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "sudo",
                    Arguments = "lsof -i :319 -i :320",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });

                if (process != null)
                {
                    await process.WaitForExitAsync();
                    var output = await process.StandardOutput.ReadToEndAsync();
                    
                    if (!string.IsNullOrEmpty(output))
                    {
                        _logger.LogInformation("Processes using PTP ports:");
                        foreach (var line in output.Split('\n'))
                        {
                            if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("COMMAND"))
                            {
                                _logger.LogInformation("  {Line}", line);
                            }
                        }
                    }
                    else
                    {
                        _logger.LogInformation("No processes found using PTP ports 319/320");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not check port usage");
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
            try
            {
                var unicastDestinations = string.Join(",", 
                    _ptpClients.Values.Select(c => c.IpAddress));
                
                // Detect the network interface - try to find a valid one
                var networkInterface = await DetectNetworkInterfaceAsync();
                
                // Use minimal, working PTPd configuration based on official examples
                var configBuilder = new StringBuilder();
                
                // Required basic settings
                configBuilder.AppendLine("[ptpengine]");
                configBuilder.AppendLine($"interface={networkInterface}");
                configBuilder.AppendLine("preset=masteronly");
                configBuilder.AppendLine("ip_mode=unicast");
                if (!string.IsNullOrEmpty(unicastDestinations))
                {
                    configBuilder.AppendLine($"unicast_destinations={unicastDestinations}");
                }
                else
                {
                    // Use a placeholder/dummy IP when no real destinations exist yet
                    // This allows PTPd to start in unicast mode and be ready for real clients
                    configBuilder.AppendLine("unicast_destinations=192.168.86.222");
                }
                configBuilder.AppendLine("domain=0");
                configBuilder.AppendLine("delay_mechanism=E2E");
                configBuilder.AppendLine();
                
                configBuilder.AppendLine("[clock]");
                configBuilder.AppendLine("no_adjust=n");
                configBuilder.AppendLine();
                
                configBuilder.AppendLine("[servo]");
                configBuilder.AppendLine("kp=0.1");
                configBuilder.AppendLine("ki=0.001");
                configBuilder.AppendLine();
                
                configBuilder.AppendLine("[global]");
                configBuilder.AppendLine("log_level=LOG_INFO");
                configBuilder.AppendLine("foreground=y");
                configBuilder.AppendLine("verbose_foreground=y");
                configBuilder.AppendLine("ignore_lock=y");
                
                var config = configBuilder.ToString();

                // Ensure the directory exists
                var configDir = Path.GetDirectoryName(PTPD_CONFIG_PATH);
                if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }

                await File.WriteAllTextAsync(PTPD_CONFIG_PATH, config);
                _logger.LogDebug("Updated PTPd configuration at {ConfigPath}", PTPD_CONFIG_PATH);
                
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update PTPd configuration at {ConfigPath}", PTPD_CONFIG_PATH);
                throw;
            }
        }

        private async Task<string> DetectNetworkInterfaceAsync()
        {
            // Try environment variable first
            var envInterface = Environment.GetEnvironmentVariable("PTP_INTERFACE");
            if (!string.IsNullOrEmpty(envInterface))
            {
                if (await IsInterfaceValidAsync(envInterface))
                {
                    return envInterface;
                }
                else
                {
                    _logger.LogWarning("Configured interface {Interface} does not exist", envInterface);
                }
            }

            // Try to detect automatically
            var commonInterfaces = new[] { "eth0", "enp0s3", "ens33", "wlan0", "enp3s0" };
            
            foreach (var iface in commonInterfaces)
            {
                if (await IsInterfaceValidAsync(iface))
                {
                    _logger.LogInformation("Auto-detected network interface: {Interface}", iface);
                    return iface;
                }
            }

            // Fallback to first available interface
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "ip",
                    Arguments = "link show",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });

                if (process != null)
                {
                    await process.WaitForExitAsync();
                    var output = await process.StandardOutput.ReadToEndAsync();

                    // Parse output to find first non-loopback interface
                    var lines = output.Split('\n');
                    foreach (var line in lines)
                    {
                        if (line.Contains(": ") && !line.Contains("lo:") && line.Contains("state UP"))
                        {
                            var parts = line.Split(": ");
                            if (parts.Length > 1)
                            {
                                var interfaceName = parts[1].Split('@')[0]; // Handle interfaces like "eth0@if2"
                                _logger.LogInformation("Found active interface: {Interface}", interfaceName);
                                return interfaceName;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to auto-detect network interface");
            }

            // Final fallback
            _logger.LogWarning("Could not detect network interface, using 'lo' as fallback");
            return "lo";
        }

        private async Task<bool> IsInterfaceValidAsync(string interfaceName)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "ip",
                    Arguments = $"link show {interfaceName}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });

                if (process != null)
                {
                    await process.WaitForExitAsync();
                    return process.ExitCode == 0;
                }
            }
            catch
            {
                // Interface check failed
            }
            return false;
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
            
            // Clean up config file
            try
            {
                if (File.Exists(PTPD_CONFIG_PATH))
                {
                    File.Delete(PTPD_CONFIG_PATH);
                    _logger.LogDebug("Cleaned up PTPd config file at {ConfigPath}", PTPD_CONFIG_PATH);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up PTPd config file");
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
