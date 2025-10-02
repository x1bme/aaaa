## Running with docker:
Figuring this out

### generic mysql container:
`docker run --name mysql-gemini -e MYSQL_ROOT_PASSWORD=123456 -e MYSQL_DATABASE=TestAuthDb -p 127.0.0.1:5050:3306 -d mysql:latest`

### Updating databse using dotnet ef:
Create migrations: `dotnet ef migrations add init`
Update migrations: `dotnet ef database update` 

### run dotnet app:
`dotnet run`
