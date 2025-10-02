# List services
grpcurl -plaintext localhost:5004 list

# List methods
grpcurl -plaintext localhost:5004 describe device.proxy.DeviceProxyService

# Start a subscription for DAU1 (run in a separate terminal)
grpcurl -plaintext -d '{"dau_id": "DAU1"}' \
    -max-time 999999 \
    localhost:5004 device.proxy.DeviceProxyService/SubscribeToMessages

# Send test messages (in another terminal)
grpcurl -plaintext -d '{
    "source_dau_id": "DAU2",
    "target_dau_id": "DAU1",
    "message_data": "SGVsbG8gREFVMSE=",
    "message_id": "msg123"
}' localhost:5004 device.proxy.DeviceProxyService/RelayMessage

# Test error scenarios:
grpcurl -plaintext -d '{
    "source_dau_id": "DAU2",
    "target_dau_id": "NON_EXISTENT",
    "message_data": "VGVzdA==",
    "message_id": "msg789"
}' localhost:5004 device.proxy.DeviceProxyService/RelayMessage


# Several subscribers
# Terminal 1 - Subscribe DAU1
grpcurl -plaintext -d '{"dau_id": "DAU1"}' \
    -max-time 999999 \
    localhost:5004 device.proxy.DeviceProxyService/SubscribeToMessages

# Terminal 2 - Subscribe DAU2
grpcurl -plaintext -d '{"dau_id": "DAU2"}' \
    -max-time 999999 \
    localhost:5004 device.proxy.DeviceProxyService/SubscribeToMessages

# Terminal 3 - Subscribe DAU3
grpcurl -plaintext -d '{"dau_id": "DAU3"}' \
    -max-time 999999 \
    localhost:5004 device.proxy.DeviceProxyService/SubscribeToMessages

# Terminal 4 - Send test messages
# Message from DAU1 to DAU2
grpcurl -plaintext -d '{
    "source_dau_id": "DAU1",
    "target_dau_id": "DAU2",
    "message_data": "TWVzc2FnZSBmcm9tIERBVTEgdG8gREFVMg==",
    "message_id": "msg_1_to_2"
}' localhost:5004 device.proxy.DeviceProxyService/RelayMessage

# Message from DAU2 to DAU3
grpcurl -plaintext -d '{
    "source_dau_id": "DAU2",
    "target_dau_id": "DAU3",
    "message_data": "TWVzc2FnZSBmcm9tIERBVTIgdG8gREFVMw==",
    "message_id": "msg_2_to_3"
}' localhost:5004 device.proxy.DeviceProxyService/RelayMessage

# Message from DAU3 to DAU1
grpcurl -plaintext -d '{
    "source_dau_id": "DAU3",
    "target_dau_id": "DAU1",
    "message_data": "TWVzc2FnZSBmcm9tIERBVTMgdG8gREFVMQ==",
    "message_id": "msg_3_to_1"
}' localhost:5004 device.proxy.DeviceProxyService/RelayMessage

# Broadcast-style: DAU1 sending to both DAU2 and DAU3
grpcurl -plaintext -d '{
    "source_dau_id": "DAU1",
    "target_dau_id": "DAU2",
    "message_data": "QnJvYWRjYXN0IG1lc3NhZ2UgMQ==",
    "message_id": "broadcast_1"
}' localhost:5004 device.proxy.DeviceProxyService/RelayMessage

grpcurl -plaintext -d '{
    "source_dau_id": "DAU1",
    "target_dau_id": "DAU3",
    "message_data": "QnJvYWRjYXN0IG1lc3NhZ2UgMQ==",
    "message_id": "broadcast_2"
}' localhost:5004 device.proxy.DeviceProxyService/RelayMessage


# Several subscribers error case testing:
# Send to non-existent DAU
grpcurl -plaintext -d '{
    "source_dau_id": "DAU1",
    "target_dau_id": "NON_EXISTENT",
    "message_data": "RXJyb3IgdGVzdA==",
    "message_id": "error_msg_1"
}' localhost:5004 device.proxy.DeviceProxyService/RelayMessage

# Send with empty source
grpcurl -plaintext -d '{
    "source_dau_id": "",
    "target_dau_id": "DAU2",
    "message_data": "RXJyb3IgdGVzdA==",
    "message_id": "error_msg_2"
}' localhost:5004 device.proxy.DeviceProxyService/RelayMessage

# Send with empty target
grpcurl -plaintext -d '{
    "source_dau_id": "DAU1",
    "target_dau_id": "",
    "message_data": "RXJyb3IgdGVzdA==",
    "message_id": "error_msg_3"
}' localhost:5004 device.proxy.DeviceProxyService/RelayMessage

# Send with empty message data
grpcurl -plaintext -d '{
    "source_dau_id": "DAU1",
    "target_dau_id": "DAU2",
    "message_data": "",
    "message_id": "error_msg_4"
}' localhost:5004 device.proxy.DeviceProxyService/RelayMessage
