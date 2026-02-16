# PDF generation on Azure

Package PDFs are generated using Chromium (PuppeteerSharp). **Azure App Service on Windows** often fails with:

- *"side-by-side configuration is incorrect"*  
- *"The application has failed to start"*

This is due to missing Visual C++ runtimes and sandbox limits on Windows App Service.

## Options that work

### 1. Deploy with the Docker image (recommended)

Use the included **Dockerfile** so the API runs on Linux with Chromium installed:

1. From `backend/src/TravelPathways.Api`:
   ```bash
   docker build -t travelpathways-api .
   ```

2. Deploy the image to:
   - **Azure Web App for Containers** (Linux), or  
   - **Azure Container Apps**

3. No extra config: the image sets `PdfGenerator__ChromeExecutablePath` to the installed Chrome.

### 2. Azure App Service on Linux (without Docker)

If you deploy the app to **Azure App Service (Linux)** (not Windows):

1. In the App Service **Configuration** → **General settings**, set **Stack** to **.NET 8** and **Platform** to **Linux**.

2. Add a **Startup Command** or use an **extension** to install Chromium, or use a custom startup script that runs:
   ```bash
   apt-get update && apt-get install -y chromium-browser
   ```
   (Exact package name may vary; then set **PdfGenerator:ChromeExecutablePath** in app settings to the Chromium binary path.)

### 3. Keep Windows and point to Chrome (advanced)

If you install Chromium (or Chrome) yourself on the Windows App Service (e.g. via a custom step or extension):

- In **Configuration** → **Application settings** add:
  - **PdfGenerator__ChromeExecutablePath** = full path to `chrome.exe` or `chromium.exe`

The API will use that path instead of downloading Chromium.

## Summary

| Hosting                         | PDF works? |
|---------------------------------|------------|
| Azure App Service **Windows**   | No (side-by-side / startup errors) |
| Azure App Service **Linux**     | Yes, if Chromium is installed      |
| Azure **Container** (Dockerfile)| Yes                                |
| Local / on-premises             | Yes (PuppeteerSharp downloads Chrome) |
