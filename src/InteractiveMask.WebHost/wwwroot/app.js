// ---- Live state polling -----------------------------------------------------
const STATE_URL = '/api/state';
const TOGGLE_URL = '/api/toggle';
const POLL_MS = 1000;

const T = window.__t || {};

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
        if (!resp.ok) { setConn(false); return; }
        const state = await resp.json();
        setConn(state.ipcConnected);
        for (const tile of state.tiles) {
            const article = gridHost.querySelector(`[data-slot="${tile.slot}"]`);
            if (article) applyTile(article, tile);
        }
    } catch { setConn(false); }
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

async function postToggle(slot, pin) {
    const resp = await fetch(TOGGLE_URL, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ slot, pin }),
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

    let resp = await postToggle(slot, null);

    while (true) {
        if (resp.result === 'Ok') return;

        if (resp.result === 'PinRequired') {
            const wasMasked = article.classList.contains('masked');
            const pin = await openPinModal(wasMasked ? 'verify' : 'set');
            if (!pin) return;
            resp = await postToggle(slot, pin);
            continue;
        }

        if (resp.result === 'PinWrong') {
            pinError.textContent = T.pinWrong;
            pinDigits = [];
            renderPinDots();
            const pin = await openPinModal('verify');
            if (!pin) return;
            resp = await postToggle(slot, pin);
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

gridHost.addEventListener('click', ev => {
    const article = ev.target.closest('.tile');
    if (article) handleTileClick(article);
});
