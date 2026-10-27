// Inactivity warning: session-timeout.js. Independent of the underlying auth cookie/token refresh -
// this only drives the client-side "Extend session?" warning and the active expiry it enforces.
(function () {
    'use strict';

    var banner = document.getElementById('session-timeout-warning');
    if (!banner) {
        return;
    }

    var warningAfterMs = Number.parseInt(banner.dataset.warningAfterMs, 10);
    var expireAfterMs = Number.parseInt(banner.dataset.expireAfterMs, 10);
    var extendUrl = toSafeRelativeUrl(banner.dataset.extendUrl);
    var expiredUrl = toSafeRelativeUrl(banner.dataset.expiredUrl);
    var countdownEl = document.getElementById('session-timeout-countdown');
    var announcementEl = document.getElementById('session-timeout-countdown-announcement');
    var extendButton = document.getElementById('session-timeout-extend');
    var antiforgeryToken = banner.dataset.antiforgeryToken;

    // Only ever a same-origin relative path (never scheme-relative/absolute) is accepted here -
    // these attributes are server-generated and trustworthy today, but navigating/fetching based on
    // unvalidated DOM-read text is a DOM-based open-redirect risk if that ever changes (CodeQL
    // js/xss-through-dom). Returns null for anything else so callers can fail safe.
    function toSafeRelativeUrl(value) {
        return typeof value === 'string' && /^\/(?!\/)/.test(value) ? value : null;
    }

    if (!extendUrl || !expiredUrl) {
        return;
    }

    // Seconds-remaining points at which the hidden live region re-announces the countdown - not
    // every second, which would be unusably disruptive for screen reader users.
    var ONE_MINUTE_REMAINING_SECONDS = 60;
    var THIRTY_SECONDS_REMAINING = 30;
    var TEN_SECONDS_REMAINING = 10;
    var ANNOUNCE_MILESTONES = new Set([ONE_MINUTE_REMAINING_SECONDS, THIRTY_SECONDS_REMAINING, TEN_SECONDS_REMAINING]);

    var idleTimer = null;
    var countdownInterval = null;
    var warningShown = false;
    var announcedMilestones = {};

    function expireNow() {
        window.location.href = expiredUrl;
    }

    function scheduleWarning() {
        clearTimeout(idleTimer);
        idleTimer = setTimeout(showWarning, warningAfterMs);
    }

    function showWarning() {
        warningShown = true;
        announcedMilestones = {};
        banner.hidden = false;
        // Moves keyboard/screen-reader focus to the banner (role="alert" announces it either way,
        // but the banner sits after all page content in the DOM, so a keyboard user tabbing through
        // the page would otherwise have no indication it has appeared).
        banner.focus();

        var expiresAt = Date.now() + expireAfterMs;
        updateCountdown(expiresAt);
        countdownInterval = setInterval(function () {
            updateCountdown(expiresAt);
        }, 1000);
    }

    function updateCountdown(expiresAt) {
        var remainingSeconds = Math.round((expiresAt - Date.now()) / 1000);
        if (remainingSeconds <= 0) {
            clearInterval(countdownInterval);
            expireNow();
            return;
        }

        countdownEl.textContent = String(remainingSeconds);
        announceMilestone(remainingSeconds);
    }

    function announceMilestone(remainingSeconds) {
        if (!ANNOUNCE_MILESTONES.has(remainingSeconds) || announcedMilestones[remainingSeconds]) {
            return;
        }

        announcedMilestones[remainingSeconds] = true;
        announcementEl.textContent = `You will be signed out in ${remainingSeconds} seconds unless you choose to continue.`;
    }

    function hideWarning() {
        warningShown = false;
        banner.hidden = true;
        announcementEl.textContent = '';
        clearInterval(countdownInterval);
        scheduleWarning();
    }

    function onActivity() {
        // Once the warning is showing, only an explicit "Extend session" click resets it - ordinary
        // activity no longer silently dismisses a decision the user is meant to make.
        if (!warningShown) {
            scheduleWarning();
        }
    }

    ['mousemove', 'keydown', 'scroll', 'touchstart', 'click'].forEach(function (eventName) {
        document.addEventListener(eventName, onActivity, { passive: true });
    });

    extendButton.addEventListener('click', function () {
        fetch(extendUrl, {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: `__RequestVerificationToken=${encodeURIComponent(antiforgeryToken)}`
        }).then(function (response) {
            if (response.ok) {
                hideWarning();
            } else {
                expireNow();
            }
        }, function () {
            expireNow();
        });
    });

    scheduleWarning();
})();
