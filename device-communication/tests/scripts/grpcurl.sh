# List services
grpcurl -plaintext localhost:5002 list

# List methods
grpcurl -plaintext localhost:5002 describe device.communication.DeviceCommunicationService

# Test connect device
 grpcurl -plaintext -d '{"device_id": "device001", "connection_params": "{}"}' \
     localhost:5002 device.communication.DeviceCommunicationService/ConnectDevice

# Check device status
 grpcurl -plaintext -d '{"device_id": "device001"}' \
      localhost:5002 device.communication.DeviceCommunicationService/GetDeviceStatus

# Send data to device
grpcurl -plaintext -d '{
    "device_id": "device001",
        "data": "SGVsbG8gRGV2aWNlIQ=="
        }' localhost:5002 device.communication.DeviceCommunicationService/SendToDevice

# Receive data from device
grpcurl -plaintext -d '{"device_id": "device001"}' \
    localhost:5002 device.communication.DeviceCommunicationService/ReceiveFromDevice


# Stream signal data to device (last number in the array is an approximation of something very cool)
# Run this command carefully..
grpcurl -d @ localhost:5002 device.communication.DeviceCommunicationService/StreamSignalData << EOM
{
  "device_id": "device001",
  "timestamp": $(date +%s000),
  "values": [1.0, 2.0, 3.0, 3.14159],
  "channel_id": "channel1",
  "sample_rate": "1000"
}
{
  "device_id": "device001",
  "timestamp": $(date +%s000),
  "values": [4.0, 5.0, 6.0],
  "channel_id": "channel1",
  "sample_rate": "1000"
}
EOM

# Test storing signal data (less error prone than the command directly above for streaming)
grpcurl -plaintext -d '{
 "device_id": "device001",
 "timestamp": 1701312000000,
 "values": [1.0, 2.0, 3.0, 3.14],
 "channel_id": "channel1",
 "sample_rate": "1000"
}' localhost:5002 device.communication.DeviceCommunicationService/StreamSignalData

# Test retrieving stored data
grpcurl -plaintext -d '{
 "device_id": "device001",
 "from_timestamp": 1701312000000,
 "to_timestamp": 1701398400000
}' localhost:5002 device.communication.DeviceCommunicationService/GetStoredSignalData
