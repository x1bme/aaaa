FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["src/DeviceProxy.Api/DeviceProxy.Api.csproj", "DeviceProxy.Api/"]
COPY ["src/DeviceProxy.Core/DeviceProxy.Core.csproj", "DeviceProxy.Core/"]
COPY ["src/DeviceProxy.Infrastructure/DeviceProxy.Infrastructure.csproj", "DeviceProxy.Infrastructure/"]
COPY ["src/DeviceProxy.Grpc/DeviceProxy.Grpc.csproj", "DeviceProxy.Grpc/"]
RUN dotnet restore "DeviceProxy.Api/DeviceProxy.Api.csproj"

# Copy everything else and build
COPY ["src/", "."]
RUN dotnet publish "DeviceProxy.Api/DeviceProxy.Api.csproj" -c Release -o /app/publish

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 80
EXPOSE 443
ENTRYPOINT ["dotnet", "DeviceProxy.Api.dll"]
