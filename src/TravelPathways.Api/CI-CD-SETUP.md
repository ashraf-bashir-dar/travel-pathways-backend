# CI/CD Setup: Auto-deploy API to Azure on push

When you push changes to the **backend** (to `main`), GitHub Actions builds the API Docker image, pushes it to your Azure Container Registry (ACR), and restarts the Web App so it runs the new image.

**→ Step-by-step flow (PR → merge → auto deploy, no Docker on your machine):** see **docs/STEP-BY-STEP-PR-TO-DEPLOY.md** in the repo root.

---

## 1. One-time: Add GitHub secrets

In your GitHub repo: **Settings** → **Secrets and variables** → **Actions** → **New repository secret**.

Add these secrets:

| Secret name         | Description |
|---------------------|-------------|
| `ACR_LOGIN_SERVER`  | ACR login server, e.g. `travelpathwaysacr.azurecr.io` (no `https://`) |
| `ACR_USERNAME`      | ACR **Admin** username (from ACR → Access keys) |
| `ACR_PASSWORD`      | ACR **Admin** password (one of the two passwords) |
| `AZURE_CREDENTIALS` | JSON for an Azure service principal (see below) |

---

## 2. Create Azure service principal (for Web App restart)

The workflow needs an Azure identity to run `az webapp restart`. Create a **service principal** and use its credentials as `AZURE_CREDENTIALS`.

### Option A: Azure Portal

1. **Azure Active Directory** → **App registrations** → **New registration**.
   - Name: e.g. `github-actions-travelpathways-api`.
   - Click **Register**.

2. Note:
   - **Application (client) ID**
   - **Directory (tenant) ID**

3. **Certificates & secrets** → **New client secret** → add a secret and copy its **Value** (client secret).

4. **Subscriptions** → your subscription → **Access control (IAM)** → **Add role assignment**:
   - Role: **Contributor** (or **Website Contributor** scoped to the Web App / resource group).
   - Member: select the app you just created (e.g. `github-actions-travelpathways-api`).

5. Build the JSON (replace placeholders, keep the format):

```json
{
  "clientId": "<Application (client) ID>",
  "clientSecret": "<client secret value>",
  "subscriptionId": "<your Azure subscription ID>",
  "tenantId": "<Directory (tenant) ID>"
}
```

6. In GitHub: **New repository secret** → name **`AZURE_CREDENTIALS`**, value = the whole JSON (one line is fine).

### Option B: Azure CLI

```bash
# Replace with your subscription ID and resource group
az ad sp create-for-rbac \
  --name "github-actions-travelpathways-api" \
  --role contributor \
  --scopes /subscriptions/<SUBSCRIPTION_ID>/resourceGroups/<RESOURCE_GROUP> \
  --sdk-auth
```

Use the full JSON output as the value of the **`AZURE_CREDENTIALS`** secret.

---

## 3. Set workflow variables (if different from defaults)

The workflow file uses:

- **`WEBAPP_NAME`**: your Web App name (e.g. `travelpathways-api-docker-f2c8c9ggdxcgcge8`). Default is set in the workflow.
- **`RESOURCE_GROUP`**: resource group that contains the Web App (e.g. `travelpathwaysrg` or `ASP-travelpathwaysrg` if the plan created that group).

To change them, edit **`.github/workflows/api-docker-deploy.yml`** and update the `env` block at the top (`WEBAPP_NAME` and `RESOURCE_GROUP`).

---

## 4. Push and verify

1. Push a change under **`backend/`** to **`main`** (or run the workflow manually: **Actions** → **API Docker - Build and Deploy to Azure** → **Run workflow**).

2. In GitHub **Actions**, open the run and confirm:
   - Build and push to ACR succeed.
   - Azure login and “Restart Web App” succeed.

3. After the run, open your API URL (e.g. `https://<your-webapp>.azurewebsites.net/` or `/swagger`). You should see the new version once the Web App has restarted and pulled the new image.

---

## 5. Optional: run only when API code changes

The workflow is already limited with `paths: ['backend/**']`, so it runs only when files under `backend/` change. No extra config needed.

---

## Troubleshooting

- **“Login to ACR failed”**: Check `ACR_LOGIN_SERVER` (no `https://`), `ACR_USERNAME`, and `ACR_PASSWORD`. Ensure ACR **Admin user** is enabled (ACR → Access keys).
- **“az webapp restart” / “Authorization failed”**: Ensure the service principal has Contributor (or Website Contributor) on the Web App or its resource group, and that `AZURE_CREDENTIALS` is valid JSON with `clientId`, `clientSecret`, `subscriptionId`, `tenantId`.
- **Web App still runs old code**: Confirm the workflow pushed the image and the restart step ran. In Azure, check **Deployment Center** → **Logs** to see that the app pulled the new image after restart.
