# Stage 1: build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first to leverage Docker layer caching.
# The paths are relative to the build context (repository root).
COPY src/database/ArchiveImporter/ArchiveImporter.csproj ./database/ArchiveImporter/
COPY src/database/Archiver/Archiver.csproj ./database/Archiver/
COPY src/database/DataAccess/DataAccess.csproj ./database/DataAccess/

# Restore dependencies for the main project based on the .csproj files.
RUN dotnet restore ./database/ArchiveImporter/ArchiveImporter.csproj

# Copy the rest of the source code from the host's 'src' directory
# into the container's '/src' directory.
COPY src/ .

# Publish the application
WORKDIR "/src/database/ArchiveImporter"
RUN dotnet publish "ArchiveImporter.csproj" -c Release -o /app/publish

# Stage 2: runtime
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "ArchiveImporter.dll"]