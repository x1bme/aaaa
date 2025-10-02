FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["src/ApiGateway.Api/ApiGateway.Api.csproj", "ApiGateway.Api/"]
COPY ["src/ApiGateway.Core/ApiGateway.Core.csproj", "ApiGateway.Core/"]
COPY ["src/ApiGateway.Infrastructure/ApiGateway.Infrastructure.csproj", "ApiGateway.Infrastructure/"]
COPY ["src/ApiGateway.Grpc/ApiGateway.Grpc.csproj", "ApiGateway.Grpc/"]
RUN dotnet restore "ApiGateway.Api/ApiGateway.Api.csproj"

# Copy everything else and build
COPY ["src/", "."]
RUN dotnet publish "ApiGateway.Api/ApiGateway.Api.csproj" -c Release -o /app/publish

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 80
EXPOSE 443
ENTRYPOINT ["dotnet", "ApiGateway.Api.dll"]
