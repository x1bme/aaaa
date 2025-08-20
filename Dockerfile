FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /build_app

COPY DeviceCommunication.sln ./

COPY src/DeviceCommunication.Api/DeviceCommunication.Api.csproj ./src/DeviceCommunication.Api/
COPY src/DeviceCommunication.Core/DeviceCommunication.Core.csproj ./src/DeviceCommunication.Core/
COPY src/DeviceCommunication.Infrastructure/DeviceCommunication.Infrastructure.csproj ./src/DeviceCommunication.Infrastructure/
COPY src/DeviceCommunication.Grpc/DeviceCommunication.Grpc.csproj ./src/DeviceCommunication.Grpc/

COPY tests/DeviceCommunication.UnitTests/DeviceCommunication.UnitTests.csproj ./tests/DeviceCommunication.UnitTests/
COPY tests/DeviceCommunication.IntegrationTests/DeviceCommunication.IntegrationTests.csproj ./tests/DeviceCommunication.IntegrationTests/

RUN dotnet restore DeviceCommunication.sln

COPY src/ ./src/
COPY tests/ ./tests/

RUN dotnet publish src/DeviceCommunication.Api/DeviceCommunication.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Install PTPd and necessary tools
RUN apt-get update && \\
    apt-get install -y \\
        ptpd \\
        ethtool \\
        iproute2 \\
        supervisor \\
        procps \\
        curl && \\
    rm -rf /var/lib/apt/lists/*

RUN mkdir -p /var/log/ptpd && \\
    mkdir -p /var/run/ptpd

RUN cat > /etc/ptpd.conf << 'EOL'
# PTPd Master Configuration for DAU Server
[ptpengine]
ptpengine:interface=eth0
ptpengine:preset=masteronly
ptpengine:ip_mode=unicast
ptpengine:unicast_destinations=
ptpengine:domain=0
ptpengine:port_number=1
ptpengine:use_libpcap=n
ptpengine:tx_timestamp_timeout=30
ptpengine:rx_timestamp_timeout=30
ptpengine:hw_timestamping=y
ptpengine:software_timestamping=y
ptpengine:unicast_negotiation=y
ptpengine:unicast_grant_duration=300
ptpengine:unicast_max_destinations=50
ptpengine:event_port=319
ptpengine:general_port=320

[clock]
clock:no_adjust=n
clock:frequency_adjustment_enable=y
clock:step_threshold=0.00002
clock:panic_mode=n

[servo]
servo:kp=0.1
servo:ki=0.001
servo:dt_method=constant

[global]
global:log_level=LOG_INFO
global:log_file=/var/log/ptpd/ptpd.log
global:statistics_file=/var/log/ptpd/ptpd.stats
global:foreground=n
global:verbose_foreground=n
EOL

RUN cat > /etc/supervisor/conf.d/supervisord.conf << 'EOL'
[supervisord]
nodaemon=true
user=root
logfile=/var/log/supervisor/supervisord.log
pidfile=/var/run/supervisord.pid

[program:dotnet]
command=dotnet DeviceCommunication.Api.dll
directory=/app
autostart=true
autorestart=true
startretries=5
stdout_logfile=/var/log/dotnet.log
stderr_logfile=/var/log/dotnet.err.log
environment=ASPNETCORE_ENVIRONMENT="Production",ASPNETCORE_URLS="http://+:8080"
priority=100

[program:ptpd]
command=/usr/sbin/ptpd -c /etc/ptpd.conf
autostart=true
autorestart=true
startretries=3
stdout_logfile=/var/log/ptpd/ptpd.stdout.log
stderr_logfile=/var/log/ptpd/ptpd.stderr.log
priority=10
EOL

RUN cat > /app/startup.sh << 'EOL'
#!/bin/bash
set -e

echo "Starting Device Communication Server with PTPd..."

INTERFACE=${PTP_INTERFACE:-eth0}
echo "Using network interface: $INTERFACE"

sed -i "s/ptpengine:interface=.*/ptpengine:interface=$INTERFACE/" /etc/ptpd.conf

echo "Checking hardware timestamping capabilities..."
if ethtool -T $INTERFACE 2>/dev/null | grep -q "hardware-transmit"; then
    echo "Hardware timestamping is supported on $INTERFACE"
    ethtool -K $INTERFACE rx-all on 2>/dev/null || true
    ethtool -K $INTERFACE tx on 2>/dev/null || true
    ethtool -K $INTERFACE rx on 2>/dev/null || true
    sed -i 's/ptpengine:hw_timestamping=.*/ptpengine:hw_timestamping=y/' /etc/ptpd.conf
else
    echo "Hardware timestamping not available, using software timestamping"
    sed -i 's/ptpengine:hw_timestamping=.*/ptpengine:hw_timestamping=n/' /etc/ptpd.conf
fi

if command -v iptables &> /dev/null; then
    echo "Setting up firewall rules for PTP..."
    iptables -A INPUT -p udp --dport 319 -j ACCEPT 2>/dev/null || true
    iptables -A INPUT -p udp --dport 320 -j ACCEPT 2>/dev/null || true
    iptables -A INPUT -d 224.0.1.129 -j ACCEPT 2>/dev/null || true
    iptables -A INPUT -d 224.0.0.107 -j ACCEPT 2>/dev/null || true
fi

mkdir -p /var/run/ptpd
mkdir -p /var/log/ptpd

echo "Starting services..."
exec /usr/bin/supervisord -c /etc/supervisor/conf.d/supervisord.conf
EOL

RUN chmod +x /app/startup.sh

COPY --from=build /app/publish .

EXPOSE 8080
EXPOSE 5002
EXPOSE 12345
EXPOSE 319/udp
EXPOSE 320/udp

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \\
    CMD curl -f http://localhost:8080/ || exit 1

ENV ASPNETCORE_ENVIRONMENT=Production \\
    ASPNETCORE_URLS=http://+:8080 \\
    PTP_INTERFACE=eth0 \\
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \\
    LC_ALL=en_US.UTF-8 \\
    LANG=en_US.UTF-8

ENTRYPOINT ["/app/startup.sh"]
