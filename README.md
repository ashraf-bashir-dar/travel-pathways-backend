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

### Production deployment checklist

Before selling or hosting for multiple tenants, set these in environment variables or a secure config (e.g. Azure Key Vault, User Secrets); **do not** rely on defaults in `appsettings.json`.

| Setting | Purpose | Risk if default |
|--------|---------|------------------|
| **Jwt:SigningKey** | JWT token signing. Must be a long, random secret (e.g. 32+ chars). | Token forgery; full account takeover. |
| **Encryption:PasswordKey** | AES key for reversible password storage (admin “view password”). Use a base64-encoded 32-byte value. | If empty, a key is derived from other config; use a dedicated secret in production. |
| **SuperAdmin:Password** | Initial Super Admin password (seed only). Change after first login. | Weak or default password gives platform-wide access. |
| **ConnectionStrings:DefaultConnection** | Database connection. | Use strong credentials and restrict network access. |

- **CORS**: Set `Cors:AllowedOrigins` to your frontend origin(s) only (no `*` in production if using credentials).
- **IncludeExceptionDetailsInResponse**: Keep `false` in production so stack traces are not sent to clients.
- Run `dotnet ef database update` against the production database and ensure migrations are applied.

