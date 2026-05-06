// ---- Live state polling -----------------------------------------------------
const STATE_URL = '/api/state';
const TOGGLE_URL = '/api/toggle';
const AUTH_MODE_URL = '/api/auth-mode';
const POLL_MS = 1000;

const T = window.__t || {};
let authMode = { mode: 'pin', domain: '' };

async function refreshAuthMode() {
    try {
        const resp = await fetch(AUTH_MODE_URL, { cache: 'no-store' });
        if (resp.ok) authMode = await resp.json();
    } catch { /* keep last good */ }
}
refreshAuthMode();
setInterval(refreshAuthMode, 5000);

const gridHost = document.getElementById('grid-host');
const connDot  = document.getElementById('conn-dot');
const connText = document.getElementById('conn-text');

if (gridHost) {
    gridHost.style.setProperty('--rows', gridHost.dataset.rows || 1);
    gridHost.style.setProperty('--cols', gridHost.dataset.cols || 1);
}

const STATUS_CLASSES = ['live', 'pending', 'disconnected', 'videoloss', 'error', 'empty'];

function applyTile(article, tile) {
    article.classList.toggle('has-camera', tile.cameraIndex >= 0);
    article.classList.toggle('masked', tile.isMasked);
    article.classList.toggle('warning', tile.isTimerWarning);

    STATUS_CLASSES.forEach(c => article.classList.remove(c));
    article.classList.add(tile.status.toLowerCase());

    article.querySelector('.label').textContent = tile.label || '';
    article.querySelector('.status-text').textContent = statusText(tile.status);
    article.querySelector('.mask-text').textContent = tile.isMasked ? `${T.privacyActive} — ${tile.label}` : '';
    article.querySelector('.mask-countdown').textContent = tile.isMasked ? localiseCountdown(tile.countdownText) : '';
}

function localiseCountdown(serverText) {
    // Server uses Display's NL/EN string; if WebHost language differs, swap the prefix.
    if (!serverText) return '';
    return serverText.replace(/^auto-uit/i, T.autoOff).replace(/^auto-off/i, T.autoOff);
}

function statusText(status) {
    switch (status) {
        case 'Live':         return '';
        case 'Pending':      return T.connecting;
        case 'Disconnected': return T.connectionLost;
        case 'VideoLoss':    return T.noVideoSignal;
        case 'Error':        return T.error;
        default:             return '';
    }
}

function setConn(connected) {
    connDot.className = 'dot ' + (connected ? 'dot-green' : 'dot-red');
    connText.textContent = connected ? T.connectedToDisplay : T.waitingForDisplay;
}

async function poll() {
    try {
        const resp = await fetch(STATE_URL, { cache: 'no-store' });
        if (resp.status === 401) {
            // Session expired or web auth was just enabled — bounce to login.
            window.location.replace('/login?returnUrl=' + encodeURIComponent(window.location.pathname + window.location.search));
            return;
        }
        if (!resp.ok) { setConn(false); return; }
        const state = await resp.json();
        setConn(state.ipcConnected);
        for (const tile of state.tiles) {
            const article = gridHost.querySelector(`[data-slot="${tile.slot}"]`);
            if (article) applyTile(article, tile);
        }
    } catch { setConn(false); }
}

// ---- Logout button (only visible when access auth is enabled) --------------

const logoutBtn = document.getElementById('logout-btn');
async function refreshLogoutVisibility() {
    try {
        const resp = await fetch('/api/access-mode', { cache: 'no-store' });
        if (!resp.ok) return;
        const data = await resp.json();
        if (logoutBtn) logoutBtn.hidden = (data.mode || 'off').toLowerCase() === 'off';
    } catch { /* keep last */ }
}
refreshLogoutVisibility();

if (logoutBtn) {
    logoutBtn.addEventListener('click', async () => {
        try {
            await fetch('/api/logout', { method: 'POST' });
        } catch { /* ignore — we'll navigate anyway */ }
        window.location.replace('/login');
    });
}

poll();
setInterval(poll, POLL_MS);

// ---- Toggle flow with PIN modal --------------------------------------------

const modal = document.getElementById('pin-modal');
const pinTitle = document.getElementById('pin-title');
const pinSub = document.getElementById('pin-sub');
const pinError = document.getElementById('pin-error');
const pinDots = modal.querySelectorAll('.pin-dot');
const pinCancel = document.getElementById('pin-cancel');

let pinDigits = [];
let pinPromise = null;
let pinMode = 'verify';

function openPinModal(mode) {
    pinMode = mode;
    pinDigits = [];
    pinError.textContent = '';
    renderPinDots();
    if (mode === 'set') {
        pinTitle.textContent = T.pinSetTitle;
        pinSub.textContent = T.pinSetSubtitle;
        pinCancel.style.visibility = 'hidden';
    } else {
        pinTitle.textContent = T.pinVerifyTitle;
        pinSub.textContent = T.pinVerifySubtitle;
        pinCancel.style.visibility = 'visible';
    }
    modal.hidden = false;
    return new Promise(resolve => { pinPromise = resolve; });
}

function closePinModal(value) {
    modal.hidden = true;
    const resolve = pinPromise;
    pinPromise = null;
    if (resolve) resolve(value);
}

function renderPinDots() {
    pinDots.forEach((d, i) => d.classList.toggle('filled', i < pinDigits.length));
}

modal.querySelectorAll('.pin-key').forEach(btn => {
    btn.addEventListener('click', () => {
        if (btn.dataset.action === 'back') {
            if (pinDigits.length > 0) { pinDigits.pop(); renderPinDots(); pinError.textContent = ''; }
            return;
        }
        const d = btn.dataset.digit;
        if (!d || pinDigits.length >= 4) return;
        pinDigits.push(d);
        renderPinDots();
        if (pinDigits.length === 4) closePinModal(pinDigits.join(''));
    });
});

pinCancel.addEventListener('click', () => closePinModal(null));

document.addEventListener('keydown', ev => {
    if (modal.hidden) return;
    if (ev.key >= '0' && ev.key <= '9') {
        if (pinDigits.length < 4) {
            pinDigits.push(ev.key);
            renderPinDots();
            if (pinDigits.length === 4) closePinModal(pinDigits.join(''));
        }
    } else if (ev.key === 'Backspace') {
        if (pinDigits.length > 0) { pinDigits.pop(); renderPinDots(); pinError.textContent = ''; }
    } else if (ev.key === 'Escape' && pinMode === 'verify') {
        closePinModal(null);
    }
});

async function postToggle(slot, body) {
    const resp = await fetch(TOGGLE_URL, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ slot, ...body }),
    });
    if (!resp.ok) {
        let detail = '';
        try { detail = (await resp.json())?.detail || ''; } catch { /* ignore */ }
        return { result: 'IpcError', http: resp.status, detail };
    }
    return await resp.json();
}

async function handleTileClick(article) {
    const slot = parseInt(article.dataset.slot, 10);
    if (!article.classList.contains('has-camera')) return;

    let resp = await postToggle(slot, {});

    while (true) {
        if (resp.result === 'Ok') return;

        if (resp.result === 'PinRequired') {
            const wasMasked = article.classList.contains('masked');
            const pin = await openPinModal(wasMasked ? 'verify' : 'set');
            if (!pin) return;
            resp = await postToggle(slot, { pin });
            continue;
        }

        if (resp.result === 'PinWrong') {
            pinError.textContent = T.pinWrong;
            pinDigits = [];
            renderPinDots();
            const pin = await openPinModal('verify');
            if (!pin) return;
            resp = await postToggle(slot, { pin });
            continue;
        }

        if (resp.result === 'CredentialsRequired') {
            const creds = await openAdModal();
            if (!creds) return;
            resp = await postToggle(slot, creds);
            continue;
        }

        if (resp.result === 'CredentialsWrong') {
            adError.textContent = T.adWrongCredentials || T.error;
            const creds = await openAdModal(true);
            if (!creds) return;
            resp = await postToggle(slot, creds);
            continue;
        }

        if (resp.result === 'LockedOut') {
            pinError.textContent = (T.pinLockedOutFormat || '').replace('{0}', resp.lockoutSecondsRemaining ?? 30);
            await openPinModal('verify');
            return;
        }

        if (resp.result === 'IpcError') {
            // Surface the WebHost-side reason so the user knows it's not a PIN issue.
            const msg = resp.http === 503 ? (T.ipcUnavailable || resp.detail)
                      : resp.http === 504 ? (T.ipcTimeout || resp.detail)
                      : (T.error || 'Error');
            alert(msg);
            return;
        }

        return;
    }
}

// ---- AD credentials modal --------------------------------------------------

const adModal = document.getElementById('ad-modal');
const adForm = document.getElementById('ad-form');
const adUsername = document.getElementById('ad-username');
const adPassword = document.getElementById('ad-password');
const adError = document.getElementById('ad-error');
const adCancel = document.getElementById('ad-cancel');
let adPromise = null;

function openAdModal(keepValues) {
    if (!keepValues) {
        adUsername.value = '';
        adPassword.value = '';
        adError.textContent = '';
    } else {
        // Wrong-credentials retry: leave username, clear password, focus password.
        adPassword.value = '';
    }
    adModal.hidden = false;
    setTimeout(() => (keepValues ? adPassword : adUsername).focus(), 0);
    return new Promise(resolve => { adPromise = resolve; });
}

function closeAdModal(value) {
    adModal.hidden = true;
    const resolve = adPromise;
    adPromise = null;
    // Always wipe the password field so it doesn't linger in the DOM.
    adPassword.value = '';
    if (resolve) resolve(value);
}

adForm.addEventListener('submit', ev => {
    ev.preventDefault();
    const username = adUsername.value.trim();
    const password = adPassword.value;
    if (!username || !password) return;
    closeAdModal({ username, password });
});

adCancel.addEventListener('click', () => closeAdModal(null));

document.addEventListener('keydown', ev => {
    if (adModal.hidden) return;
    if (ev.key === 'Escape') closeAdModal(null);
});

gridHost.addEventListener('click', ev => {
    const refreshBtn = ev.target.closest('.tile-refresh');
    if (refreshBtn) {
        const article = refreshBtn.closest('.tile');
        if (article) refreshSnapshot(article);
        ev.stopPropagation();
        return;
    }
    const article = ev.target.closest('.tile');
    if (article) handleTileClick(article);
});

// ---- Manual snapshot refresh ------------------------------------------------

async function refreshSnapshot(article) {
    if (!article.classList.contains('has-camera')) return;
    const slot = parseInt(article.dataset.slot, 10);
    const img = article.querySelector('.tile-thumb');
    if (!img) return;

    article.classList.add('snapshot-loading');
    try {
        const resp = await fetch(`/api/snapshot/${slot}?t=${Date.now()}`, { cache: 'no-store' });
        if (resp.status === 403) {
            // Privacy is on — no thumbnail; clear any stale one.
            img.removeAttribute('src');
            article.classList.add('snapshot-blocked');
            return;
        }
        if (!resp.ok) {
            img.removeAttribute('src');
            return;
        }
        const blob = await resp.blob();
        const previous = img.dataset.objectUrl;
        if (previous) URL.revokeObjectURL(previous);
        const url = URL.createObjectURL(blob);
        img.dataset.objectUrl = url;
        img.src = url;
        article.classList.remove('snapshot-blocked');
    } catch {
        img.removeAttribute('src');
    } finally {
        article.classList.remove('snapshot-loading');
    }
}
