# Travel Pathways Browser Activity Extension

Chrome/Edge extension that reports **websites visited** (Facebook, Google, etc.) to the Travel Pathways API for **tenant admin** review under **Employee → Activity**.

## Install via Travel Pathways app (recommended)

1. Admin enables **Extensions** module for the tenant and assigns it to the employee (Users → edit user → Extensions).
2. Employee opens **Extensions** in the sidebar.
3. Click **Install** on **Travel Pathways Activity** → download ZIP → follow steps.
4. Admin removes **Extensions** module from the user when done.

1. Open Chrome or Edge → **Extensions** → enable **Developer mode**
2. Click **Load unpacked**
3. Select this folder: `browser-extension/`
4. Click the extension icon → sign in with your **tenant user** email and password
5. Set **API URL** to your backend (default `http://localhost:5258`)

## Requirements

- API running and reachable from the browser
- User must have **Activity tracking** enabled (admin toggle in Employee → Activity)
- **Super Admin** accounts are not supported
- Employees should be informed per your company policy

## What is tracked

- HTTP/HTTPS pages in normal browser tabs
- Page title and URL
- Approximate time on page when switching tabs

## What is not tracked

- `chrome://`, `edge://`, extension pages
- Localhost / Travel Pathways app URLs (to avoid duplicate in-app logs)

## Admin view

**Employee → Activity → Page & link activity** — filter type **Browser** to see extension-reported sites.

## Production API URL

Employees should set the production API URL in the extension popup (e.g. `https://api.yourdomain.com`).
