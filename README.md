## TravelPathways Backend (ASP.NET Core + PostgreSQL)

This folder contains the backend API for the Angular app.

### Prerequisites

- .NET SDK 8.x
- **PostgreSQL** (local, **Amazon RDS for PostgreSQL**, or another host)

### Configure DB connection

Set `ConnectionStrings:DefaultConnection` in `src/TravelPathways.Api/appsettings.json` (development) or environment variables / **AWS Secrets Manager** (production). Example (local):

```text
Host=localhost;Port=5432;Database=TravelPathways;Username=postgres;Password=YourPassword
```

### Apply migrations

From the API project folder:

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

```powershell
cd backend/src/TravelPathways.Api
dotnet run
```

Swagger: `https://localhost:7001/swagger` (or the URL shown in the console).

### Login (seeded Super Admin)

See `appsettings.json`:

- **Email**: `super@travelpathways.local`
- **Password**: `Super@123`

Use `POST /api/auth/login` in Swagger to get a JWT.

### Multi-tenancy

- Tenant users have `tenantId` claim in JWT.
- Super Admin can pass `X-Tenant-Id` header to scope reads.

### Hosting on AWS

See **`../docs/AWS-HOSTING.md`** for S3 + CloudFront (UI), EC2/ECS/RDS (API + DB), CORS, TLS, and optional CI.

### Production deployment checklist

Before selling or hosting for multiple tenants, set these in environment variables or a secure config (e.g. **AWS Secrets Manager**, User Secrets); **do not** rely on defaults in `appsettings.json`.

| Setting | Purpose | Risk if default |
|--------|---------|------------------|
| **Jwt:SigningKey** | JWT token signing. Must be a long, random secret (e.g. 32+ chars). | Token forgery; full account takeover. |
| **Encryption:PasswordKey** | AES key for reversible password storage (admin “view password”). Use a base64-encoded 32-byte value. | If empty, a key is derived from other config; use a dedicated secret in production. |
| **SuperAdmin:Password** | Initial Super Admin password (seed only). Change after first login. | Weak or default password gives platform-wide access. |
| **ConnectionStrings:DefaultConnection** | Database connection. | Use strong credentials and restrict network access. |

- **CORS**: Set `Cors:AllowedOrigins` to your frontend origin(s) only (no `*` in production if using credentials).
- **IncludeExceptionDetailsInResponse**: Keep `false` in production so stack traces are not sent to clients.
- Run `dotnet ef database update` against the production database and ensure migrations are applied.
