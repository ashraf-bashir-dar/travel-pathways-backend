# Deploying to Railway

## Fix: "Railpack could not determine how to build"

The API project (and its **Dockerfile**) lives in `src/TravelPathways.Api`, but Railway was building from the repo root and could not detect how to build.

### What we did

- Added **`railway.toml`** at the backend repo root with:
  ```toml
  [build]
  rootDirectory = "src/TravelPathways.Api"
  ```
  So Railway builds from that folder, finds the **Dockerfile**, and uses it.

### If it still fails

1. In **Railway** → your service → **Settings** → **Build**:
   - Set **Root Directory** to: `src/TravelPathways.Api`
2. Redeploy.

### Env vars (same as other hosts)

Set in Railway **Variables**:

- `ConnectionStrings__DefaultConnection` = Supabase PostgreSQL URI  
- `Jwt__SigningKey` = long random secret  
- `SuperAdmin__Email` / `SuperAdmin__Password`  
- `Cors__AllowedOrigins` = your Cloudflare Pages URL(s)  
- (Optional) `Api__BaseUrl` or `PdfGenerator__BaseUrl` = your Railway API URL  

Then redeploy and use the generated URL as the frontend API base.
