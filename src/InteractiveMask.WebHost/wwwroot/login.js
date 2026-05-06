// Boots the login page: figure out which auth mode the server wants, reveal
// the matching form, and post credentials to /api/login. On success, redirect
// back to the requested page (or "/").

const T = window.__t || {};
const returnUrl = window.__returnUrl || '/';

const titleEl   = document.getElementById('login-title');
const subEl     = document.getElementById('login-sub');
const loadingEl = document.getElementById('loading');
const pinForm   = document.getElementById('pin-form');
const pinInput  = document.getElementById('login-pin');
const pinError  = document.getElementById('pin-error');
const adForm    = document.getElementById('ad-form');
const adUser    = document.getElementById('login-username');
const adPass    = document.getElementById('login-password');
const adError   = document.getElementById('ad-error');

async function init() {
    let mode = 'off';
    try {
        const resp = await fetch('/api/access-mode', { cache: 'no-store' });
        if (resp.ok) {
            const data = await resp.json();
            mode = (data.mode || 'off').toLowerCase();
        }
    } catch { /* fall through to off */ }

    loadingEl.hidden = true;

    if (mode === 'off') {
        // Auth disabled — never expected to land here, but be tolerant.
        window.location.replace(returnUrl);
        return;
    }

    if (mode === 'pin') {
        titleEl.textContent = T.loginTitle;
        subEl.textContent = T.loginSubPin;
        pinForm.hidden = false;
        setTimeout(() => pinInput.focus(), 0);
    } else if (mode === 'ad') {
        titleEl.textContent = T.loginTitle;
        subEl.textContent = T.loginSubAd;
        adForm.hidden = false;
        setTimeout(() => adUser.focus(), 0);
    }
}

async function postLogin(body, errEl) {
    errEl.textContent = '';
    try {
        const resp = await fetch('/api/login', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body),
        });
        if (resp.ok) {
            window.location.replace(returnUrl);
            return;
        }
        if (resp.status === 401) {
            errEl.textContent = T.loginWrong;
            return;
        }
        errEl.textContent = T.loginError;
    } catch {
        errEl.textContent = T.loginError;
    }
}

pinForm.addEventListener('submit', ev => {
    ev.preventDefault();
    const pin = pinInput.value;
    if (!pin) return;
    postLogin({ pin }, pinError);
});

adForm.addEventListener('submit', ev => {
    ev.preventDefault();
    const username = adUser.value.trim();
    const password = adPass.value;
    if (!username || !password) return;
    postLogin({ username, password }, adError);
});

init();
