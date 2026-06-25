const DEFAULT_API_URL = 'https://localhost:44396';
const FLUSH_ALARM = 'tp-flush-visits';
const FLUSH_INTERVAL_MINUTES = 1;
const DEDUPE_MS = 5000;

/** @type {{ url: string, pageTitle?: string, durationSeconds?: number, visitedAtUtc: string }[]} */
let pendingVisits = [];
/** @type {Map<number, { url: string, title: string, startedAt: number }>} */
const activeTabs = new Map();
/** @type {{ url: string, at: number } | null} */
let lastRecorded = null;

chrome.runtime.onInstalled.addListener(() => {
  chrome.storage.local.get(['apiUrl'], data => {
    if (!data.apiUrl) {
      chrome.storage.local.set({ apiUrl: DEFAULT_API_URL });
    }
  });
  chrome.alarms.create(FLUSH_ALARM, { periodInMinutes: FLUSH_INTERVAL_MINUTES });
});

chrome.alarms.onAlarm.addListener(alarm => {
  if (alarm.name === FLUSH_ALARM) {
    flushPendingVisits();
  }
});

chrome.tabs.onActivated.addListener(async activeInfo => {
  await recordTabLeave(activeInfo.previousTabId);
  await recordTabEnter(activeInfo.tabId);
});

chrome.tabs.onUpdated.addListener(async (tabId, changeInfo, tab) => {
  if (changeInfo.status !== 'complete' || !tab.url) return;
  if (!isTrackableUrl(tab.url)) return;

  const prev = activeTabs.get(tabId);
  if (prev && prev.url !== tab.url) {
    enqueueVisit(prev.url, prev.title, secondsSince(prev.startedAt));
  }

  activeTabs.set(tabId, {
    url: tab.url,
    title: tab.title || '',
    startedAt: Date.now()
  });

  enqueueVisit(tab.url, tab.title || '', undefined);
});

chrome.tabs.onRemoved.addListener(tabId => {
  const prev = activeTabs.get(tabId);
  if (prev) {
    enqueueVisit(prev.url, prev.title, secondsSince(prev.startedAt));
    activeTabs.delete(tabId);
  }
});

async function recordTabLeave(previousTabId) {
  if (previousTabId == null || previousTabId < 0) return;
  const prev = activeTabs.get(previousTabId);
  if (!prev) return;
  enqueueVisit(prev.url, prev.title, secondsSince(prev.startedAt));
  activeTabs.set(previousTabId, { ...prev, startedAt: Date.now() });
}

async function recordTabEnter(tabId) {
  try {
    const tab = await chrome.tabs.get(tabId);
    if (!tab.url || !isTrackableUrl(tab.url)) return;
    activeTabs.set(tabId, {
      url: tab.url,
      title: tab.title || '',
      startedAt: Date.now()
    });
  } catch {
    /* tab may not exist */
  }
}

function secondsSince(startedAt) {
  return Math.max(1, Math.floor((Date.now() - startedAt) / 1000));
}

function isTrackableUrl(url) {
  try {
    const u = new URL(url);
    if (u.protocol !== 'http:' && u.protocol !== 'https:') return false;
    const host = u.hostname.toLowerCase();
    if (host === 'localhost' || host === '127.0.0.1') return false;
    if (host.endsWith('.travelpathways.local')) return false;
    return true;
  } catch {
    return false;
  }
}

function shouldDedupe(url) {
  const now = Date.now();
  if (lastRecorded && lastRecorded.url === url && now - lastRecorded.at < DEDUPE_MS) {
    return true;
  }
  lastRecorded = { url, at: now };
  return false;
}

function enqueueVisit(url, pageTitle, durationSeconds) {
  if (!isTrackableUrl(url)) return;
  if (shouldDedupe(url) && durationSeconds == null) return;

  pendingVisits.push({
    url,
    pageTitle: pageTitle || undefined,
    durationSeconds: durationSeconds ?? undefined,
    visitedAtUtc: new Date().toISOString()
  });

  if (pendingVisits.length >= 20) {
    flushPendingVisits();
  }
}

async function flushPendingVisits() {
  if (pendingVisits.length === 0) return;

  const batch = pendingVisits.splice(0, 50);
  const session = await getSession();
  if (!session?.token || !session?.tenantId) return;

  const apiUrl = (session.apiUrl || DEFAULT_API_URL).replace(/\/$/, '');
  try {
    const res = await fetch(`${apiUrl}/api/user-activity/browser-visits`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${session.token}`,
        'X-Tenant-Id': session.tenantId
      },
      body: JSON.stringify({ visits: batch })
    });

    const data = await res.json().catch(() => ({}));
    if (!res.ok) {
      console.warn('[Travel Pathways] browser-visits failed', res.status, data);
      pendingVisits.unshift(...batch);
      return;
    }

    if (data?.data?.trackingEnabled === false) {
      await chrome.storage.local.set({ trackingDisabled: true });
    }

    await chrome.storage.local.set({ lastSyncAt: new Date().toISOString(), lastSyncCount: batch.length });
  } catch (err) {
    console.warn('[Travel Pathways] flush error', err);
    pendingVisits.unshift(...batch);
  }
}

async function getSession() {
  return chrome.storage.local.get(['token', 'tenantId', 'apiUrl', 'trackingDisabled']);
}

/** Called from popup after login */
chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
  if (message?.type === 'flush') {
    flushPendingVisits().then(() => sendResponse({ ok: true }));
    return true;
  }
  if (message?.type === 'logout') {
    pendingVisits = [];
    activeTabs.clear();
    sendResponse({ ok: true });
    return false;
  }
  return false;
});
