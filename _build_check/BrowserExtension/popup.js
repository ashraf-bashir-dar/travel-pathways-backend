const DEFAULT_API_URL = 'https://localhost:44396';

function normalizeApiUrl(value) {
  const raw = (value || DEFAULT_API_URL).trim().replace(/\/$/, '');
  try {
    const u = new URL(raw);
    return `${u.protocol}//${u.host}`;
  } catch {
    return raw;
  }
}

const loginView = document.getElementById('login-view');
const statusView = document.getElementById('status-view');
const loginBtn = document.getElementById('login-btn');
const logoutBtn = document.getElementById('logout-btn');
const syncBtn = document.getElementById('sync-btn');
const loginError = document.getElementById('login-error');
const userNameEl = document.getElementById('user-name');
const statusText = document.getElementById('status-text');
const statusDot = document.getElementById('status-dot');
const lastSyncEl = document.getElementById('last-sync');

document.addEventListener('DOMContentLoaded', async () => {
  const stored = await chrome.storage.local.get([
    'token',
    'userName',
    'apiUrl',
    'lastSyncAt',
    'trackingDisabled'
  ]);

  document.getElementById('api-url').value = stored.apiUrl || DEFAULT_API_URL;

  if (stored.token && stored.userName) {
    showStatus(stored);
  }
});

loginBtn.addEventListener('click', async () => {
  loginError.hidden = true;
  loginBtn.disabled = true;

  const apiUrl = normalizeApiUrl(document.getElementById('api-url').value);
  const email = document.getElementById('email').value.trim();
  const password = document.getElementById('password').value;
  const tenantCode = document.getElementById('tenant-code').value.trim();

  if (!email || !password) {
    showLoginError('Email and password are required.');
    loginBtn.disabled = false;
    return;
  }

  try {
    const body = { email, password };
    if (tenantCode) body.tenantCode = tenantCode;

    const res = await fetch(`${apiUrl}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body)
    });

    const data = await res.json().catch(() => null);
    if (!res.ok) {
      showLoginError(data?.message || `Login failed (${res.status})`);
      loginBtn.disabled = false;
      return;
    }

    const token = data.token;
    const user = data.user;
    const tenant = data.tenant;

    if (!token || !tenant?.id) {
      showLoginError('Invalid login response. Super Admin accounts cannot use this extension.');
      loginBtn.disabled = false;
      return;
    }

    if (user?.role === 'SuperAdmin') {
      showLoginError('Super Admin accounts are not tracked. Sign in as a tenant user.');
      loginBtn.disabled = false;
      return;
    }

    const name = `${user.firstName || ''} ${user.lastName || ''}`.trim() || user.email;

    await chrome.storage.local.set({
      token,
      tenantId: tenant.id,
      userName: name,
      userEmail: user.email,
      apiUrl,
      trackingDisabled: false,
      lastSyncAt: null
    });

    showStatus({ userName: name, trackingDisabled: false, lastSyncAt: null });
    chrome.runtime.sendMessage({ type: 'flush' });
  } catch (err) {
      showLoginError('Could not reach API. Use the base URL only (e.g. https://localhost:44396 — not /swagger). Ensure Visual Studio / IIS Express is running.');
  }

  loginBtn.disabled = false;
});

logoutBtn.addEventListener('click', async () => {
  chrome.runtime.sendMessage({ type: 'logout' });
  await chrome.storage.local.remove([
    'token',
    'tenantId',
    'userName',
    'userEmail',
    'trackingDisabled',
    'lastSyncAt',
    'lastSyncCount'
  ]);
  statusView.hidden = true;
  loginView.hidden = false;
  document.getElementById('password').value = '';
});

syncBtn.addEventListener('click', () => {
  chrome.runtime.sendMessage({ type: 'flush' }, () => {
    chrome.storage.local.get(['lastSyncAt', 'lastSyncCount'], data => {
      updateLastSync(data.lastSyncAt, data.lastSyncCount);
    });
  });
});

function showLoginError(msg) {
  loginError.textContent = msg;
  loginError.hidden = false;
}

function showStatus(stored) {
  loginView.hidden = true;
  statusView.hidden = false;
  userNameEl.textContent = stored.userName || 'Signed in';

  if (stored.trackingDisabled) {
    statusText.textContent = 'Tracking disabled by admin';
    statusDot.classList.add('off');
  } else {
    statusText.textContent = 'Tracking active';
    statusDot.classList.remove('off');
  }

  updateLastSync(stored.lastSyncAt, stored.lastSyncCount);
}

function updateLastSync(iso, count) {
  if (!iso) {
    lastSyncEl.textContent = 'Last sync: not yet';
    return;
  }
  const d = new Date(iso);
  const label = d.toLocaleString();
  lastSyncEl.textContent = count != null ? `Last sync: ${label} (${count} visits)` : `Last sync: ${label}`;
}
