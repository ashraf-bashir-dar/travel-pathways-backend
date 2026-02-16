# Step-by-step: Fix PDF on Azure (deploy with Docker)

PDF fails on Azure App Service (Windows) because Chrome cannot start there. Deploy the API as a **Docker container on Linux** so Chrome is available and PDF works.

---

## Prerequisites

- Azure subscription
- Docker installed on your machine (or use Azure Cloud Shell)
- Your API and database already working (e.g. connection string for production)

---

## Option A: Azure Web App for Containers (step-by-step)

### Step 1: Build the Docker image locally

1. Open a terminal (PowerShell or Command Prompt).
2. Go to the API project folder:
   ```bash
   cd c:\Users\Lenovo\source\repos\travel-pathways\backend\src\TravelPathways.Api
   ```
3. Build the image:
   ```bash
   docker build -t travelpathways-api .
   ```
4. Check that it built: you should see `Successfully built` and `Successfully tagged travelpathways-api:latest`.

---

### Step 2: Create an Azure Container Registry (if you don’t have one)

1. In **Azure Portal** go to **Create a resource** → search **Container Registry** → Create.
2. Choose:
   - **Resource group**: same as your app (e.g. your existing one).
   - **Registry name**: e.g. `travelpathwaysacr` (must be globally unique).
   - **SKU**: Basic is enough.
3. Click **Review + create** → **Create**.
4. When it’s created, open the registry → **Access keys**.
5. Turn **Admin user** to **Enabled**.
6. Copy and save:
   - **Login server** (e.g. `travelpathwaysacr.azurecr.io`)
   - **Username**
   - **Password** (one of the two).

---

### Step 3: Push the image to Azure Container Registry

1. Log in to the registry (replace with your login server):
   ```bash
   docker login travelpathwaysacr.azurecr.io
   ```
   Use the **Username** and **Password** from Step 2.

2. Tag the image with your registry (replace `travelpathwaysacr` with your registry name):
   ```bash
   docker tag travelpathways-api:latest travelpathwaysacr.azurecr.io/travelpathways-api:latest
   ```

3. Push the image:
   ```bash
   docker push travelpathwaysacr.azurecr.io/travelpathways-api:latest
   ```

4. In Azure Portal → your Container Registry → **Repositories**: you should see `travelpathways-api` with tag `latest`.

---

### Step 4: Create (or convert to) a Web App that uses the container

**If you already have a Web App (API) and want to switch it to Docker:**

1. Go to your **App Service** in Azure Portal.
2. **Settings** → **Configuration** → **General settings**.
3. Set **Stack settings** to:
   - **Stack**: Docker (or “Docker Container”).
   - **Option**: Single Container (or “Azure Container Registry”).
   - **Registry source**: Azure Container Registry.
   - **Registry**: select the registry you created.
   - **Image**: `travelpathways-api`.
   - **Tag**: `latest`.
4. **Save**.
5. In **Settings** → **Configuration** → **Application settings**, make sure you have:
   - **WEBSITES_PORT** = **8080** (so Azure routes traffic to the container’s port 8080; otherwise the site may hang on load).
   - **Api__BaseUrl** = your API public URL (e.g. `https://your-app.azurewebsites.net`, no trailing slash) so PDF hotel images load on live.
   - Your **ConnectionStrings__DefaultConnection** (or `ConnectionStrings:DefaultConnection` in the UI) for the live database.
   - **Jwt**, **CORS**, and any other settings your app needs.
6. **Save** again and **Restart** the app.

**If you are creating a new Web App:**

1. **Create a resource** → **Web App**.
2. **Basics**:
   - **Subscription** and **Resource group**: as you use for the rest of the project.
   - **Name**: e.g. `travelpathways-api`.
   - **Publish**: Docker Container.
   - **Operating System**: Linux.
   - **Region**: same as your database.
   - **Pricing plan**: e.g. Basic B1 or higher.
3. **Docker** tab:
   - **Options**: Single Container.
   - **Image Source**: Azure Container Registry.
   - **Registry**: your ACR (e.g. travelpathwaysacr).
   - **Image**: travelpathways-api.
   - **Tag**: latest.
4. Create the Web App.
5. Go to **Configuration** → **Application settings** and add:
   - **WEBSITES_PORT** = **8080** (required so Azure sends traffic to the API’s port).
   - **ConnectionStrings__DefaultConnection** = your production SQL connection string.
   - Any other app settings (Jwt, CORS, etc.).
6. **Save** and **Restart**.

---

### Step 5: Allow the Web App to pull from the registry

1. In your **App Service** → **Settings** → **Configuration** → **General settings**, if you use ACR, the portal often sets the registry credentials for you when you chose the image.
2. If the app fails to start with “unauthorized” or “cannot pull image”:
   - Go to your **Container Registry** → **Access keys**.
   - In **App Service** → **Deployment Center** (or **Container settings**), set the registry **Login server**, **Username**, and **Password** (same as Step 2).

---

### Step 6: Test PDF on live

1. Open your live API URL (e.g. `https://travelpathways-api.azurewebsites.net`).
2. Log in and open a package.
3. Click **Download PDF** (or whatever triggers package PDF).
4. A PDF should download and open correctly. If it does, PDF is fixed on Azure.

---

## Option B: Azure Container Apps (alternative)

If you prefer Container Apps instead of Web App:

1. **Create** → **Container App**.
2. **Basics**: same subscription, resource group, name; **Region** as needed.
3. **Container**:
   - **Image type**: Azure Container Registry.
   - **Registry** / **Image** / **Tag**: same as above (`travelpathwaysacr.azurecr.io/travelpathways-api:latest`).
4. **Ingress**: enable ingress, set **Accept traffic from**: Everyone (or restrict later). Set the port your API uses (usually **80** or **8080**).
5. Add **Environment variables** or use **Secrets** for:
   - `ConnectionStrings__DefaultConnection`
   - Jwt and other config.
6. Create the Container App.
7. Use the Container App URL to test the API and PDF the same way as in Step 6 above.

---

## Quick checklist

- [ ] Docker image built from `TravelPathways.Api` folder.
- [ ] Azure Container Registry created and image pushed.
- [ ] Web App (or Container App) set to **Linux** and using the **Docker image** from ACR.
- [ ] **Connection string** and other **app settings** set for production.
- [ ] App **restarted** after config changes.
- [ ] **Download PDF** tested on live and works.

---

## If something goes wrong

- **App won’t start**: Check **Log stream** and **Log files** in the App Service (or Container App logs). Fix connection string, missing settings, or registry credentials.
- **PDF still fails**: Confirm the app is running the **Docker** image (Linux), not the old Windows publish. Check logs for “Chromium” or “Chrome” errors.
- **No registry**: Make sure you pushed the image (Step 3) and that the Web App/Container App is set to use that image and registry.
- **API URL just loads / hangs**: The app listens on port **8080**; Azure defaults to port 80. Add **Application setting** **WEBSITES_PORT** = **8080**, then **Save** and **Restart** the Web App.
- **"Welcome to nginx!" at your API URL**: The Web App is running an nginx container, not your TravelPathways API image. In **App Service** → **Deployment Center** (or **Configuration** → **General settings**), set **Image source** to **Azure Container Registry**, then **Registry** = your ACR, **Image** = `travelpathways-api`, **Tag** = `latest`. Ensure the image was pushed (Step 3). **Save** and **Restart**.
- **504 Gateway Timeout** when opening the API URL: Azure waited for the app to respond and it didn’t in time. See the **504 Gateway Timeout** section below.

---

## 504 Gateway Timeout – what to check

When the Web App URL returns **504 Gateway Timeout**, the gateway did not get a response from your container in time. Go through these in order:

### 1. Set WEBSITES_PORT = 8080 (most common)

The API listens on port **8080**. If Azure is sending traffic to port 80, the request never reaches the app and you get a timeout.

- **App Service** → **Configuration** → **Application settings**.
- Add or edit: **Name** = `WEBSITES_PORT`, **Value** = `8080`.
- **Save** and **Restart** the Web App.

### 2. Confirm the right container image

If the Web App is still using a different image (e.g. nginx/staticsite), it may not respond as expected.

- **App Service** → **Deployment Center** → **Containers** tab.
- **Source** should be **Azure Container Registry**, **Image** = `travelpathways-api`, **Tag** = `latest`.
- If not, change it, **Save**, and **Restart**.

### 3. Check database connection and startup time

The app runs migrations and seeding at startup. If the database is unreachable, slow, or the connection string is wrong, startup can hang and Azure will return 504.

- **Configuration** → **Application settings**: confirm **ConnectionStrings__DefaultConnection** (or `ConnectionStrings:DefaultConnection`) is correct for the live SQL server (server name, database name, user, password). Ensure the DB firewall allows the Web App (Azure services / your app’s outbound IPs).
- **Log stream**: **App Service** → **Monitoring** → **Log stream**. Restart the app and watch for errors like “Database … is not currently available”, “Login failed”, or “Database migration/seed failed”. Fix the connection string or DB firewall and restart.

### 4. Give the app more time to start (optional)

If the database is slow or seeding is heavy, the first request might hit before the app is listening. After fixing port and DB:

- **Restart** the Web App, wait 1–2 minutes, then try the URL again.
- On **Basic** or higher, you can enable **Configuration** → **General settings** → **Always On** = On so the app is less likely to sleep and time out on first request.

### 5. Confirm the app is running

- **Deployment Center** → **Logs** (or **Log stream**): after restart you should see the .NET app start and “Now listening on: http://[::]:8080”. If you see repeated restarts or errors, fix the cause (e.g. wrong image, DB error) then restart again.

For more context (why Windows fails, other options), see **PDF-On-Azure.md** in the same folder.

---

## CI/CD: Deploy API on every push

To build and push the API Docker image to ACR and restart the Web App whenever you push backend code, see **CI-CD-SETUP.md** in this folder. It uses the workflow in the repo root: **.github/workflows/api-docker-deploy.yml**.
