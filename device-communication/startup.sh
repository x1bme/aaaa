# Detect network interface
INTERFACE=${PTP_INTERFACE:-eth0}
echo "Using network interface: $INTERFACE"

# Check if interface exists
if ! ip link show $INTERFACE > /dev/null 2>&1; then
    echo "ERROR: Interface $INTERFACE does not exist!"
    echo "Available interfaces:"
    ip link show
    
    # Try to find the first non-loopback interface
    INTERFACE=$(ip route | grep default | awk '{print $5}' | head -n1)
    if [ -z "$INTERFACE" ]; then
        echo "ERROR: Could not find any suitable network interface"
        exit 1
    fi
    echo "Using interface: $INTERFACE"
fi

# Wait for interface to have an IP address
echo "Waiting for interface $INTERFACE to get an IP address..."
timeout=30
count=0
while [ $count -lt $timeout ]; do
    if ip addr show $INTERFACE | grep -q "inet "; then
        echo "Interface $INTERFACE has an IP address"
        break
    fi
    echo "Waiting for IP address on $INTERFACE... ($count/$timeout)"
    sleep 1
    count=$((count + 1))
done

if [ $count -eq $timeout ]; then
    echo "ERROR: Interface $INTERFACE never got an IP address"
    exit 1
fi

# Update interface in PTPd configuration
sed -i "s/ptpengine:interface=.*/ptpengine:interface=$INTERFACE/" /etc/ptpd.conf
