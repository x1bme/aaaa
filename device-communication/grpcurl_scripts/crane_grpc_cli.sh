#!/bin/bash

# --- Configuration (CHANGE if NECESSARY) ---
GRPC_SERVER_ADDR="localhost:5002"
GRPC_SERVICE_NAME="simple_device_control_api.SimpleDeviceController" # Primarily for the grpc web client in Loom, but re-using it for testing with grpcurl.
DEFAULT_DEVICE_ID="test-device-001" # To be replaced with either MAC address or STM32's 96-bit unique identifier
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PAYLOAD_DIR="${SCRIPT_DIR}/grpc_payloads"

# --- Helper Function (DONT CHANGE) ---
_call_grpc() {
    local method_name="$1"
    local payload_data_or_file_ref="$2"
    shift 2 # Decrement positional parameter counter by 2
    local extra_options=("$@") # Capture any additional grpcurl options like -H

    echo "-----------------------------------------------------"
    echo "Calling Method : ${method_name}"
    echo "Raw payload_data_or_file_ref argument: '${payload_data_or_file_ref}'"

    local data_for_grpcurl_d_option="" # actual JSON string

    if [[ "${payload_data_or_file_ref}" == "@"* ]]; then
        # Case 1: Argument is a file reference
        local file_path="${payload_data_or_file_ref#@}"
        echo "Payload reference is a file: ${file_path}"
        if [ -f "${file_path}" ]; then
            IFS= read -r -d '' data_for_grpcurl_d_option < "${file_path}"
            
            echo "Content read from file '${file_path}':"
            if [ ${#data_for_grpcurl_d_option} -gt 500 ]; then
                echo "${data_for_grpcurl_d_option:0:500} ... (content truncated for display)"
            else
                echo "${data_for_grpcurl_d_option}"
            fi
        else
            echo "ERROR: Payload file '${file_path}' not found!"
            echo "-----------------------------------------------------"
            return 1 # Exit function with an error status
        fi
    elif [ -n "${payload_data_or_file_ref}" ]; then
        # Case 2: Argument is already an inline JSON string
        echo "Payload reference is an inline JSON string."
        data_for_grpcurl_d_option="${payload_data_or_file_ref}"
        echo "Inline JSON: ${data_for_grpcurl_d_option}"
    else
        # Case 3: No payload data provided (for methods without a request body)
        echo "No payload data provided for -d option."
    fi

    echo "-----------------------------------------------------"
    echo "Executing grpcurl..."
    
    if [ -n "${data_for_grpcurl_d_option}" ]; then
        # If there's data, use the -d option
        grpcurl -plaintext -d "${data_for_grpcurl_d_option}" "${extra_options[@]}" "${GRPC_SERVER_ADDR}" "${GRPC_SERVICE_NAME}/${method_name}"
    else
        # If no data, call grpcurl without -d
        grpcurl -plaintext "${extra_options[@]}" "${GRPC_SERVER_ADDR}" "${GRPC_SERVICE_NAME}/${method_name}"
    fi
    
    local exit_status=$?
    echo "-----------------------------------------------------"
    echo 
    return ${exit_status}
}

# --- gRPC Method Aliases (Reflects commands in Firmware release plan 3452F02, Rev. 4) ---

# List Services (no payload argument needed for grpcurl's list command)
grpcListServices() {
    echo "-----------------------------------------------------"
    echo "Calling: list services"
    echo "-----------------------------------------------------"
    grpcurl -plaintext "${GRPC_SERVER_ADDR}" list
    echo "-----------------------------------------------------"
    echo
}

# Describe Service (no payload argument needed for grpcurl's describe command)
grpcDescribeService() {
    echo "-----------------------------------------------------"
    echo "Calling: describe ${GRPC_SERVICE_NAME}"
    echo "-----------------------------------------------------"
    grpcurl -plaintext "${GRPC_SERVER_ADDR}" describe "${GRPC_SERVICE_NAME}"
    echo "-----------------------------------------------------"
    echo
}


# SendDeviceHeartbeat
# Usage: grpcHeartbeat
# No JSON file referenced here! Thought the input was short enough anyways.
grpcHeartbeat() {
    local device_id="${1:-$DEFAULT_DEVICE_ID}"
    _call_grpc "SendDeviceHeartbeat" "{\"device_id\": \"${device_id}\"}"
}

# GetDeviceHealthStatus
# Usage: grpcHealthStatus
# NO JSON file referenced here!
grpcHealthStatus() {
    local device_id="${1:-$DEFAULT_DEVICE_ID}"
    _call_grpc "GetDeviceHealthStatus" "{\"device_id\": \"${device_id}\"}"
}

# GetDeviceFirmwareInfo
# Usage: grpcFirmwareInfo
grpcFirmwareInfo() {
    local device_id="${1:-$DEFAULT_DEVICE_ID}"
    _call_grpc "GetDeviceFirmwareInfo" "{\"device_id\": \"${device_id}\"}"
}

# GetDeviceDataset
# Usage: grpcDataGet
# Uses JSON file for input parameters
grpcDataGet() {
    _call_grpc "GetDeviceDataset" "@${PAYLOAD_DIR}/data_get_request.json"
}

# ConfigureDeviceDataCollection
# Usage: grpcDataConfigure
# Uses JSON file for input parameters
grpcDataConfigure() {
    _call_grpc "ConfigureDeviceDataCollection" "@${PAYLOAD_DIR}/data_configure_request.json"
}

# PrepareFirmwareUpdate
# Usage: grpcFirmwarePrepare
# Uses JSON file for input parameters
grpcFirmwarePrepare() {
    _call_grpc "PrepareFirmwareUpdate" "@${PAYLOAD_DIR}/firmware_prepare_request.json"
}

# ExecuteFirmwareUpdateOperation - VERIFY
# Usage: grpcFirmwareVerify
# Uses JSON file for input parameters
grpcFirmwareVerify() {
    _call_grpc "ExecuteFirmwareUpdateOperation" "@${PAYLOAD_DIR}/firmware_verify_request.json"
}

# ExecuteFirmwareUpdateOperation - APPLY
# Usage: grpcFirmwareApply
# Uses JSON file for input parameters
grpcFirmwareApply() {
    _call_grpc "ExecuteFirmwareUpdateOperation" "@${PAYLOAD_DIR}/firmware_apply_request.json"
}

# ExecuteFirmwareUpdateOperation - ABORT
# Usage: grpcFirmwareAbort
# Uses JSON file for input parameters
grpcFirmwareAbort() {
    _call_grpc "ExecuteFirmwareUpdateOperation" "@${PAYLOAD_DIR}/firmware_abort_request.json"
}

# ServerInitiatedStartCapture
grpcStartCapture() {
    local device_id="${1:-$DEFAULT_DEVICE_ID}"
    local trigger_ts_ns="${2:-$(date +%s%N)}"
    _call_grpc "ServerInitiatedStartCapture" "{\"device_id\": \"${device_id}\", \"trigger_timestamp_ns\": \"${trigger_ts_ns}\"}"
}

# --- Calibration ---
grpcCalReadParams() {
    _call_grpc "ReadDeviceCalibrationParams" "@${PAYLOAD_DIR}/calibration_read_params_request.json"
}

grpcCalStart() {
    _call_grpc "StartDeviceCalibrationProcedure" "@${PAYLOAD_DIR}/calibration_start_request.json"
}

grpcCalGetStatus() {
    local device_id_payload="{\"device_id\": \"${1:-$DEFAULT_DEVICE_ID}\"}"
    _call_grpc "GetDeviceCalibrationStatus" "${device_id_payload}"
}

# --- device ---
grpcDeviceSetName() {
    _call_grpc "SetDeviceAssignedName" "@${PAYLOAD_DIR}/device_set_assigned_name_request.json"
}

grpcDeviceGetNetworkConfig() {
    local device_id_payload="{\"device_id\": \"${1:-$DEFAULT_DEVICE_ID}\"}"
    _call_grpc "GetDeviceNetworkConfig" "${device_id_payload}"
}

grpcDeviceSetNetworkConfig() {
    _call_grpc "SetDeviceNetworkConfig" "@${PAYLOAD_DIR}/device_set_network_config_request.json"
}

grpcDeviceGetCertInfo() {
    local device_id_payload="{\"device_id\": \"${1:-$DEFAULT_DEVICE_ID}\"}"
    _call_grpc "GetDeviceCertificateInfo" "${device_id_payload}"
}

grpcDeviceGenerateCSR() { # Assuming CSR request only needs device_id (for now) or is empty
    local device_id_payload="{\"device_id\": \"${1:-$DEFAULT_DEVICE_ID}\"}"
    _call_grpc "GenerateDeviceCSR" "${device_id_payload}"
}

grpcDeviceUpdateCert() {
    _call_grpc "UpdateDeviceCertificate" "@${PAYLOAD_DIR}/device_update_certificate_request.json"
}

grpcDeviceReboot() {
    _call_grpc "RebootDevice" "@${PAYLOAD_DIR}/device_reboot_request.json"
}

grpcDeviceFactoryReset() {
    _call_grpc "FactoryResetDevice" "@${PAYLOAD_DIR}/device_factory_reset_request.json"
}

grpcDeviceSyncTime() {
    local device_id="${1:-$DEFAULT_DEVICE_ID}"
    local server_ts_ms="${2:-$(($(date +%s%N)/1000000))}"
    _call_grpc "SyncDeviceTime" "{\"device_id\": \"${device_id}\", \"server_timestamp_ms_for_sync\": \"${server_ts_ms}\"}"
}

# --- Apigwsvc grpc calls (this script is mainly for Indesign, so I'll probably remove this later) ---
grpcGetAllDaus() {
    local PAYLOAD_FILE="@${PAYLOAD_DIR}/get_all_daus_request.json"
    _call_grpc "GetAllDaus" "${PAYLOAD_FILE}"
}

grpcUpdateDauFirmware() {
    local PAYLOAD_FILE="@${PAYLOAD_DIR}/update_dau_firmware_payload.json"
    _call_grpc "UpdateFirmware" "${PAYLOAD_FILE}"
}

grpcConfigureSingleDau() {
    local PAYLOAD_FILE="@${PAYLOAD_DIR}/configure_dau_payload.json"
    _call_grpc "ConfigureDau" "${PAYLOAD_FILE}"
}


# --- Make default payload templates if not existing ---
_ensure_payload_templates() {
    mkdir -p "${PAYLOAD_DIR}"
    local files_to_create_if_missing=(
        "health_heartbeat_request.json:{\"device_id\": \"${DEFAULT_DEVICE_ID}\"}"
        "data_get_request.json:{\"device_id\": \"${DEFAULT_DEVICE_ID}\",\"dataset_id\": 777,\"start_chunk_sequence_number\": 0,\"max_chunks_in_response_hint\": 5}"
        "data_configure_request.json:{\"device_id\": \"${DEFAULT_DEVICE_ID}\",\"sampling_rate_hz\": 1000,\"channel_configs\": [{\"channel_id\":1,\"enabled\":true}],\"total_storage_allocation_kb\": 10240}"
        "firmware_prepare_request.json:{\"device_id\": \"${DEFAULT_DEVICE_ID}\",\"firmware_size_bytes\": 10240,\"firmware_version\": \"1.0.0-sim\",\"signature\":\"c2ltdWxhdGVkLXNpZ25hdHVyZQ==\",\"block_size_preference\":512}"
        "firmware_verify_request.json:{\"device_id\": \"${DEFAULT_DEVICE_ID}\",\"operation\": \"FW_OP_GRPC_VERIFY\",\"total_blocks_sent_for_verify\":20,\"full_image_crc32_for_verify\":12345}"
        "firmware_apply_request.json:{\"device_id\": \"${DEFAULT_DEVICE_ID}\",\"operation\": \"FW_OP_GRPC_APPLY\",\"reboot_delay_seconds_for_apply\":5}"
        "firmware_abort_request.json:{\"device_id\": \"${DEFAULT_DEVICE_ID}\",\"operation\": \"FW_OP_GRPC_ABORT\",\"reason_for_abort\":\"Test Abort from CLI\"}"
        "calibration_read_params_request.json:{\"device_id\":\"${DEFAULT_DEVICE_ID}\",\"channel_ids\":[]}"
        "calibration_start_request.json:{\"device_id\":\"${DEFAULT_DEVICE_ID}\",\"force_calibration\":false,\"channel_ids\":[]}"
        "device_set_assigned_name_request.json:{\"device_id\":\"${DEFAULT_DEVICE_ID}\",\"assigned_name\":\"NewDeviceName\"}"
        "device_set_network_config_request.json:{\"device_id\":\"${DEFAULT_DEVICE_ID}\",\"settings\":{\"use_dhcp\":true,\"static_ip_address\":\"\",\"subnet_mask\":\"\",\"gateway\":\"\",\"primary_dns\":\"\",\"secondary_dns\":\"\"}}"
        "device_update_certificate_request.json:{\"device_id\":\"${DEFAULT_DEVICE_ID}\",\"new_certificate_der\":\"aGh1YQo=\"}"
        "device_reboot_request.json:{\"device_id\":\"${DEFAULT_DEVICE_ID}\",\"force_immediate\":false,\"delay_seconds\":5}"
        "device_factory_reset_request.json:{\"device_id\":\"${DEFAULT_DEVICE_ID}\",\"confirmation_code\":\"CONFIRM_RESET\",\"preserve_device_id\":true,\"preserve_network_config\":false,\"preserve_calibration\":false}"
        "get_all_daus_request.json:{\"device_id\":[]}" # Corresponds to DauList message
        "update_dau_firmware_payload.json:{\"device_id\":\"${DEFAULT_DEVICE_ID}\",\"version\":\"0.0.0\",\"checksum\":\"\",\"firmware_data\":\"aGh1YQo=\",\"firmware_type\":\"main\",\"timestamp\":0}" # Corresponds to UpdatePayload message
        "configure_dau_payload.json:{\"dau\":{\"device_id\":\"${DEFAULT_DEVICE_ID}\"}}" # Corresponds to Configuration message
    )

    for item in "${files_to_create_if_missing[@]}"; do
        IFS=":" read -r fname fcontent <<< "$item"
        local full_path="${PAYLOAD_DIR}/${fname}"
        if [ ! -f "${full_path}" ]; then
            echo --------
            echo "INFO: Payload template '${full_path}' not found. Creating a minimal example."
            echo "      Please edit this file with appropriate values for the '${fname%%_request.json}' command."
            echo "${fcontent}" > "${full_path}"
        fi
    done
}

_ensure_payload_templates

# --- Help Function ---
grpcHelp() {
    echo "Available gRPC CLI commands:"
    echo "  (Commands referencing a JSON file expect you to edit the file in '${PAYLOAD_DIR}/')"
    echo
    echo "  grpcListServices             - List all available gRPC services"
    echo "  grpcDescribeService          - Describe the methods of '${GRPC_SERVICE_NAME}'"
    echo "  grpcHeartbeat [dev_id]       - Send heartbeat (dev_id optional, defaults to ${DEFAULT_DEVICE_ID})"
    echo "  grpcHealthStatus [dev_id]    - Get health status (dev_id optional)"
    echo "  grpcFirmwareInfo [dev_id]    - Get firmware info (dev_id optional)"
    echo
    echo "  grpcDataGet                  - Uses '${PAYLOAD_DIR}/data_get_request.json'"
    echo "  grpcDataConfigure            - Uses '${PAYLOAD_DIR}/data_configure_request.json'"
    echo "  grpcFirmwarePrepare          - Uses '${PAYLOAD_DIR}/firmware_prepare_request.json'"
    echo "  grpcFirmwareVerify           - Uses '${PAYLOAD_DIR}/firmware_verify_request.json'"
    echo "  grpcFirmwareApply            - Uses '${PAYLOAD_DIR}/firmware_apply_request.json'"
    echo "  grpcFirmwareAbort            - Uses '${PAYLOAD_DIR}/firmware_abort_request.json'"
    echo "  grpcStartCapture [dev_id] [ts_ns] - Initiate server-side capture (dev_id, ts_ns optional)"
    echo
    echo "  grpcCalReadParams            - Uses '${PAYLOAD_DIR}/calibration_read_params_request.json'"
    echo "  grpcCalStart                 - Uses '${PAYLOAD_DIR}/calibration_start_request.json'"
    echo "  grpcCalGetStatus             - Get calibration status (dev_id optional)"
    echo
    echo "  grpcDeviceSetName            - Uses '@${PAYLOAD_DIR}/device_set_assigned_name_request.json'"
    echo "  grpcDeviceGetNetworkConfig   - Get network configuration details (dev_id optional)"
    echo "  grpcDeviceSetNetworkConfig   - Uses '@${PAYLOAD_DIR}/device_set_network_config_request.json'"
    echo "  grpcDeviceGetCertInfo        - Get TLS cert info (dev_id optional)"
    echo "  grpcDeviceGenerateCSR        - Generate CSR, may use a json file in future (dev_id optional)"
    echo "  grpcDeviceUpdateCert         - Uses '@${PAYLOAD_DIR}/device_update_certificate_request.json'"
    echo "  grpcDeviceReboot             - Uses '@${PAYLOAD_DIR}/device_reboot_request.json'"
    echo "  grpcDeviceFactoryReset       - Uses '@${PAYLOAD_DIR}/device_factory_reset_request.json'"
    echo "  grpcDeviceSyncTime [dev_id] [serv_ts_ms] - Sync Device time (dev_id, server_ts_ms optional), TODO json file input later"
    echo
    echo "  grpcHelp                     - Show this help message."
    echo
    echo "To use commands like 'grpcDataGet':"
    echo "  1. Edit the corresponding JSON file in '${PAYLOAD_DIR}/' with desired parameters."
    echo "  2. Run the simple command (e.g., 'grpcDataGet')."
    echo "**NOTE**: I know there are some commands in R0.8 that aren't populated here, but I'll add"
    echo "**NOTE**: it eventually in a future update. Very easy to modify!"
}

# If script is run directly, show help. If sourced, aliases are defined.
if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
    grpcHelp
fi

# Checklist from Firmware release plan 3452F02, Rev. 4
# --- Health ---
# Heartbeat (OK)
# HealthStatus (OK)

# --- Firmware ---
# FirmwareInfo (OK)
# UpdateFirmware (OK, everything except for Transfer, later maybe?)

# --- Data ---
# GetData (OK, req <-> resp flow modified to 1-1 as opposed to 1-many, most likely better, change/discuss later, TODO)
# ClearData
# EventTriggerAlert (Device -> Server) [Indesign tested, Bill used this to test event trigger, StartCapture sent back to source device]
# Configure (OK)

# --- Calibration ---
# ManageCalibration (ReadParams, Start, GetStatus)

# --- Device ---
# DeviceConfig (Set/Get Network config, TLS info
# DeviceControl (reboot)
# SyncTime (OK)
# ErrorAlert (Device -> Server) [Indesign tested, Ben used NO_ERROR to for device id registration on server]
# FactoryReset (OK)
