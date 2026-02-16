# Faster PDF: Put uploads on persistent storage (Azure)

Hotel images in the PDF are inlined (read from disk and embedded) when the API can read the files. That makes PDF generation **much faster** because Chromium doesn’t fetch each image over the network.

On Azure, the container’s filesystem is **ephemeral**: anything under `wwwroot/uploads` is lost on restart, and new uploads only live until the next restart. To have **persistent** uploads that the API can read (and thus faster PDFs), put uploads on **Azure Files** and mount that share into the Web App.

---

## 1. Create Azure Storage and File Share

1. In **Azure Portal** → **Create a resource** → **Storage account**.
2. **Basics**: subscription, resource group (e.g. same as Web App), **Storage account name** (e.g. `travelpathwaysuploads`), **Region** (e.g. Central India), **Performance** Standard, **Redundancy** LRS (or as you prefer).
3. Create the storage account.
4. Open the storage account → **Data storage** → **File shares** → **+ File share**.
5. **Name**: e.g. `uploads`. **Quota**: e.g. 10 GiB. Create.

---

## 2. Mount the file share in the Web App

1. Open your **Web App** (e.g. `travelpathways-api-v2`) → **Settings** → **Path mappings** (or **Configuration** → **Path mappings**).
2. **+ New Azure Storage Mount** (or **+ New mount**).
3. **Name**: e.g. `uploads`.
4. **Configuration options**: **Basic**.
5. **Storage accounts**: select the storage account you created.
6. **Storage type**: **Azure Files**.
7. **Storage container**: select the **File share** you created (e.g. `uploads`).
8. **Mount path**: `/home/uploads` (this is the path inside the container).
9. **Save**.

The container will see the file share at **`/home/uploads`**. New uploads and existing content (if you copy it there) will persist across restarts.

---

## 3. Configure the API to use the mount

1. **Web App** → **Configuration** → **Application settings**.
2. **+ New application setting**:
   - **Name**: `Uploads__Path`
   - **Value**: `/home/uploads`
3. **Save** and **Restart** the Web App.

The API will then:
- **Save** all new uploads (hotel images, etc.) under `/home/uploads` (the mounted share).
- **Serve** `/uploads/...` from that path (static files).
- **Read** images from that path when generating PDFs and **inline** them, so PDF download is faster.

---

## 4. Existing uploads (optional)

If you already have uploads in the old location (e.g. from before the mount), they are not automatically in the new share. You can:

- **Option A**: Re-upload hotel images through the app; they will go to the new path.
- **Option B**: Copy existing files into the Azure File Share (e.g. via Azure Portal **Storage browser** → open the file share and upload, keeping the same structure: `tenants/<tenantId>/hotels/<hotelId>/images/...`).

---

## Summary

| Step | Action |
|------|--------|
| 1 | Create a Storage account and a **File share** (e.g. `uploads`). |
| 2 | In the Web App, add a **Path mapping**: Azure Files share → **Mount path** `/home/uploads`. |
| 3 | Add app setting **Uploads__Path** = `/home/uploads`, then **Save** and **Restart**. |
| 4 | New uploads go to the share; PDF generation reads from disk and inlines images → **faster PDF**. |

After this, hotel images are stored on persistent storage and the API reads them from `/home/uploads` for PDFs, so download time should improve.
