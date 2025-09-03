# Get your main interface
INTERFACE=$(ip route | grep default | awk '{print $5}' | head -n1)
echo "Using interface: $INTERFACE"

# Create config with your actual interface
cat > ptp.conf << EOF
[global]
hybrid_e2e                  1
inhibit_multicast_service   1
unicast_listen              1
time_stamping              software
verbose                    1
logging_level              7
priority1                  64
priority2                  64
clockClass                 6

[$INTERFACE]
unicast_listen             1
EOF

# Run with detected interface
docker run --rm -it \
  --name ptp-master \
  --network host \
  --privileged \
  -v $(pwd)/ptp.conf:/etc/ptp.conf:ro \
  ubuntu:22.04 bash -c "
    apt-get update && apt-get install -y linuxptp && \
    ptp4l -f /etc/ptp.conf -i $INTERFACE -m
  "
