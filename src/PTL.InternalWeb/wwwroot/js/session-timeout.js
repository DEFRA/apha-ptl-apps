// Inactivity warning: session-timeout.js. Independent of the underlying auth cookie/token refresh -
// this only drives the client-side "Extend session?" warning and the active expiry it enforces.
// The timers, announcements and extend call come from PTL.SharedUI's ptl-session-timeout.js.
(function () {
    'use strict';

    var sessionTimeout = window.ptlSessionTimeout;
    var banner = document.getElementById('session-timeout-warning');
    if (!banner) {
        return;
    }

    var warningAfterMs = Number.parseInt(banner.dataset.warningAfterMs, 10);
    var expireAfterMs = Number.parseInt(banner.dataset.expireAfterMs, 10);
    var extendUrl = sessionTimeout.toSafeRelativeUrl(banner.dataset.extendUrl);
    var expiredUrl = sessionTimeout.toSafeRelativeUrl(banner.dataset.expiredUrl);
    var countdownEl = document.getElementById('session-timeout-countdown');
    var announcementEl = document.getElementById('session-timeout-countdown-announcement');
    var extendButton = document.getElementById('session-timeout-extend');
    var antiforgeryToken = banner.dataset.antiforgeryToken;

    if (!extendUrl || !expiredUrl) {
        return;
    }

    var warningShown = false;
    var countdown = null;
    var announcer = sessionTimeout.milestoneAnnouncer(announcementEl, function (remainingSeconds) {
        return `You will be signed out in ${remainingSeconds} seconds unless you choose to continue.`;
    });
    var idle = sessionTimeout.idleTimer(warningAfterMs, showWarning, function () {
        return warningShown;
    });

    function expireNow() {
        window.location.href = expiredUrl;
    }

    function showWarning() {
        warningShown = true;
        announcer.reset();
        banner.hidden = false;
        // Moves keyboard/screen-reader focus to the banner (role="alert" announces it either way,
        // but the banner sits after all page content in the DOM, so a keyboard user tabbing through
        // the page would otherwise have no indication it has appeared).
        banner.focus();

        countdown = sessionTimeout.countdown(expireAfterMs, updateCountdown, expireNow);
    }

    function updateCountdown(remainingSeconds) {
        countdownEl.textContent = formatCountdown(remainingSeconds);
        announcer.announce(remainingSeconds);
    }

    // Zero-padded mm:ss (02:00, 01:59 ...); _SessionTimeoutWarning.cshtml renders the first value the same way.
    function formatCountdown(totalSeconds) {
        var minutes = Math.floor(totalSeconds / 60);
        var seconds = totalSeconds % 60;
        return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
    }

    function hideWarning() {
        warningShown = false;
        banner.hidden = true;
        announcer.clear();
        countdown.stop();
        idle.schedule();
    }

    extendButton.addEventListener('click', function () {
        sessionTimeout.extendSession(extendUrl, antiforgeryToken, hideWarning, expireNow);
    });

    idle.schedule();
})();
