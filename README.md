## TravelPathways Backend (ASP.NET Core + SQL Server)

This folder contains the backend API for the Angular app.

### Prerequisites

- .NET SDK 8.x
- SQL Server (one of):
  - SQL Server Express / Developer
  - LocalDB (optional)

### Configure DB connection

The connection string in `backend/src/TravelPathways.Api/appsettings.json` is set for **localhost with Windows Authentication**:

```text
Server=localhost;Database=TravelPathways;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True
```

- **Default instance** (typical for SQL Server 2022 Developer): use `Server=localhost` as above.
- **Named instance** (e.g. Express): use `Server=localhost\\SQLEXPRESS` (or your instance name).
- If connection fails: ensure **SQL Server service** is running (e.g. "SQL Server (MSSQLSERVER)" in Services), and that TCP/IP or Named Pipes is enabled in SQL Server Configuration Manager.

For SQL auth instead of Windows auth:

```text
Server=localhost;Database=TravelPathways;User Id=sa;Password=YourPassword;TrustServerCertificate=True
```

### Apply migrations

After configuring the connection string, create/update the database from the API project folder:

```powershell
cd backend/src/TravelPathways.Api
dotnet ef database update
```

If the `dotnet-ef` tool is not installed:

```powershell
dotnet tool restore
dotnet ef database update
```

(EF Core tools are configured in `backend/.config/dotnet-tools.json`.)

### Run the API

From repo root:

```powershell
cd backend/src/TravelPathways.Api
dotnet run
```

Swagger will be available at:
- `https://localhost:7001/swagger`

### Login (seeded Super Admin)

The API seeds a Super Admin user on startup (see `appsettings.json`):

- **Email**: `super@travelpathways.local`
- **Password**: `Super@123`

Use `POST /api/auth/login` in Swagger to get a JWT.

### Notes about multi-tenancy

- Tenant users have `tenantId` claim in JWT.
- Super Admin can optionally pass `X-Tenant-Id` header to scope reads.

