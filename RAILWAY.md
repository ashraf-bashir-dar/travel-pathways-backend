# Deploying to Railway

## Fix: "Railpack could not determine how to build"

The API lives in `src/TravelPathways.Api`; Railway was building from the repo root and Railpack could not detect the app.

### What we did

- **`Dockerfile`** at the backend repo root: builds `src/TravelPathways.Api` so Railway has a single Dockerfile at root.
- **`railway.toml`** sets `builder = "DOCKERFILE"` so Railway uses that Dockerfile instead of Railpack.

No need to set Root Directory in the dashboard. Commit and push, then redeploy.

### Env vars (same as other hosts)

Set in Railway **Variables**:

- `ConnectionStrings__DefaultConnection` = Supabase PostgreSQL URI  
- `Jwt__SigningKey` = long random secret  
- `SuperAdmin__Email` / `SuperAdmin__Password`  
- `Cors__AllowedOrigins` = your Cloudflare Pages URL(s)  
- (Optional) `Api__BaseUrl` or `PdfGenerator__BaseUrl` = your Railway API URL  

Then redeploy and use the generated URL as the frontend API base.
