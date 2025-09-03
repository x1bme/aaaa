cat > ptp.conf << 'EOF'
[global]
hybrid_e2e                  0
delay_mechanism             E2E
network_transport           UDPv4
inhibit_multicast_service   1
unicast_listen              1
time_stamping              software
verbose                    1
logging_level              7
priority1                  64
priority2                  64
clockClass                 6

[ens192]
unicast_listen             1
EOF

# Run with UDP transport
docker run --rm -it \
  --name ptp-master \
  --network host \
  --privileged \
  -v $(pwd)/ptp.conf:/etc/ptp.conf:ro \
  ubuntu:22.04 bash -c "
    apt-get update && apt-get install -y linuxptp && \
    ptp4l -f /etc/ptp.conf -i ens192 -m
  "
