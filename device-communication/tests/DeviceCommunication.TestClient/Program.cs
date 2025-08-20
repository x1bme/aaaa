using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Device;
using Google.Protobuf;
using Google.Protobuf.Collections;

// Alias for Device.StatusCode to avoid conflict if this client ever used gRPC Core's StatusCode
using DeviceStatusCode = Device.StatusCode;

namespace DeviceCommunication.TestClient
{
    public class Program
    {
        static CancellationTokenSource _appShutdownCts = new CancellationTokenSource();
        static string _deviceId = "test-device-001"; // Default, can be overridden by args
        static Stopwatch _uptimeStopwatch = new Stopwatch();

        // --- Firmware Update Simulation State ---
        static int _expectedFirmwareSize = 0;
        static int _expectedBlockSize = 0;
        static Dictionary<uint, ByteString> _receivedBlocks = new Dictionary<uint, ByteString>();
        static int _totalBlocksExpected = 0;

        // --- Sequence counters for client-initiated messages ---
        static long _triggerAlertSequenceCounter = 500000;
        static long _errorAlertSequenceCounter = 600000;
        static long _dataChunkSequenceCounter = 700000; // For chunks sent in GetDataResponse (unsolicited pushes)

        // --- Simulated Static Dataset ---
        private const uint StaticDatasetId = 777;
        private const int StaticDatasetTotalSamples = 90000; // 90k samples, see Indesign's sample file that they're using.
        private const int SamplesPerAdcChunk = 1024; // Max per SignalDataChunk.RawAdcValues
        private const int ChunksPerGetDataResponse = 5; // Max per GetDataResponse.DataChunks
        private const int NumChannelsForStaticDataset = 2;
        private const uint SampleRateHzForStaticDataset = 1000;
        private static readonly int StaticDatasetTotalChunks = (int)Math.Ceiling((double)StaticDatasetTotalSamples / SamplesPerAdcChunk);
        private static readonly ulong StaticDatasetTriggerTimestampNs = (ulong)DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeMilliseconds() * 1_000_000UL;
        private static readonly ulong StaticDatasetStartTimeNs = StaticDatasetTriggerTimestampNs - (5 * 1_000_000_000UL); // 5s pre-trigger
        private static readonly ulong StaticDatasetEndTimeNs = StaticDatasetStartTimeNs + (ulong)((double)StaticDatasetTotalSamples / SampleRateHzForStaticDataset * 1_000_000_000.0);


        private static Device.SignalDataInfo _simulatedStaticDatasetInfo = new Device.SignalDataInfo
        {
            DatasetId = StaticDatasetId,
            TriggerTimestampNs = StaticDatasetTriggerTimestampNs,
            StartTimeNs = StaticDatasetStartTimeNs,
            EndTimeNs = StaticDatasetEndTimeNs,
            PreTriggerDurationSeconds = 5,
            PostTriggerDurationSeconds = (uint)(StaticDatasetTotalSamples / SampleRateHzForStaticDataset) - 5,
            SampleRateHz = SampleRateHzForStaticDataset,
            NumChannels = NumChannelsForStaticDataset,
            ApproximateSizeKb = (uint)((StaticDatasetTotalSamples * NumChannelsForStaticDataset * 4 /*bytes_per_sint32_sample*/) / 1024),
            CaptureStatus = Device.CaptureStatus.CaptureCompleteSuccess
        };
        // --- End of Simulated Static Dataset ---

        // Store for dynamically created datasets if needed (e.g., from server-initiated StartCapture)
        static ConcurrentDictionary<uint, SignalDataInfo> _dynamicStoredDatasetInfo = new ConcurrentDictionary<uint, SignalDataInfo>();
        static ConcurrentDictionary<uint, ulong> _dynamicDatasetTriggerTimes = new ConcurrentDictionary<uint, ulong>();
        static CancellationTokenSource? _staticDataSendCts = null; // To cancel ongoing static data send if needed

        private const string FactoryResetConfirmationCodeDeviceSide = "CONFIRM-SIM-RESET-123"; // Device expects this for factory reset

        public static async Task Main(string[] args)
        {
            if (args.Length > 0)
            {
                _deviceId = args[0];
                _logger.LogInformation($"Overriding Device ID to: {_deviceId}");
            }

            _uptimeStopwatch.Start();
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                _logger.LogInformation("Ctrl+C pressed. Shutting down client...");
                if (_staticDataSendCts != null && !_staticDataSendCts.IsCancellationRequested)
                {
                    _staticDataSendCts.Cancel();
                }
                _appShutdownCts.Cancel();
                eventArgs.Cancel = true; // Prevent the process from terminating immediately
            };

            await RunClientAsync(_appShutdownCts.Token);

            _uptimeStopwatch.Stop();
            _logger.LogInformation("Test client finished execution.");
        }

        static async Task RunClientAsync(CancellationToken cancellationToken)
        {
            TcpClient? client = null;
            Task? triggerAlertTask = null;
            Task? errorAlertTask = null;
            CancellationTokenSource? linkedCtsForClientRun = null;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    client = new TcpClient();
                    _logger.LogInformation($"Device '{_deviceId}' attempting to connect to localhost:12345...");
                    // Use a timeout for connection attempts
                    var connectTask = client.ConnectAsync("localhost", 12345);
                    if (await Task.WhenAny(connectTask, Task.Delay(TimeSpan.FromSeconds(5), cancellationToken)) != connectTask || !client.Connected)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        _logger.LogWarning($"Connection attempt timed out or failed. Retrying in 5s...");
                        client.Dispose();
                        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                        continue;
                    }
                    _logger.LogInformation($"Connected as Device ID: {_deviceId}");

                    using var stream = client.GetStream();
                    linkedCtsForClientRun = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                    // Send initial identification message (e.g., a heartbeat)
                    await SendIdentificationAsync(stream, linkedCtsForClientRun.Token);

                    _logger.LogInformation("Starting background task for trigger alerts.");
                    triggerAlertTask = Task.Run(() => SendTriggerAlertsPeriodicallyAsync(stream, linkedCtsForClientRun.Token), linkedCtsForClientRun.Token);

                    _logger.LogInformation("Starting background task for error alerts.");
                    errorAlertTask = Task.Run(() => SendErrorAlertsPeriodicallyAsync(stream, linkedCtsForClientRun.Token), linkedCtsForClientRun.Token);

                    _logger.LogInformation("Listening for commands. Press Ctrl+C to exit.");
                    await ListenForCommandsAsync(client, stream, linkedCtsForClientRun.Token);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || (linkedCtsForClientRun?.IsCancellationRequested ?? false))
                {
                    _logger.LogInformation("Client operation cancelled during run.");
                }
                catch (SocketException sockEx)
                {
                    _logger.LogError(sockEx, $"Socket error: {sockEx.SocketErrorCode}. Retrying connection in 5s...");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error in client execution. Retrying connection in 5s...");
                }
                finally
                {
                    _logger.LogDebug("Client run loop finished or errored. Cleaning up current connection attempt.");
                    if (_staticDataSendCts != null && !_staticDataSendCts.IsCancellationRequested)
                    {
                        _logger.LogInformation("Cancelling ongoing static data send due to disconnect/error.");
                        _staticDataSendCts.Cancel(); // Cancel any active static data send
                        _staticDataSendCts.Dispose();
                        _staticDataSendCts = null;
                    }

                    var alertTasksToWait = new List<Task>();
                    if (triggerAlertTask != null && !triggerAlertTask.IsCompleted) alertTasksToWait.Add(triggerAlertTask);
                    if (errorAlertTask != null && !errorAlertTask.IsCompleted) alertTasksToWait.Add(errorAlertTask);

                    if (alertTasksToWait.Any())
                    {
                        _logger.LogDebug("Waiting for alert tasks to complete (max 2s)...");
                        try { await Task.WhenAll(alertTasksToWait).WaitAsync(TimeSpan.FromSeconds(2), cancellationToken); } // Use main CToken for overall shutdown
                        catch (TimeoutException) { _logger.LogWarning("Alert tasks did not complete within timeout during cleanup."); }
                        catch (OperationCanceledException) { _logger.LogInformation("Alert task waiting cancelled."); }
                        catch (Exception ex) { _logger.LogError(ex, "Exception waiting for alert tasks during cleanup."); }
                    }
                    triggerAlertTask = null;
                    errorAlertTask = null;

                    try { client?.Close(); client?.Dispose(); } catch { } // Dispose client for this attempt
                    client = null;
                    try { linkedCtsForClientRun?.Cancel(); linkedCtsForClientRun?.Dispose(); } catch { }
                    linkedCtsForClientRun = null;

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Attempting to reconnect in 5 seconds...");
                        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken); // Wait before retrying connection
                    }
                }
            }
            _logger.LogInformation("Client exiting main run loop due to cancellation.");
        }


        static async Task SendIdentificationAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            var identifyRequest = new Device.Main
            {
                Header = new Device.Header
                {
                    DeviceId = _deviceId,
                    SequenceNumber = 1,
                    TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                },
                // Send an initial HealthRequest:Heartbeat as identification
                HealthRequest = new Device.HealthRequest
                {
                    CommandType = Device.HealthCommandType.Heartbeat,
                    Heartbeat = new Device.HeartbeatRequest()
                }
            };
            _logger.LogDebug("Sending identification (Heartbeat Seq: 1)...");
            await SendMessageAsync(stream, identifyRequest, cancellationToken);
            _logger.LogInformation("Identification sent.");
        }

        static async Task ListenForCommandsAsync(TcpClient client, NetworkStream stream, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && client.Connected)
            {
                Device.Main receivedMsg;
                try
                {
                    // ParseDelimitedFrom reads the varint length prefix then the message.
                    // Use Task.Run to make it truly cancellable with our token.
                    receivedMsg = await Task.Run(() => Device.Main.Parser.ParseDelimitedFrom(stream), cancellationToken);

                    if (receivedMsg == null) // Indicates EOS
                    {
                        _logger.LogWarning("Disconnected from server (ParseDelimitedFrom returned null).");
                        break;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Command listening cancelled.");
                    break;
                }
                catch (Google.Protobuf.InvalidProtocolBufferException ex) when (ex.InnerException is EndOfStreamException)
                {
                    _logger.LogInformation("Server disconnected gracefully (EOS during parse).");
                    break;
                }
                catch (Google.Protobuf.InvalidProtocolBufferException protoEx)
                {
                    _logger.LogError(protoEx, $"Failed to parse command from server.");
                    continue; // Or break, depending on desired robustness
                }
                catch (IOException ioEx)
                {
                    _logger.LogError(ioEx, "IOException (e.g. connection reset by server). Assuming disconnect.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading from server stream.");
                    break;
                }


                try
                {
                    await HandleServerCommandAsync(stream, receivedMsg, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Command handling cancelled.");
                    break;
                }
                catch (Exception ex) { _logger.LogError(ex, "Error handling server command."); }
            }
            _logger.LogInformation("ListenForCommands loop finished or client disconnected.");
        }

        static async Task HandleServerCommandAsync(NetworkStream stream, Device.Main receivedMsg, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Received from server. Device: {receivedMsg.Header?.DeviceId}, Seq: {receivedMsg.Header?.SequenceNumber}, Type: {receivedMsg.PayloadCase}");

            Device.Main responseMain = new Device.Main
            {
                Header = new Device.Header
                {
                    DeviceId = _deviceId,
                    SequenceNumber = receivedMsg.Header?.SequenceNumber ?? 0,
                    TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                }
            };
            bool shouldRespond = true;

            // --- Handle HealthRequest ---
            if (receivedMsg.PayloadCase == Device.Main.PayloadOneofCase.HealthRequest)
            {
                var healthReq = receivedMsg.HealthRequest;
                _logger.LogInformation("--> Handling HealthRequest, CommandType: {0}", healthReq.CommandType);
                var healthRespPayload = new Device.HealthResponse { CommandType = healthReq.CommandType };

                if (healthReq.CommandType == Device.HealthCommandType.Heartbeat)
                {
                    healthRespPayload.Heartbeat = new Device.HeartbeatResponse
                    {
                        ResponseBase = new Device.ResponseBase { Status = DeviceStatusCode.StatusOk },
                        DeviceTimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };
                }
                else if (healthReq.CommandType == Device.HealthCommandType.HealthStatus)
                {
                    var statusOpReq = healthReq.HealthStatus;
                    var statusOpResp = new Device.HealthStatusResponse { Operation = statusOpReq.Operation };
                    _logger.LogInformation("    -> HealthStatus Operation: {0}", statusOpReq.Operation);

                    if (statusOpReq.Operation == Device.HealthStatusOperation.GetCurrentStatus)
                    {
                        statusOpResp.GetCurrent = new Device.GetCurrentStatusResponse
                        {
                            ResponseBase = new Device.ResponseBase { Status = DeviceStatusCode.StatusOk },
                            IsOperational = true,
                            SystemState = "SIM_OPERATIONAL",
                            TemperatureCelsius = 28.5f,
                            UptimeSeconds = (ulong)_uptimeStopwatch.Elapsed.TotalSeconds,
                            CpuUsagePercent = (float)(new Random().NextDouble() * 50.0),
                            PtpLocked = true,
                            LastResetReason = Device.ResetReason.PowerOn
                        };
                    }
                    else if (statusOpReq.Operation == Device.HealthStatusOperation.GetErrorLog)
                    {
                        // Simplified: Return a few dummy errors, no real pagination for test client
                        statusOpResp.GetLog = new Device.GetErrorLogResponse
                        {
                            ResponseBase = new Device.ResponseBase { Status = DeviceStatusCode.StatusOk },
                            TotalMatchingEntries = 2,
                            NextPageToken = 0
                        };
                        statusOpResp.GetLog.ErrorLogEntries.Add(new AlertInfo
                        {
                            Code = ErrorCode.HealthTempWarning,
                            Severity = SeverityLevel.SeverityWarning,
                            Description = "Log Entry 1: Temp High",
                            TimestampMs = (ulong)DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeMilliseconds()
                        });
                        statusOpResp.GetLog.ErrorLogEntries.Add(new AlertInfo
                        {
                            Code = ErrorCode.DataBufferOverwrite,
                            Severity = SeverityLevel.SeverityError,
                            Description = "Log Entry 2: Buffer Full",
                            TimestampMs = (ulong)DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeMilliseconds()
                        });
                    }
                    else if (statusOpReq.Operation == Device.HealthStatusOperation.ClearErrorLog)
                    {
                        statusOpResp.ClearLog = new Device.ClearErrorLogResponse
                        {
                            ResponseBase = new Device.ResponseBase
                            {
                                Status = (statusOpReq.ClearLog.ConfirmationCode == "SIM-CONFIRM-CLEAR-LOG" ? DeviceStatusCode.StatusOk : DeviceStatusCode.StatusUnauthorized)
                            }
                        };
                    }
                    healthRespPayload.HealthStatus = statusOpResp;
                }
                responseMain.HealthResponse = healthRespPayload;
            }
            // --- Handle FirmwareRequest ---
            else if (receivedMsg.PayloadCase == Device.Main.PayloadOneofCase.FirmwareRequest)
            {
                var fwReq = receivedMsg.FirmwareRequest;
                _logger.LogInformation("--> Handling FirmwareRequest, CommandType: {0}", fwReq.CommandType);
                var fwRespPayload = new Device.FirmwareResponse { CommandType = fwReq.CommandType };

                if (fwReq.CommandType == Device.FirmwareCommandType.GetInfo)
                {
                    fwRespPayload.GetInfo = new Device.GetFirmwareInfoResponse
                    {
                        ResponseBase = new Device.ResponseBase { Status = DeviceStatusCode.StatusOk },
                        Version = "SIM-CLIENT-1.2.3",
                        BuildDate = "2024-07-04",
                        BuildHash = "simclientdev77",
                        SecureBootEnabled = true,
                        CurrentImageSlot = "A_SIM"
                    };
                }
                else if (fwReq.CommandType == Device.FirmwareCommandType.Update)
                {
                    var updateOpReq = fwReq.Update;
                    var updateOpResp = new Device.UpdateFirmwareResponse
                    {
                        Operation = updateOpReq.Operation,
                        ResponseBase = new Device.ResponseBase { Status = DeviceStatusCode.StatusOk }
                    };
                    _logger.LogInformation("    -> FirmwareUpdate Operation: {0}", updateOpReq.Operation);
                    switch (updateOpReq.Operation)
                    {
                        case Device.FirmwareUpdateOperation.FirmwareOpPrepare:
                            _expectedFirmwareSize = (int)updateOpReq.Prepare.FirmwareSizeBytes;
                            _expectedBlockSize = (int)updateOpReq.Prepare.BlockSizePreference;
                            _receivedBlocks.Clear();
                            _totalBlocksExpected = (int)Math.Ceiling((double)_expectedFirmwareSize / Math.Max(1, _expectedBlockSize));
                            updateOpResp.Prepare = new Device.FirmwarePrepareResponsePayload
                            {
                                ReadyToReceive = true,
                                MaxBlockSize = (uint)Math.Max(1, _expectedBlockSize)
                            };
                            break;
                        case Device.FirmwareUpdateOperation.FirmwareOpTransfer:
                            if (updateOpReq.Transfer != null)
                            {
                                _receivedBlocks[updateOpReq.Transfer.BlockSequenceNumber] = updateOpReq.Transfer.Data;
                                updateOpResp.Transfer = new Device.FirmwareTransferResponsePayload
                                {
                                    BlockSequenceNumber = updateOpReq.Transfer.BlockSequenceNumber,
                                    CrcOk = true
                                };
                            }
                            else
                            {
                                updateOpResp.ResponseBase.Status = DeviceStatusCode.StatusInvalidParam;
                            }
                            break;
                        case Device.FirmwareUpdateOperation.FirmwareOpVerify:
                            bool passed = updateOpReq.Verify != null && (updateOpReq.Verify.TotalBlocksSent == _receivedBlocks.Count && updateOpReq.Verify.TotalBlocksSent == _totalBlocksExpected);
                            updateOpResp.Verify = new Device.FirmwareVerifyResponsePayload { VerificationPassed = passed };
                            if (!passed)
                                updateOpResp.ResponseBase.Status = DeviceStatusCode.StatusError;
                            break;
                        case Device.FirmwareUpdateOperation.FirmwareOpApply:
                            updateOpResp.Apply = new Device.FirmwareApplyResponsePayload { ApplicationScheduled = true };
                            _logger.LogInformation("    SIM: Device would reboot now to apply firmware.");
                            break;
                        case Device.FirmwareUpdateOperation.FirmwareOpAbort:
                            updateOpResp.Abort = new Device.FirmwareAbortResponsePayload { Aborted = true };
                            break;
                        default:
                            updateOpResp.ResponseBase.Status = DeviceStatusCode.StatusInvalidParam;
                            break;
                    }
                    fwRespPayload.Update = updateOpResp;
                }
                responseMain.FirmwareResponse = fwRespPayload;
            }
            // --- Handle CalibrationRequest ---
            else if (receivedMsg.PayloadCase == Device.Main.PayloadOneofCase.CalibrationRequest)
            {
                var calReq = receivedMsg.CalibrationRequest.ManageCalibration; // Only one type of cal request
                _logger.LogInformation("--> Handling CalibrationRequest, Operation: {0}", calReq.Operation);
                var calRespPayload = new Device.ManageCalibrationResponse { Operation = calReq.Operation };

                if (calReq.Operation == Device.CalibrationOperation.ReadParams)
                {
                    var readParamsResp = new Device.ReadCalibrationParamsResponse
                    {
                        ResponseBase = new Device.ResponseBase { Status = DeviceStatusCode.StatusOk }
                    };
                    readParamsResp.Parameters.Add(new AdcChannelCalibrationParams
                    {
                        ChannelId = 1,
                        Gain = 1.001f,
                        Offset = -0.002f,
                        LastUpdatedMs = (ulong)DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeMilliseconds(),
                        CalibrationExpiresMs = (ulong)DateTimeOffset.UtcNow.AddDays(29).ToUnixTimeMilliseconds(),
                        TemperatureAtCalCelsius = 25.5f
                    });
                    if (calReq.ReadParams.ChannelIds.Count == 0 || calReq.ReadParams.ChannelIds.Contains(2))
                        readParamsResp.Parameters.Add(new AdcChannelCalibrationParams
                        {
                            ChannelId = 2,
                            Gain = 0.998f,
                            Offset = 0.0015f,
                            LastUpdatedMs = (ulong)DateTimeOffset.UtcNow.AddDays(-2).ToUnixTimeMilliseconds(),
                            CalibrationExpiresMs = (ulong)DateTimeOffset.UtcNow.AddDays(28).ToUnixTimeMilliseconds(),
                            TemperatureAtCalCelsius = 26.0f
                        });
                    calRespPayload.ReadParams = readParamsResp;
                }
                else if (calReq.Operation == Device.CalibrationOperation.StartProcedure)
                {
                    calRespPayload.StartProcedure = new Device.StartCalibrationProcedureResponse
                    {
                        ResponseBase = new Device.ResponseBase { Status = DeviceStatusCode.StatusOk },
                        ProcedureStarted = true,
                        EstimatedDurationSeconds = 120
                    };
                }
                else if (calReq.Operation == Device.CalibrationOperation.GetStatus)
                {
                    var status = new Device.GetCalibrationStatusResponse
                    {
                        ResponseBase = new Device.ResponseBase { Status = DeviceStatusCode.StatusOk },
                        IsCalibrating = false,
                        ProgressPercent = 0,
                        TimeRemainingSeconds = 0
                    };
                    // Simulate if a calibration was "running"
                    // status.IsCalibrating = true; status.ProgressPercent = 50; status.TimeRemainingSeconds = 60; status.ChannelsInProgress.Add(1);
                    calRespPayload.GetStatus = status;
                }
                responseMain.CalibrationResponse = new Device.CalibrationResponse { ManageCalibration = calRespPayload };
            }
            // --- Handle DataRequest ---
            else if (receivedMsg.PayloadCase == Device.Main.PayloadOneofCase.DataRequest)
            {
                var dataRequest = receivedMsg.DataRequest;
                var dataResp = new Device.DataResponse { CommandType = dataRequest.CommandType };
                _logger.LogInformation("--> Handling DataRequest: CommandType {0}", dataRequest.CommandType);

                if (dataRequest.CommandType == Device.DataCommandType.Configure)
                {
                    dataResp.Configure = new Device.ConfigureResponse
                    {
                        ResponseBase = new Device.ResponseBase { Status = DeviceStatusCode.StatusOk },
                        ActualStorageAllocationKb = dataRequest.Configure.TotalStorageAllocationKb
                    };
                }
                else if (dataRequest.CommandType == Device.DataCommandType.ManageData)
                {
                    var manageDataReq = dataRequest.ManageData;
                    var manageDataRespPayload = new Device.ManageDataResponse { Operation = manageDataReq.Operation };
                    _logger.LogInformation("    -> ManageData Operation: {0}", manageDataReq.Operation);

                    switch (manageDataReq.Operation)
                    {
                        case Device.DataOperation.StartCapture:
                            uint dynamicId = (uint)new Random().Next(1000, 2000);
                            while (dynamicId == StaticDatasetId) dynamicId = (uint)new Random().Next(1000, 2000);
                            manageDataRespPayload.StartCapture = new Device.StartCaptureResponse
                            {
                                ResponseBase = new Device.ResponseBase { Status = DeviceStatusCode.StatusOk },
                                AssignedDatasetId = dynamicId,
                                CaptureInitiated = true,
                                EstimatedCaptureTimeSeconds = 3
                            };
                            _dynamicDatasetTriggerTimes[dynamicId] = manageDataReq.StartCapture.TriggerTimestampNs;
                            _dynamicStoredDatasetInfo[dynamicId] = new SignalDataInfo
                            {
                                DatasetId = dynamicId,
                                TriggerTimestampNs = manageDataReq.StartCapture.TriggerTimestampNs,
                                CaptureStatus = CaptureStatus.CaptureInProgress,
                                SampleRateHz = SampleRateHzForStaticDataset,
                                NumChannels = NumChannelsForStaticDataset
                            };
                            Task.Run(async () =>
                            {
                                await Task.Delay(3000, cancellationToken);
                                if (_dynamicStoredDatasetInfo.TryGetValue(dynamicId, out var info))
                                {
                                    info.CaptureStatus = CaptureStatus.CaptureCompleteSuccess;
                                    _logger.LogInformation("Simulated capture complete for dynamic ID {0}", dynamicId);
                                }
                            }, cancellationToken);
                            break;
                        case Device.DataOperation.DataGetStorageInfo:
                            manageDataRespPayload.GetStorageInfo = new Device.GetStorageInfoResponse
                            {
                                ResponseBase = new Device.ResponseBase { Status = DeviceStatusCode.StatusOk },
                                StorageInfo = new Device.StorageInfo
                                {
                                    TotalStorageKb = 8192 * 2,
                                    UsedStorageKb = _simulatedStaticDatasetInfo.ApproximateSizeKb + 512 + (uint)_dynamicStoredDatasetInfo.Count * 100,
                                    TotalDatasets = 1 + (uint)_dynamicStoredDatasetInfo.Count
                                }
                            };
                            break;
                        case Device.DataOperation.DataRead:
                            var readPayload = new Device.ReadDataResponse
                            {
                                ResponseBase = new Device.ResponseBase { Status = DeviceStatusCode.StatusOk }
                            };
                            readPayload.Datasets.Add(_simulatedStaticDatasetInfo);
                            readPayload.Datasets.AddRange(_dynamicStoredDatasetInfo.Values.Where(di => di.CaptureStatus == CaptureStatus.CaptureCompleteSuccess));
                            readPayload.TotalMatchingDatasets = (uint)readPayload.Datasets.Count;
                            readPayload.NextPageToken = 0;
                            manageDataRespPayload.Read = readPayload;
                            break;
                        case Device.DataOperation.DataGet:
                            var getDataReqPayload = manageDataReq.Get;
                            _logger.LogInformation("    -> TestClient: Received GetDataRequest (ServerSeq: {0}) for DatasetID: {1}",
                                receivedMsg.Header.SequenceNumber,
                                getDataReqPayload.DatasetId);

                            if (getDataReqPayload.DatasetId == StaticDatasetId)
                            {
                                var initialGetDataResponseContents = new Device.GetDataResponse
                                {
                                    ResponseBase = new Device.ResponseBase { Status = DeviceStatusCode.StatusOk, Message = "Static data retrieval initiated." },
                                    DatasetId = StaticDatasetId,
                                    TotalChunksInDataset = (uint)StaticDatasetTotalChunks,
                                    FirstChunkSequenceNumberInThisResponse = 0
                                };
                                manageDataRespPayload.Get = initialGetDataResponseContents;

                                _logger.LogInformation("    -> TestClient: Matched StaticDatasetId. Preparing initial GetDataResponse (ServerSeq: {0}). Initiating SendStaticDataChunksAsync.", receivedMsg.Header.SequenceNumber);
                                if (_staticDataSendCts != null && !_staticDataSendCts.IsCancellationRequested) _staticDataSendCts.Cancel();
                                _staticDataSendCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                                _ = Task.Run(() => SendStaticDataChunksAsync(stream, _staticDataSendCts.Token), _staticDataSendCts.Token);
                            }
                            else
                            {
                                _logger.LogWarning("    -> TestClient: GetDataRequest for unknown or not-ready DatasetID: {0}", getDataReqPayload.DatasetId);
                                var errorGetDataResponseContents = new Device.GetDataResponse
                                {
                                    ResponseBase = new Device.ResponseBase { Status = DeviceStatusCode.StatusError, Message = $"Dataset {getDataReqPayload.DatasetId} not found/ready." },
                                    DatasetId = getDataReqPayload.DatasetId
                                };
                                manageDataRespPayload.Get = errorGetDataResponseContents;
                            }
                            dataResp.ManageData = manageDataRespPayload;
                            responseMain.DataResponse = dataResp;
                            break;
                        case Device.DataOperation.DataDelete:
                            var deleteResp = new Device.DeleteDataResponse { ResponseBase = new Device.ResponseBase { Status = DeviceStatusCode.StatusOk } };
                            foreach (var idToDel in manageDataReq.Delete.DatasetIds)
                            {
                                if (idToDel == StaticDatasetId)
                                {
                                    deleteResp.NotDeletedCount++;
                                    _logger.LogWarning("    -> TestClient: Attempt to delete static dataset {0} ignored.", idToDel);
                                }
                                else if (_dynamicStoredDatasetInfo.TryRemove(idToDel, out _))
                                {
                                    _dynamicDatasetTriggerTimes.TryRemove(idToDel, out _);
                                    deleteResp.DeletedDatasetIds.Add(idToDel);
                                }
                                else
                                {
                                    deleteResp.NotDeletedCount++;
                                }
                            }
                            if (manageDataReq.Delete.DeleteAllDatasets && manageDataReq.Delete.ConfirmationCode == "SIM-CONFIRM-DELETE-ALL")
                            {
                                foreach (var key in _dynamicStoredDatasetInfo.Keys.ToList())
                                {
                                    if (_dynamicStoredDatasetInfo.TryRemove(key, out _))
                                    {
                                        _dynamicDatasetTriggerTimes.TryRemove(key, out _);
                                        deleteResp.DeletedDatasetIds.Add(key);
                                    }
                                }
                                _logger.LogInformation("    -> TestClient: All dynamic datasets deleted.");
                            }
                            manageDataRespPayload.Delete = deleteResp;
                            break;
                        default:
                            _logger.LogWarning("    -> TestClient: Unknown ManageData Operation: {0}. Sending error in StartCaptureResponse.", manageDataReq.Operation);
                            manageDataRespPayload.StartCapture = new Device.StartCaptureResponse
                            {
                                ResponseBase = new Device.ResponseBase { Status = DeviceStatusCode.StatusInvalidParam, Message = "Unknown ManageData operation received by client" }
                            };
                            break;
                    }
                    dataResp.ManageData = manageDataRespPayload;
                }
                responseMain.DataResponse = dataResp;
            }
            // --- Handle DeviceRequest ---
            else if (receivedMsg.PayloadCase == Device.Main.PayloadOneofCase.DeviceRequest)
            {
                var devReq = receivedMsg.DeviceRequest;
                _logger.LogInformation("--> Handling DeviceRequest, CommandType: {0}", devReq.CommandType);
                var devRespPayload = new Device.DeviceResponse { CommandType = devReq.CommandType };

                if (devReq.CommandType == Device.DeviceCommandType.DeviceConfig)
                {
                    var cfgReq = devReq.Config;
                    var cfgResp = new Device.DeviceConfigResponse { Operation = cfgReq.Operation };
                    _logger.LogInformation("    -> DeviceConfig Operation: {0}", cfgReq.Operation);
                    switch (cfgReq.Operation)
                    {
                        case DeviceConfigOperation.SetAssignedName:
                            cfgResp.SetAssignedName = new SetAssignedNameResponse
                            {
                                ResponseBase = new ResponseBase { Status = DeviceStatusCode.StatusOk, Message = $"Name set to {cfgReq.SetAssignedName.AssignedName}" }
                            };
                            break;
                        case DeviceConfigOperation.GetNetworkConfig:
                            cfgResp.GetNetworkConfig = new GetNetworkConfigResponse
                            {
                                ResponseBase = new ResponseBase { Status = DeviceStatusCode.StatusOk },
                                CurrentSettings = new NetworkSettings { UseDhcp = true, StaticIpAddress = "0.0.0.0" }
                            };
                            break;
                        case DeviceConfigOperation.SetNetworkConfig:
                            cfgResp.SetNetworkConfig = new SetNetworkConfigResponse
                            {
                                ResponseBase = new ResponseBase { Status = DeviceStatusCode.StatusOk, Message = "Network config applied (simulated)" }
                            };
                            break;
                        case DeviceConfigOperation.GetCertificateInfo:
                            cfgResp.GetCertificateInfo = new GetCertificateInfoResponse
                            {
                                ResponseBase = new ResponseBase { Status = DeviceStatusCode.StatusOk },
                                TlsClientCertInfo = new CertificateInfo
                                {
                                    SubjectName = $"CN={_deviceId}",
                                    IssuerName = "CN=SimCA",
                                    ValidNotBeforeMs = (ulong)DateTimeOffset.UtcNow.AddYears(-1).ToUnixTimeMilliseconds(),
                                    ValidNotAfterMs = (ulong)DateTimeOffset.UtcNow.AddYears(1).ToUnixTimeMilliseconds(),
                                    CertificateDer = ByteString.CopyFromUtf8("SIM-CERT-DATA")
                                }
                            };
                            break;
                        case DeviceConfigOperation.GenerateCsr:
                            cfgResp.GenerateCsr = new GenerateCsrResponse
                            {
                                ResponseBase = new ResponseBase { Status = DeviceStatusCode.StatusOk },
                                CsrDer = ByteString.CopyFromUtf8("SIM-CSR-DATA")
                            };
                            break;
                        case DeviceConfigOperation.UpdateCertificate:
                            cfgResp.UpdateCertificate = new UpdateCertificateResponse
                            {
                                ResponseBase = new ResponseBase { Status = DeviceStatusCode.StatusOk, Message = "Certificate updated (simulated)" }
                            };
                            break;
                        default:
                            _logger.LogWarning("    -> TestClient: Unknown DeviceConfig Operation: {0}. Sending error in SetAssignedNameResponse.", cfgReq.Operation);
                            cfgResp.SetAssignedName = new Device.SetAssignedNameResponse
                            {
                                ResponseBase = new Device.ResponseBase { Status = DeviceStatusCode.StatusInvalidParam, Message = "Unknown DeviceConfig operation received by client" }
                            };
                            break;
                    }
                    devRespPayload.Config = cfgResp;
                }
                else if (devReq.CommandType == Device.DeviceCommandType.DeviceControl)
                {
                    var ctrlReq = devReq.Control;
                    var ctrlResp = new Device.DeviceControlResponse
                    {
                        Action = ctrlReq.Action,
                        ResponseBase = new Device.ResponseBase { Status = DeviceStatusCode.StatusOk }
                    };
                    _logger.LogInformation("    -> DeviceControl Action: {0}", ctrlReq.Action);
                    switch (ctrlReq.Action)
                    {
                        case DeviceControlAction.DeviceActionReboot:
                            ctrlResp.Reboot = new RebootResponsePayload { RebootScheduled = true };
                            _logger.LogInformation("    SIM: Device would reboot now.");
                            break;
                        case DeviceControlAction.DeviceActionReset:
                            ctrlResp.Reset = new ResetResponsePayload { ResetScheduled = true };
                            _logger.LogInformation("    SIM: Device would reset now.");
                            break;
                        case DeviceControlAction.DeviceActionSetPowerMode:
                            ctrlResp.SetPowerMode = new SetPowerModeResponsePayload { ActualModeSet = ctrlReq.SetPowerMode.Mode };
                            break;
                        case DeviceControlAction.DeviceActionSetSafeMode:
                            ctrlResp.SetSafeMode = new SetSafeModeResponsePayload { SafeModeNowActive = ctrlReq.SetSafeMode.Enable };
                            break;
                        default:
                            ctrlResp.ResponseBase.Status = DeviceStatusCode.StatusInvalidParam;
                            break;
                    }
                    devRespPayload.Control = ctrlResp;
                }
                else if (devReq.CommandType == Device.DeviceCommandType.FactoryReset)
                {
                    var frReq = devReq.FactoryReset;
                    _logger.LogInformation("    -> FactoryReset Request, Confirmation: {0}", frReq.ConfirmationCode);
                    var frStatus = (frReq.ConfirmationCode == FactoryResetConfirmationCodeDeviceSide) ? DeviceStatusCode.StatusOk : DeviceStatusCode.StatusUnauthorized;
                    devRespPayload.FactoryReset = new FactoryResetResponse
                    {
                        ResponseBase = new ResponseBase { Status = frStatus },
                        ResetScheduled = (frStatus == DeviceStatusCode.StatusOk),
                        ResetDelaySeconds = 5
                    };
                    if (frStatus == DeviceStatusCode.StatusOk) _logger.LogInformation("    SIM: Device would factory reset now.");
                }
                else if (devReq.CommandType == Device.DeviceCommandType.SyncTime)
                {
                    devRespPayload.SyncTime = new SyncTimeResponse
                    {
                        ResponseBase = new ResponseBase { Status = DeviceStatusCode.StatusOk },
                        DeviceTimeBeforeSyncMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 5,
                        DeviceTimeAfterSyncMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        OffsetAppliedMs = 5,
                        PtpStatus = PtpSyncStatus.PtpStatusLocked,
                        PtpMasterId = "SimPTPMaster",
                        PtpOffsetNanoseconds = 12345
                    };
                }
                responseMain.DeviceResponse = devRespPayload;
            }
            // --- Handle EventTriggerAlertResponse (ACK from Server for client's alert) ---
            else if (receivedMsg.PayloadCase == Device.Main.PayloadOneofCase.EventTriggerAlertResponse)
            {
                shouldRespond = false; // No response needed for an ACK
                _logger.LogInformation("--> Received EventTriggerAlertResponse (ACK for my alert Seq: {0}), Status: {1}", receivedMsg.Header.SequenceNumber, receivedMsg.EventTriggerAlertResponse.ResponseBase?.Status);
            }
            // --- Handle ErrorAlertResponse (ACK from Server for client's error) ---
            else if (receivedMsg.PayloadCase == Device.Main.PayloadOneofCase.ErrorAlertResponse)
            {
                shouldRespond = false; // No response needed for an ACK
                _logger.LogInformation("--> Received ErrorAlertResponse (ACK for my error Seq: {0}), Status: {1}", receivedMsg.Header.SequenceNumber, receivedMsg.ErrorAlertResponse.ResponseBase?.Status);
            }
            // --- Default ---
            else
            {
                _logger.LogWarning($"Received unhandled server message type: {receivedMsg.PayloadCase}. No specific response prepared.");
                shouldRespond = false;
            }

            if (shouldRespond)
            {
                try
                {
                    await SendMessageAsync(stream, responseMain, cancellationToken);
                    _logger.LogInformation("--> Sent Response for Server Seq: {0} (My Seq: {1}), Type: {2}", responseMain.Header.SequenceNumber, responseMain.Header.SequenceNumber, responseMain.PayloadCase);
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Failed to send response to server for Seq: {0}", responseMain.Header.SequenceNumber);
                }
            }
        }

        static async Task SendStaticDataChunksAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            _logger.LogInformation("[STATIC_DATA_SEND] Task started for DatasetID: {0}. Total Chunks: {1}", StaticDatasetId, StaticDatasetTotalChunks);
            Random random = new Random();
            var currentGetDataResponsePayload = new Device.GetDataResponse
            {
                DatasetId = StaticDatasetId,
                TotalChunksInDataset = (uint)StaticDatasetTotalChunks
            };
            int chunksInCurrentMessage = 0;

            for (int chunkIdx = 0; chunkIdx < StaticDatasetTotalChunks; chunkIdx++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("[STATIC_DATA_SEND] Cancellation requested. Aborting send for DatasetID: {0} at chunk {1}.", StaticDatasetId, chunkIdx);
                    break;
                }

                var signalChunk = new Device.SignalDataChunk
                {
                    StartTimestampNs = StaticDatasetStartTimeNs + ((ulong)chunkIdx * SamplesPerAdcChunk * (1_000_000_000UL / SampleRateHzForStaticDataset)),
                    SampleRateHz = SampleRateHzForStaticDataset,
                    NumChannels = NumChannelsForStaticDataset,
                    SequenceNumber = (uint)chunkIdx
                };
                signalChunk.PgaGainSettings.Add(1.0f); // Example gain for channel 1
                if (NumChannelsForStaticDataset > 1) signalChunk.PgaGainSettings.Add(1.5f); // Example gain for channel 2

                int samplesForThisChunk = 0;
                for (int s = 0; s < SamplesPerAdcChunk * NumChannelsForStaticDataset; s++)
                {
                    // Ensure we don't generate more samples than StaticDatasetTotalSamples
                    if ((chunkIdx * SamplesPerAdcChunk) + (s / NumChannelsForStaticDataset) >= StaticDatasetTotalSamples) break;
                    signalChunk.RawAdcValues.Add(random.Next(-2000000, 2000000)); // Simulate sint32 ADC values
                    samplesForThisChunk++;
                }
                _logger.LogDebug("    [CHUNK_GEN] Generated chunk {0} with {1} total ADC values ({2} samples per channel).", chunkIdx, samplesForThisChunk, samplesForThisChunk / NumChannelsForStaticDataset);


                currentGetDataResponsePayload.DataChunks.Add(signalChunk);
                chunksInCurrentMessage++;

                if (chunksInCurrentMessage == ChunksPerGetDataResponse || (chunkIdx == StaticDatasetTotalChunks - 1))
                {
                    currentGetDataResponsePayload.FirstChunkSequenceNumberInThisResponse = (uint)(chunkIdx - chunksInCurrentMessage + 1);

                    var dataChunkMain = new Device.Main
                    {
                        Header = new Device.Header
                        {
                            DeviceId = _deviceId,
                            SequenceNumber = (uint)Interlocked.Increment(ref _dataChunkSequenceCounter),
                            TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                        },
                        DataResponse = new Device.DataResponse
                        {
                            CommandType = Device.DataCommandType.ManageData,
                            ManageData = new Device.ManageDataResponse
                            {
                                Operation = Device.DataOperation.DataGet,
                                Get = currentGetDataResponsePayload
                            }
                        }
                    };

                    _logger.LogInformation(
                        "--> [STATIC_DATA_SEND] Sending Main Envelope (ClientSeq: {0}) for Dataset {1}, containing {2} chunks starting at internal_chunk_seq {3}.",
                        dataChunkMain.Header.SequenceNumber,
                        StaticDatasetId,
                        currentGetDataResponsePayload.DataChunks.Count,
                        currentGetDataResponsePayload.FirstChunkSequenceNumberInThisResponse);

                    try
                    {
                        await SendMessageAsync(stream, dataChunkMain, cancellationToken);
                    }
                    catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogError(ex, "[STATIC_DATA_SEND] Failed to send data chunk message for Dataset {0}, chunk_seq {1}", StaticDatasetId, currentGetDataResponsePayload.FirstChunkSequenceNumberInThisResponse);
                        break; // Exit loop on send failure
                    }

                    currentGetDataResponsePayload = new Device.GetDataResponse { DatasetId = StaticDatasetId, TotalChunksInDataset = (uint)StaticDatasetTotalChunks }; // Reset for next batch
                    chunksInCurrentMessage = 0;
                    if (!cancellationToken.IsCancellationRequested) await Task.Delay(20, cancellationToken); // Slightly increased delay
                }
            }
            _logger.LogInformation("[STATIC_DATA_SEND] Task finished for DatasetID: {0}. Nominal Chunks: {1}.", StaticDatasetId, StaticDatasetTotalChunks);
        }


        static async Task SendTriggerAlertsPeriodicallyAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting periodic trigger alert sender (every 25-50s)...");
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(new Random().Next(25, 51)), cancellationToken);
                    var alertPayload = new Device.EventTriggerAlert
                    {
                        TriggerTimestampNs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000UL,
                        TriggerDescription = "Simulated periodic event trigger"
                    };
                    var alertMain = new Device.Main
                    {
                        Header = new Device.Header
                        {
                            DeviceId = _deviceId,
                            SequenceNumber = (uint)Interlocked.Increment(ref _triggerAlertSequenceCounter),
                            TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                        },
                        EventTriggerAlert = alertPayload
                    };
                    _logger.LogInformation("--> Sending EventTriggerAlert (Seq: {0})", alertMain.Header.SequenceNumber);
                    await SendMessageAsync(stream, alertMain, cancellationToken);
                }
                catch (OperationCanceledException) { _logger.LogInformation("Trigger alert sender cancelled."); break; }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested) { _logger.LogError(ex, "Error in trigger alert sender. Will retry."); await Task.Delay(5000, cancellationToken); } // Don't spam on immediate errors
            }
            _logger.LogInformation("Periodic trigger alert sender stopped.");
        }

        static async Task SendErrorAlertsPeriodicallyAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting periodic error alert sender (every 35-70s)...");
            var errorCodes = Enum.GetValues(typeof(Device.ErrorCode)).Cast<Device.ErrorCode>().Where(ec => ec != Device.ErrorCode.NoError && !ec.ToString().Contains("INFO")).ToList();

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(new Random().Next(35, 71)), cancellationToken);
                    var randomErrorCode = errorCodes[new Random().Next(errorCodes.Count)];
                    var alertInfo = new Device.AlertInfo
                    {
                        Code = randomErrorCode,
                        Severity = (randomErrorCode.ToString().Contains("WARN") ? Device.SeverityLevel.SeverityWarning : Device.SeverityLevel.SeverityError),
                        Description = $"Simulated periodic error: {randomErrorCode}",
                        TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };
                    var alertMain = new Device.Main
                    {
                        Header = new Device.Header
                        {
                            DeviceId = _deviceId,
                            SequenceNumber = (uint)Interlocked.Increment(ref _errorAlertSequenceCounter),
                            TimestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                        },
                        ErrorAlert = new Device.ErrorAlert { ErrorInfo = alertInfo }
                    };
                    _logger.LogWarning("--> Sending ErrorAlert (Seq: {0}), Code: {1}", alertMain.Header.SequenceNumber, randomErrorCode);
                    await SendMessageAsync(stream, alertMain, cancellationToken);
                }
                catch (OperationCanceledException) { _logger.LogInformation("Error alert sender cancelled."); break; }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested) { _logger.LogError(ex, "Error in error alert sender. Will retry."); await Task.Delay(5000, cancellationToken); }
            }
            _logger.LogInformation("Periodic error alert sender stopped.");
        }


        static async Task SendMessageAsync(NetworkStream stream, Device.Main message, CancellationToken cancellationToken)
        {
            // IMessage.WriteDelimitedTo handles writing the varint length prefix and then the message.
            await Task.Run(() => message.WriteDelimitedTo(stream), cancellationToken);
            await stream.FlushAsync(cancellationToken); // Ensure data is sent
        }

        // Add this helper method to TestClient Program class
        static Device.SignalDataChunk CreateStaticChunk(int chunkIndex)
        {
            Random random = new Random(); // Consider making Random a class member if called frequently
            var signalChunk = new Device.SignalDataChunk
            {
                StartTimestampNs = StaticDatasetStartTimeNs + ((ulong)chunkIndex * SamplesPerAdcChunk * (1_000_000_000UL / SampleRateHzForStaticDataset)),
                SampleRateHz = SampleRateHzForStaticDataset,
                NumChannels = NumChannelsForStaticDataset,
                SequenceNumber = (uint)chunkIndex
            };
            signalChunk.PgaGainSettings.Add(1.0f);
            if (NumChannelsForStaticDataset > 1) signalChunk.PgaGainSettings.Add(1.5f);

            for (int s = 0; s < SamplesPerAdcChunk * NumChannelsForStaticDataset; s++)
            {
                if ((chunkIndex * SamplesPerAdcChunk) + (s / NumChannelsForStaticDataset) >= StaticDatasetTotalSamples) break;
                signalChunk.RawAdcValues.Add(random.Next(-2000000, 2000000));
            }
            return signalChunk;
        }

        // Simple console logger for the TestClient
        private static class _logger
        {
            private static readonly object _logLock = new object();
            public static void Log(string level, string message)
            {
                lock (_logLock) { Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff} TC-{_deviceId} {level}] {message}"); }
            }
            public static void LogInformation(string message, params object?[] args) => Log("INFO ", string.Format(message, args));
            public static void LogWarning(string message, params object?[] args) => Log("WARN ", string.Format(message, args));
            public static void LogDebug(string message, params object?[] args) => Log("DEBUG", string.Format(message, args));
            public static void LogError(string message, params object?[] args) => Log("ERROR", string.Format(message, args));
            public static void LogError(Exception? ex, string message, params object?[] args)
            {
                string formattedMsg = string.Format(message, args);
                string exceptionDetails = ex != null
                    ? $": {ex.GetType().Name} - {ex.Message}{Environment.NewLine}StackTrace: {ex.StackTrace}"
                    : "";
                Log("ERROR", $"{formattedMsg}{exceptionDetails}");
            }
        }
    }
}
