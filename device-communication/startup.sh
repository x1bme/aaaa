#!/bin/bash
set -e

echo "Starting Device Communication Server with PTPd..."

# Detect network interface
INTERFACE=${PTP_INTERFACE:-eth0}
echo "Using network interface: $INTERFACE"

# Update interface in PTPd configuration
sed -i "s/ptpengine:interface=.*/ptpengine:interface=$INTERFACE/" /etc/ptpd.conf

# Check for hardware timestamping support
echo "Checking hardware timestamping capabilities..."
if ethtool -T $INTERFACE 2>/dev/null | grep -q "hardware-transmit"; then
    echo "Hardware timestamping is supported on $INTERFACE"
    
    # Enable hardware timestamping
    ethtool -K $INTERFACE rx-all on 2>/dev/null || true
    ethtool -K $INTERFACE tx on 2>/dev/null || true
    ethtool -K $INTERFACE rx on 2>/dev/null || true
    
    # Ensure PTPd uses hardware timestamping
    sed -i 's/ptpengine:hw_timestamping=.*/ptpengine:hw_timestamping=y/' /etc/ptpd.conf
else
    echo "Hardware timestamping not available, using software timestamping"
    sed -i 's/ptpengine:hw_timestamping=.*/ptpengine:hw_timestamping=n/' /etc/ptpd.conf
fi

# Set up iptables rules for PTP if available
if command -v iptables &> /dev/null; then
    echo "Setting up firewall rules for PTP..."
    # Allow PTP event messages (port 319)
    iptables -A INPUT -p udp --dport 319 -j ACCEPT 2>/dev/null || true
    # Allow PTP general messages (port 320)
    iptables -A INPUT -p udp --dport 320 -j ACCEPT 2>/dev/null || true
    # Allow PTP multicast addresses
    iptables -A INPUT -d 224.0.1.129 -j ACCEPT 2>/dev/null || true
    iptables -A INPUT -d 224.0.0.107 -j ACCEPT 2>/dev/null || true
fi

# Create required directories
mkdir -p /var/run/ptpd
mkdir -p /var/log/ptpd

echo "Starting services..."
exec /usr/bin/supervisord -c /etc/supervisor/conf.d/supervisord.conf