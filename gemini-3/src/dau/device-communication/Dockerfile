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
RUN dotnet publish src/DeviceCommunication.Api/DeviceCommunication.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 5002
EXPOSE 12345

ENTRYPOINT ["dotnet", "DeviceCommunication.Api.dll"]

