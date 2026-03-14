# Deploying the backend to Render

Use this when the **backend** is deployed as its own repo (or as the root of what you connect to Render).

---

## 1. Create a Web Service

1. Go to [render.com](https://render.com) → **New** → **Web Service**.
2. Connect your **GitHub** account and select the **backend** repository (and the branch you want, e.g. `main` or your Postgres branch).

---

## 2. Build & run settings

| Setting | Value |
|--------|--------|
| **Name** | e.g. `travelpathways-api` |
| **Region** | Choose one close to your users |
| **Root Directory** | `src/TravelPathways.Api` |
| **Runtime** | **Docker** |
| **Instance Type** | Free or paid (Free tier spins down when idle) |

Render will use the **Dockerfile** inside `src/TravelPathways.Api` to build and run the API. No extra build command is needed.

---

## 3. Environment variables

In **Environment** (or **Environment Variables**), add:

**Required:**

| Key | Value |
|-----|--------|
| `ConnectionStrings__DefaultConnection` | Your Supabase PostgreSQL connection URI |
| `Jwt__SigningKey` | Long random secret (32+ characters) |
| `SuperAdmin__Email` | e.g. `super@travelpathways.local` |
| `SuperAdmin__Password` | Strong password |
| `Cors__AllowedOrigins` | Your frontend URL(s), comma-separated, e.g. `https://your-app.pages.dev` |

**Optional:**

| Key | Value |
|-----|--------|
| `Encryption__PasswordKey` | Base64 key for “view password” (if you use it) |
| `Api__BaseUrl` or `PdfGenerator__BaseUrl` | Your Render service URL (e.g. `https://travelpathways-api.onrender.com`) for PDF links |
| `Uploads__Path` | Path to a persistent disk mount (if you add a disk on a paid plan) |

---

## 4. Deploy

Click **Create Web Service**. Render will build the Docker image and start the app.

- Your API URL will be like: **`https://<service-name>.onrender.com`**
- Use this URL in your frontend (e.g. `environment.prod.ts` or Cloudflare `API_URL`) and in `Cors__AllowedOrigins`.

---

## 5. If your repo is the full monorepo (frontend + backend)

If you connected the **full** travel-pathways repo (with `backend/` and `travel-pathways-ui/`):

- Set **Root Directory** to: **`backend/src/TravelPathways.Api`**
- Leave **Runtime** as **Docker**.

Everything else (env vars, URL, CORS) is the same.
