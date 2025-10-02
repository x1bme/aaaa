<!-- filepath: c:\Users\htopping\source\repos\gemini-server\Archiver\README.md -->
# Archiver – Database Setup and Run

## 1. Run MySQL via Docker
```bash
docker run --name mysql-archiver \
  -e MYSQL_ROOT_PASSWORD=123456 \
  -e MYSQL_DATABASE=ArchiverDb \
  -p 127.0.0.1:5051:3306 \
  -d mysql:latest
```

## 2. Configure `appsettings.json`
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=mysql-archiver;Database=ArchiverDb;User=root;Password=123456;"
  }
}
```

## 3. Update `Archive` Model
The `Archive` table now has JSON columns for each file:
- Providers  
- MOV  
- MathItems  
- Criteria  
- Actuators  
- ValveImages  
- Sensors  

Each is stored as a MySQL `JSON` column.

## 4. Add & Apply EF Core Migration
```bash
dotnet ef migrations add AddArchiveJsonFields --project Archiver.csproj
dotnet ef database update --project Archiver.csproj
```

## 5. Run Archiver
```bash
dotnet run --project Archiver.csproj
```