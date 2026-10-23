// Inactivity warning: session-timeout.js. Independent of the underlying auth cookie/token refresh -
// this only drives the client-side "Extend session?" warning and the active expiry it enforces.
(function () {
    'use strict';

    var banner = document.getElementById('session-timeout-warning');
    if (!banner) {
        return;
    }

    var warningAfterMs = parseInt(banner.getAttribute('data-warning-after-ms'), 10);
    var expireAfterMs = parseInt(banner.getAttribute('data-expire-after-ms'), 10);
    var extendUrl = banner.getAttribute('data-extend-url');
    var expiredUrl = banner.getAttribute('data-expired-url');
    var countdownEl = document.getElementById('session-timeout-countdown');
    var announcementEl = document.getElementById('session-timeout-countdown-announcement');
    var extendButton = document.getElementById('session-timeout-extend');
    var antiforgeryToken = banner.getAttribute('data-antiforgery-token');

    // Seconds-remaining points at which the hidden live region re-announces the countdown - not
    // every second, which would be unusably disruptive for screen reader users.
    var ANNOUNCE_MILESTONES = [60, 30, 10];

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
        if (ANNOUNCE_MILESTONES.indexOf(remainingSeconds) === -1 || announcedMilestones[remainingSeconds]) {
            return;
        }

        announcedMilestones[remainingSeconds] = true;
        announcementEl.textContent = 'You will be signed out in ' + remainingSeconds + ' seconds unless you choose to continue.';
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
            body: '__RequestVerificationToken=' + encodeURIComponent(antiforgeryToken)
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
