FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /build_app

# Copy solution file
COPY DeviceCommunication.sln ./

# Copy all project files
COPY src/DeviceCommunication.Api/DeviceCommunication.Api.csproj ./src/DeviceCommunication.Api/
COPY src/DeviceCommunication.Core/DeviceCommunication.Core.csproj ./src/DeviceCommunication.Core/
COPY src/DeviceCommunication.Infrastructure/DeviceCommunication.Infrastructure.csproj ./src/DeviceCommunication.Infrastructure/
COPY src/DeviceCommunication.Grpc/DeviceCommunication.Grpc.csproj ./src/DeviceCommunication.Grpc/

COPY tests/DeviceCommunication.UnitTests/DeviceCommunication.UnitTests.csproj ./tests/DeviceCommunication.UnitTests/
COPY tests/DeviceCommunication.IntegrationTests/DeviceCommunication.IntegrationTests.csproj ./tests/DeviceCommunication.IntegrationTests/

# Restore dependencies
RUN dotnet restore DeviceCommunication.sln

# Copy source code
COPY src/ ./src/
COPY tests/ ./tests/

# Build and publish
RUN dotnet publish src/DeviceCommunication.Api/DeviceCommunication.Api.csproj -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Install PTPd and necessary tools
RUN apt-get update && \
    apt-get install -y \
        ptpd \
        ethtool \
        iproute2 \
        supervisor \
        procps \
        curl && \
    rm -rf /var/lib/apt/lists/*

# Create directories for PTPd
RUN mkdir -p /var/log/ptpd && \
    mkdir -p /var/run/ptpd

# Copy configuration files
COPY ptpd.conf /etc/ptpd.conf
COPY supervisord.conf /etc/supervisor/conf.d/supervisord.conf
COPY startup.sh /app/startup.sh

# Make startup script executable
RUN chmod +x /app/startup.sh

# Copy the published application
COPY --from=build /app/publish .

# Expose ports
# gRPC/HTTP port
EXPOSE 8080
EXPOSE 5002

# TCP device connection port
EXPOSE 12345

# PTP ports (UDP)
EXPOSE 319/udp
EXPOSE 320/udp

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/ || exit 1

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080 \
    PTP_INTERFACE=eth0 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    LC_ALL=en_US.UTF-8 \
    LANG=en_US.UTF-8

# Entry point
ENTRYPOINT ["/app/startup.sh"]