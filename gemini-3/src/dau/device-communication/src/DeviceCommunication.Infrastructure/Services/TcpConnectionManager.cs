using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Device;
using Google.Protobuf;

namespace DeviceCommunication.Infrastructure.Services
{
        public class ManagedDeviceConnection : IDisposable
        {
                public string DeviceId { get; }
                public TcpClient Client { get; }
                public NetworkStream Stream { get; }
                public CancellationTokenSource Cts { get; }
                private bool _disposed = false;
                private readonly object _disposeLock = new object();

                public ManagedDeviceConnection(string deviceId, TcpClient client)
                {
                        DeviceId = deviceId;
                        Client = client;
                        Client.NoDelay = true;
                        Stream = client.GetStream();
                        Cts = new CancellationTokenSource();
                }

                public void Dispose()
                {
                        Dispose(true);
                        GC.SuppressFinalize(this);
                }

                protected virtual void Dispose(bool disposing)
                {
                        lock (_disposeLock)
                        {
                                if (_disposed) return;
                                if (disposing)
                                {
                                        if (!Cts.IsCancellationRequested)
                                        {
                                                try { Cts.Cancel(); } catch (ObjectDisposedException) { }
                                        }
                                        try { Cts.Dispose(); } catch (ObjectDisposedException) { }
                                        try { Stream?.Close(); Stream?.Dispose(); } catch { }
                                        try { Client?.Close(); Client?.Dispose(); } catch { }
                                }
                                _disposed = true;
                        }
                }
        }

        public class TcpConnectionManager : IDisposable
        {
                private readonly ILoggerFactory _loggerFactory; // Kept if other parts might use it
                private readonly ILogger<TcpConnectionManager> _logger;
                private readonly TcpListener _listener;
                private bool _isRunning;
                private readonly CancellationTokenSource _serverShutdownCts;
                private readonly ConcurrentDictionary<string, ManagedDeviceConnection> _activeConnections = new();
                private bool _disposed = false;
                // Sequence counter for NEW commands initiated by the server (e.g., auto StartCapture)
                private static long _serverSequenceCounter = 100000; 

                // Event for other services (like Orchestrator) to subscribe to all received messages
                public event Func<string /*deviceId*/, Device.Main /*message*/, Task>? DeviceMessageReceivedAsync;


                public TcpConnectionManager(
                                ILoggerFactory loggerFactory, // For creating loggers if needed by spawned tasks
                                string ipAddress = "0.0.0.0",
                                int port = 12345)
                {
                        _loggerFactory = loggerFactory;
                        _logger = loggerFactory.CreateLogger<TcpConnectionManager>();
                        _listener = new TcpListener(IPAddress.Parse(ipAddress), port);
                        _serverShutdownCts = new CancellationTokenSource();
                }

                public async Task StartAsync()
                {
                        if (_isRunning) return;
                        _isRunning = true;
                        try { _listener.Start(); }
                        catch (SocketException ex)
                        {
                                _logger.LogError(ex, "Failed to start TCP listener on port {Port}.", ((IPEndPoint)_listener.LocalEndpoint).Port);
                                _isRunning = false; return;
                        }
                        _logger.LogInformation("TCP listener started on {Address}:{Port}", ((IPEndPoint)_listener.LocalEndpoint).Address, ((IPEndPoint)_listener.LocalEndpoint).Port);

                        try
                        {
                                while (!_serverShutdownCts.Token.IsCancellationRequested)
                                {
                                        var client = await _listener.AcceptTcpClientAsync(_serverShutdownCts.Token);
                                        _ = HandleClientAsync(client); // Fire and forget client handling
                                }
                        }
                        catch (OperationCanceledException) { _logger.LogInformation("TCP listener stopping as requested."); }
                        catch (Exception ex) { _logger.LogError(ex, "TCP listener encountered an unhandled exception."); }
                        finally
                        {
                                try { _listener.Stop(); } catch (Exception ex) { _logger.LogWarning(ex, "Exception during listener stop."); }
                                _isRunning = false;
                                _logger.LogInformation("TCP listener stopped.");
                        }
                }

                private async Task HandleClientAsync(TcpClient client)
                {
                        string? deviceId = null;
                        ManagedDeviceConnection? managedConnection = null;
                        var remoteEndPoint = client.Client?.RemoteEndPoint?.ToString() ?? "Unknown";
                        _logger.LogInformation("Client connected from {RemoteEndPoint}", remoteEndPoint);
                        NetworkStream? initialStream = null;

                        try
                        {
                                initialStream = client.GetStream();
                                // For delimited format, we'll use a CancellationToken for the initial parse.
                                using var identifyCts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // Timeout for initial message
                                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(identifyCts.Token, _serverShutdownCts.Token);

                                Device.Main initialMessage;
                                try
                                {
                                        // ParseDelimitedFrom reads the varint length prefix then the message.
                                        initialMessage = await Task.Run(() => Device.Main.Parser.ParseDelimitedFrom(initialStream), linkedCts.Token);
                                }
                                catch (OperationCanceledException) when (identifyCts.IsCancellationRequested)
                                {
                                        _logger.LogWarning("Client from {RemoteEndPoint} timed out before sending complete initial message.", remoteEndPoint);
                                        client.Dispose(); return;
                                }
                                catch (Google.Protobuf.InvalidProtocolBufferException ex) when (ex.InnerException is EndOfStreamException)
                                {
                                        _logger.LogWarning("Client from {RemoteEndPoint} disconnected before sending complete initial message (EOS).", remoteEndPoint);
                                        client.Dispose(); return;
                                }
                                catch (IOException ioEx)
                                {
                                        _logger.LogWarning(ioEx, "Client from {RemoteEndPoint} disconnected or stream error during initial message read.", remoteEndPoint);
                                        client.Dispose(); return;
                                }


                                if (initialMessage == null) // Should be covered by exceptions, but as a safeguard
                                {
                                        _logger.LogWarning("Client from {RemoteEndPoint} sent null initial message or disconnected.", remoteEndPoint);
                                        client.Dispose(); return;
                                }

                                if (initialMessage.Header == null || string.IsNullOrEmpty(initialMessage.Header.DeviceId))
                                {
                                        _logger.LogWarning("Identification from {RemoteEndPoint} missing header or device ID.", remoteEndPoint);
                                        client.Dispose(); return;
                                }
                                deviceId = initialMessage.Header.DeviceId;
                                _logger.LogInformation("Received identification from Device ID: {DeviceId} ({RemoteEndPoint})", deviceId, remoteEndPoint);

                                managedConnection = new ManagedDeviceConnection(deviceId, client);
                                if (_activeConnections.TryGetValue(deviceId, out var existingConnection))
                                {
                                        _logger.LogWarning("Device {DeviceId} reconnected. Closing previous connection.", deviceId);
                                        existingConnection.Dispose(); // This will cancel its Cts
                                        await Task.Delay(100, _serverShutdownCts.Token); 
                                }
                                _activeConnections[deviceId] = managedConnection;
                                _logger.LogInformation("Device {DeviceId} registered. Starting processing loop.", deviceId);

                                // Process the very first message that might have been the identification
                                _ = ProcessSingleMessageAsync(managedConnection, initialMessage); 
                                // Start loop for subsequent messages
                                _ = ProcessIncomingDeviceDataAsync(managedConnection); // Pass the managedConnection which has its own Cts
                        }
                        catch (OperationCanceledException) when (_serverShutdownCts.IsCancellationRequested)
                        {
                                _logger.LogInformation("Server shutting down, client handling aborted for {RemoteEndPoint}.", remoteEndPoint);
                                if (managedConnection != null) managedConnection.Dispose(); else try { client?.Dispose(); } catch { }
                                try { initialStream?.Dispose(); } catch { }
                        }
                        catch (Exception ex)
                        {
                                _logger.LogError(ex, "Error during initial handling of client {RemoteEndPoint} (ID: {DeviceId})", remoteEndPoint, deviceId ?? "N/A");
                                if (managedConnection != null) { _activeConnections.TryRemove(KeyValuePair.Create(deviceId!, managedConnection)); managedConnection.Dispose(); }
                                else if (client?.Connected ?? false) { try { client.Dispose(); } catch { } }
                                try { initialStream?.Dispose(); } catch { }
                        }
                }

                // Helper to process a single, already parsed message (like the identification message)
                private async Task ProcessSingleMessageAsync(ManagedDeviceConnection connection, Device.Main message)
                {
                        try
                        {
                                await HandleSpecificMessageLogic(connection, message);

                                // Fire event for orchestrator and other services
                                if (DeviceMessageReceivedAsync != null)
                                {
                                        // Using Task.Run to avoid blocking and to catch exceptions from subscribers
                                        _ = Task.Run(async () =>
                                                        {
                                                        try
                                                        {
                                                        await DeviceMessageReceivedAsync(connection.DeviceId, message);
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                        _logger.LogError(ex, "Error invoking DeviceMessageReceivedAsync for Device {DeviceId}, Seq {Sequence} (single message handler)", connection.DeviceId, message.Header?.SequenceNumber);
                                                        }
                                                        }, connection.Cts.Token); // Pass the connection's cancellation token
                                }
                        }
                        catch (Exception ex)
                        {
                                _logger.LogError(ex, "Error in ProcessSingleMessageAsync for Device {DeviceId}", connection.DeviceId);
                        }
                }


                private async Task ProcessIncomingDeviceDataAsync(ManagedDeviceConnection connection)
                {
                        _logger.LogDebug("Starting data processing loop for Device {DeviceId}", connection.DeviceId);
                        try
                        {
                                // The connection.Stream is the NetworkStream from the ManagedDeviceConnection
                                while (!connection.Cts.Token.IsCancellationRequested && (connection.Client?.Connected ?? false))
                                {
                                        Device.Main receivedMsg;
                                        try
                                        {
                                                // ParseDelimitedFrom will read the varint length prefix and then the message data.
                                                // It will block until a full message is received or an error/EOS occurs.
                                                // We use Task.Run to make it cancellable with the connection's CancellationToken.
                                                receivedMsg = await Task.Run(() => Device.Main.Parser.ParseDelimitedFrom(connection.Stream), connection.Cts.Token);

                                                if (receivedMsg == null) // Should indicate EOS or error handled by exceptions below
                                                {
                                                        _logger.LogInformation("Device {DeviceId} disconnected (ParseDelimitedFrom returned null).", connection.DeviceId);
                                                        break;
                                                }
                                        }
                                        catch (OperationCanceledException) when (connection.Cts.Token.IsCancellationRequested) 
                                        { 
                                                throw; // Re-throw to be caught by the outer cancellation handler
                                        }
                                        catch (Google.Protobuf.InvalidProtocolBufferException ex) when (ex.InnerException is EndOfStreamException)
                                        {
                                                _logger.LogInformation("Device {DeviceId} disconnected gracefully (EOS during parse).", connection.DeviceId);
                                                break;
                                        }
                                        catch (Google.Protobuf.InvalidProtocolBufferException protoEx)
                                        {
                                                _logger.LogError(protoEx, "Failed to parse Protobuf from {DeviceId}.", connection.DeviceId);
                                                continue; // Try to recover by reading the next message, though stream might be corrupted
                                        }
                                        catch (IOException ioEx) // Catches socket errors, disconnections
                                        {
                                                _logger.LogInformation(ioEx, "IOException (e.g. connection reset) for {DeviceId}. Assuming disconnect.", connection.DeviceId);
                                                break;
                                        }

                                        try
                                        {
                                                await HandleSpecificMessageLogic(connection, receivedMsg);

                                                if (DeviceMessageReceivedAsync != null)
                                                {
                                                        _ = Task.Run(async () =>
                                                                        {
                                                                        try { await DeviceMessageReceivedAsync(connection.DeviceId, receivedMsg); }
                                                                        catch (Exception ex) { _logger.LogError(ex, "Error invoking DeviceMessageReceivedAsync for Device {DeviceId}, Seq {Sequence}", connection.DeviceId, receivedMsg.Header?.SequenceNumber); }
                                                                        }, connection.Cts.Token);
                                                }
                                        }
                                        catch (OperationCanceledException) when (connection.Cts.Token.IsCancellationRequested) { throw; }
                                        catch (Exception procEx) { _logger.LogError(procEx, "Error processing message from {DeviceId}", connection.DeviceId); }
                                }
                        }
                        catch (OperationCanceledException) when (connection.Cts.Token.IsCancellationRequested || _serverShutdownCts.IsCancellationRequested)
                        { _logger.LogInformation("Processing loop cancelled for Device {DeviceId}.", connection.DeviceId); }
                        // Removed the specific IOException for SocketException as the general IOException above should catch it.
                        catch (Exception ex) { _logger.LogError(ex, "Unhandled error in processing loop for {DeviceId}.", connection.DeviceId); }
                        finally
                        {
                                _logger.LogInformation("Stopping data processing and cleaning up for Device {DeviceId}.", connection.DeviceId);
                                if (_activeConnections.TryRemove(KeyValuePair.Create(connection.DeviceId, connection)))
                                {
                                        _logger.LogDebug("Removed {DeviceId} from active connections.", connection.DeviceId);
                                }
                                connection.Dispose();
                        }
                }

                // Centralized specific message logic
                private async Task HandleSpecificMessageLogic(ManagedDeviceConnection connection, Device.Main receivedMsg)
                {
                        // Generic log for any message received by TCM at this level
                        _logger.LogInformation("TCM Raw Rcv: Device {DevId}, Seq {Seq}, Type {Type}, InnerType {Inner}", 
                                        connection.DeviceId, receivedMsg.Header?.SequenceNumber, receivedMsg.PayloadCase, GetInnerPayloadCase(receivedMsg));

                        // --- Specific Handling / Logging for Firmware Team Observability ---
                        if (receivedMsg.PayloadCase == Device.Main.PayloadOneofCase.DataResponse &&
                                        receivedMsg.DataResponse.CommandResponsePayloadCase == Device.DataResponse.CommandResponsePayloadOneofCase.ManageData &&
                                        receivedMsg.DataResponse.ManageData.OperationResponsePayloadCase == Device.ManageDataResponse.OperationResponsePayloadOneofCase.Get)
                        {
                                var getDataResp = receivedMsg.DataResponse.ManageData.Get;
                                _logger.LogInformation("--> TCM: Processing GetDataResponse for Dataset {DatasetID}. EnvelopeSeq: {EnvSeq}, FirstChunkInResp: {FirstChunkSeq}, TotalChunksInDS: {TotalChunks}, ChunksInThisMsg: {Count}",
                                                getDataResp.DatasetId, receivedMsg.Header.SequenceNumber,
                                                getDataResp.FirstChunkSequenceNumberInThisResponse, getDataResp.TotalChunksInDataset, getDataResp.DataChunks.Count);

                                Console.WriteLine($"[FIRMWARE_DATA_STREAM] Device: {connection.DeviceId}, DatasetID: {getDataResp.DatasetId}, TotalChunksInDataset: {getDataResp.TotalChunksInDataset}");
                                foreach (var chunk in getDataResp.DataChunks)
                                {
                                        int numChannels = Math.Max(1, (int)chunk.NumChannels);
                                        int samplesInChunk = chunk.RawAdcValues.Count / numChannels;
                                        Console.WriteLine($"  [CHUNK] Seq#: {chunk.SequenceNumber}, Channels: {chunk.NumChannels}, Samples: {samplesInChunk}, RateHz: {chunk.SampleRateHz}, StartTimeNs: {chunk.StartTimestampNs}");


                                        if (chunk.RawAdcValues.Any())
                                        {
                                                const int maxValuesToLogPerEnd = 5;
                                                var valuesToLog = new List<int>(); // Changed from sint32 to int

                                                const int separatorMarker = int.MinValue; // Or int.MinValue + 1 if you prefer
                                                if (chunk.RawAdcValues.Count <= maxValuesToLogPerEnd * 2)
                                                {
                                                        valuesToLog.AddRange(chunk.RawAdcValues);
                                                }
                                                else
                                                {
                                                        valuesToLog.AddRange(chunk.RawAdcValues.Take(maxValuesToLogPerEnd));
                                                        // Using a very specific negative number as a marker:
                                                        valuesToLog.AddRange(Enumerable.Repeat(0, 3).Select(_ => separatorMarker));
                                                        valuesToLog.AddRange(chunk.RawAdcValues.Skip(chunk.RawAdcValues.Count - maxValuesToLogPerEnd));
                                                }
                                                //Console.WriteLine($"    RawADCValues (showing subset): [{string.Join(", ", valuesToLog.Select(v => v == separatorMarker ? "..." : v.ToString()))}]");
                                                //Console.WriteLine($"-------");
                                                //Console.WriteLine($"    RawADCValues: [{string.Join(", ", chunk.RawAdcValues)}]");
                                                Console.WriteLine($"-------");
                                                //Console.WriteLine($"    RawADCValues (hex, showing subset): [{string.Join(", ", valuesToLog.Select(v => v == separatorMarker ? "..." : $"0x{v:X8}"))}]");
                                                Console.WriteLine($"[{string.Join(", ", valuesToLog.Select(v => v == separatorMarker ? "..." : $"0x{v:X8}"))}]");
                                                Console.WriteLine($"-------");
                                        }
                                        else
                                        {
                                                Console.WriteLine("    RawADCValues: [No values in this chunk]");
                                        }

                                }
                                if (getDataResp.DataChunks.Any() && (getDataResp.DataChunks.Last().SequenceNumber + 1) == getDataResp.TotalChunksInDataset)
                                {
                                        Console.WriteLine($"[FIRMWARE_DATA_STREAM] All expected chunks for DatasetID: {getDataResp.DatasetId} received by TCM.");
                                }
                        }
                        else if (receivedMsg.PayloadCase == Device.Main.PayloadOneofCase.EventTriggerAlert)
                        {
                                var triggerAlert = receivedMsg.EventTriggerAlert;
                                _logger.LogWarning("--> TCM: Received EventTriggerAlert (Seq: {Seq}) from {DeviceId}! TriggerTimeNs: {TimeNs}, Desc: {Desc}",
                                                receivedMsg.Header.SequenceNumber, connection.DeviceId, triggerAlert.TriggerTimestampNs, triggerAlert.TriggerDescription);
                                Console.WriteLine($"[FIRMWARE_UNSOLICITED_ALERT] Type: EventTriggerAlert, Device: {connection.DeviceId}, Seq: {receivedMsg.Header.SequenceNumber}, TriggerTimeNs: {triggerAlert.TriggerTimestampNs}, Channel: {triggerAlert.TriggeringChannelId}, Value: {triggerAlert.TriggeringRawValue}, Threshold: {triggerAlert.TriggerThresholdCrossed}, Description: '{triggerAlert.TriggerDescription}'");

                                var ackMain = new Device.Main {
                                        Header = new Device.Header { DeviceId = "server-gemini-01", SequenceNumber = receivedMsg.Header.SequenceNumber, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
                                               EventTriggerAlertResponse = new Device.EventTriggerAlertResponse { ResponseBase = new Device.ResponseBase { Status = Device.StatusCode.StatusOk }, Acknowledge = true }
                                };
                                _logger.LogInformation("<-- TCM: Sending EventTriggerAlertResponse (ACK for Alert Seq: {AlertSeq})", receivedMsg.Header.SequenceNumber);
                                await SendCommandInternalAsync(connection, ackMain.ToByteArray()); // Use internal send

                                var startCapturePayload = new Device.StartCaptureRequest { TriggerTimestampNs = triggerAlert.TriggerTimestampNs };
                                var startCaptureSequence = (uint)Interlocked.Increment(ref _serverSequenceCounter);
                                var startCaptureMain = new Device.Main {
                                        Header = new Device.Header { DeviceId = "server-gemini-01", SequenceNumber = startCaptureSequence, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
                                               DataRequest = new Device.DataRequest { CommandType = Device.DataCommandType.ManageData, ManageData = new Device.ManageDataRequest { Operation = Device.DataOperation.StartCapture, StartCapture = startCapturePayload } }
                                };
                                _logger.LogInformation("<-- TCM: Auto-sending StartCaptureRequest (New Seq: {Seq}) based on EventTriggerAlert", startCaptureSequence);
                                await SendCommandInternalAsync(connection, startCaptureMain.ToByteArray()); // Use internal send
                        }
                        else if (receivedMsg.PayloadCase == Device.Main.PayloadOneofCase.ErrorAlert)
                        {
                                var errorAlertInfo = receivedMsg.ErrorAlert.ErrorInfo;
                                _logger.LogError("--> TCM: Received ErrorAlert (Seq: {Seq}) from {DeviceId}! Code: {Code}, Severity: {Severity}, Desc: '{Desc}'",
                                                receivedMsg.Header.SequenceNumber, connection.DeviceId, errorAlertInfo.Code, errorAlertInfo.Severity, errorAlertInfo.Description);
                                Console.WriteLine($"[FIRMWARE_UNSOLICITED_ALERT] Type: ErrorAlert, Device: {connection.DeviceId}, Seq: {receivedMsg.Header.SequenceNumber}, Code: {errorAlertInfo.Code}, Severity: {errorAlertInfo.Severity}, Description: '{errorAlertInfo.Description}', Timestamp: {errorAlertInfo.TimestampMs}");

                                var errorAckMain = new Device.Main {
                                        Header = new Device.Header { DeviceId = "server-gemini-01", SequenceNumber = receivedMsg.Header.SequenceNumber, TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
                                               ErrorAlertResponse = new Device.ErrorAlertResponse { ResponseBase = new Device.ResponseBase { Status = Device.StatusCode.StatusOk } }
                                };
                                _logger.LogInformation("<-- TCM: Sending ErrorAlertResponse (ACK for Alert Seq: {AlertSeq})", receivedMsg.Header.SequenceNumber);
                                await SendCommandInternalAsync(connection, errorAckMain.ToByteArray()); // Use internal send
                        }
                        // The orchestrator will log specifics for responses it's waiting for.
                        // TCM can still log if a response to a background task (not using orchestrator) is received.
                        // This part can be expanded if you have specific background tasks whose responses need logging here.
                        // Example: HeartbeatResponse from a background HeartbeatService
                        else if (receivedMsg.PayloadCase == Device.Main.PayloadOneofCase.HealthResponse &&
                                        receivedMsg.HealthResponse.CommandResponsePayloadCase == Device.HealthResponse.CommandResponsePayloadOneofCase.Heartbeat)
                        {
                                var hbResponse = receivedMsg.HealthResponse.Heartbeat;
                                _logger.LogInformation("--> TCM: Processed Heartbeat Response (Seq: {Seq}). Status: {Status}", receivedMsg.Header?.SequenceNumber, hbResponse.ResponseBase?.Status);
                        }
                }

                // Helper to get a string representation of the inner payload case for logging
                private string GetInnerPayloadCase(Device.Main msg)
                {
                        switch (msg.PayloadCase)
                        {
                                case Device.Main.PayloadOneofCase.HealthRequest: return msg.HealthRequest.CommandPayloadCase.ToString();
                                case Device.Main.PayloadOneofCase.HealthResponse: return msg.HealthResponse.CommandResponsePayloadCase.ToString();
                                case Device.Main.PayloadOneofCase.FirmwareRequest: return msg.FirmwareRequest.CommandPayloadCase.ToString();
                                case Device.Main.PayloadOneofCase.FirmwareResponse: return msg.FirmwareResponse.CommandResponsePayloadCase.ToString();
                                case Device.Main.PayloadOneofCase.DataRequest: return msg.DataRequest.CommandPayloadCase.ToString();
                                case Device.Main.PayloadOneofCase.DataResponse: return msg.DataResponse.CommandResponsePayloadCase.ToString();
                                case Device.Main.PayloadOneofCase.CalibrationRequest: return "ManageCalibration"; // Only one option
                                case Device.Main.PayloadOneofCase.CalibrationResponse: return "ManageCalibration"; // Only one option
                                case Device.Main.PayloadOneofCase.DeviceRequest: return msg.DeviceRequest.CommandPayloadCase.ToString();
                                case Device.Main.PayloadOneofCase.DeviceResponse: return msg.DeviceResponse.CommandResponsePayloadCase.ToString();
                                default: return "N/A";
                        }
                }


                private async Task<int> ReadExactAsync(NetworkStream stream, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                {
                        if (count == 0) return 0;
                        int totalBytesRead = 0;
                        while (totalBytesRead < count)
                        {
                                int bytesRead;
                                try { bytesRead = await stream.ReadAsync(buffer.AsMemory(offset + totalBytesRead, count - totalBytesRead), cancellationToken); }
                                catch (OperationCanceledException) { throw; }
                                catch (IOException ex) when (ex.InnerException is SocketException se && (se.SocketErrorCode == SocketError.ConnectionAborted || se.SocketErrorCode == SocketError.ConnectionReset || se.SocketErrorCode == SocketError.Shutdown))
                                { _logger.LogDebug("ReadExactAsync: Socket closed by peer or network error for an active connection."); return totalBytesRead; } // Return bytes read so far
                                catch (ObjectDisposedException)
                                { _logger.LogDebug("ReadExactAsync: Stream or client disposed during read."); return totalBytesRead; } // Return bytes read so far

                                if (bytesRead == 0) // Graceful disconnect by peer
                                { _logger.LogDebug("ReadExactAsync: Stream returned 0 bytes, peer likely disconnected gracefully."); return totalBytesRead; }
                                totalBytesRead += bytesRead;
                        }
                        return totalBytesRead;
                }

                // Internal send method that operates on an existing ManagedDeviceConnection
                private async Task<bool> SendCommandInternalAsync(ManagedDeviceConnection connection, byte[] commandData)
                {
                        if (connection == null || connection.Cts.IsCancellationRequested || !connection.Client.Connected)
                        {
                                _logger.LogWarning("SendCommandInternal: Send to {DeviceId} failed: connection closing/disconnected or null.", connection?.DeviceId ?? "Unknown");
                                return false;
                        }
                        try
                        {
                                // commandData is the byte array of the Device.Main message.
                                // We need to wrap this with WriteDelimitedTo using a temporary IMessage instance.
                                // This assumes commandData is a serialized Device.Main message.
                                var messageToSend = Device.Main.Parser.ParseFrom(commandData); // Re-parse to get IMessage

                                // WriteDelimitedTo handles the varint length prefix. <-- This is needed for Varint as Indesign defaulted to this
                                // Ensure exclusive access to the stream for writing the length and message.
                                // A simple lock on the stream or a dedicated send queue might be needed for high concurrency on a single connection.
                                await Task.Run(() => messageToSend.WriteDelimitedTo(connection.Stream), connection.Cts.Token);
                                await connection.Stream.FlushAsync(connection.Cts.Token); // Ensure data is sent

                                _logger.LogDebug("SendCommandInternal: Sent delimited message to {DeviceId}, Original Proto Length: {Length}", connection.DeviceId, commandData.Length);
                                return true;
                        }
                        catch (OperationCanceledException) when (connection.Cts.Token.IsCancellationRequested)
                        { _logger.LogWarning("SendCommandInternal: Send to {DeviceId} cancelled: connection closing.", connection.DeviceId); return false; }
                        catch (IOException ioEx)
                        { _logger.LogError(ioEx, "SendCommandInternal: IOException sending to {DeviceId}. Marking for closure.", connection.DeviceId); connection.Cts.Cancel(); return false; }
                        catch (ObjectDisposedException odEx)
                        { _logger.LogWarning(odEx, "SendCommandInternal: Send to {DeviceId} failed: object disposed.", connection.DeviceId); return false; }
                        catch (Exception ex)
                        { _logger.LogError(ex, "SendCommandInternal: Unexpected error sending to {DeviceId}.", connection.DeviceId); return false; }
                }


                // Public send method, used by Orchestrator and Background Services
                public async Task<bool> SendCommandAsync(string deviceId, byte[] commandData)
                {
                        if (_activeConnections.TryGetValue(deviceId, out var connection))
                        {
                                return await SendCommandInternalAsync(connection, commandData);
                        }
                        else
                        {
                                _logger.LogWarning("SendCommandAsync: Send failed: unknown/disconnected device: {DeviceId}", deviceId);
                                return false;
                        }
                }

                public void Stop()
                {
                        lock (this)
                        {
                                if (_disposed || !_isRunning) return;
                                _logger.LogInformation("Initiating server shutdown...");
                                _isRunning = false;
                                try { if (!_serverShutdownCts.IsCancellationRequested) _serverShutdownCts.Cancel(); } catch (ObjectDisposedException) { }

                                var connectionsToDispose = _activeConnections.Values.ToList(); // Create a copy to iterate
                                _logger.LogInformation("Disconnecting {Count} active clients...", connectionsToDispose.Count);

                                // We don't clear _activeConnections here anymore, let the finally block in ProcessIncomingDeviceDataAsync handle removal
                                // This avoids race conditions if a connection is removed while ProcessIncomingDeviceDataAsync is still cleaning up.
                                // Just signal them to close.
                                foreach (var conn in connectionsToDispose)
                                {
                                        try
                                        {
                                                if (!conn.Cts.IsCancellationRequested) conn.Cts.Cancel();
                                                // Don't dispose here directly, let the processing loop finish
                                        }
                                        catch (Exception ex) { _logger.LogWarning(ex, "Exception while signaling connection {DeviceId} to close during Stop.", conn.DeviceId); }
                                }
                        }
                }

                public void Dispose()
                {
                        Dispose(true);
                        GC.SuppressFinalize(this);
                }

                protected virtual void Dispose(bool disposing)
                {
                        lock (this)
                        {
                                if (_disposed) return;
                                if (disposing)
                                {
                                        Stop(); // Ensure Stop logic is called
                                        try { _serverShutdownCts?.Dispose(); } catch (ObjectDisposedException) { }
                                }
                                _disposed = true;
                                _logger.LogInformation("TcpConnectionManager disposed.");
                        }
                }
        }
}
