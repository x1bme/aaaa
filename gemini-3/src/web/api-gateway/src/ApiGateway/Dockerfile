# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["ApiGateway.csproj", "."]
RUN dotnet restore "ApiGateway.csproj"

# Copy everything else and build
COPY . .
WORKDIR /src
RUN dotnet build "ApiGateway.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "ApiGateway.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Install curl for health checks and openssl for certificates
RUN apt-get update && apt-get install -y curl openssl && rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=publish /app/publish .

# Create non-root user
RUN useradd -m -u 1001 appuser && chown -R appuser:appuser /app
USER appuser

ENTRYPOINT ["dotnet", "ApiGateway.dll"]
