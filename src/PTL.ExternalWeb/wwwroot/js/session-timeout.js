// Inactivity warning: session-timeout.js. Independent of the underlying auth cookie/token refresh -
// this only drives the client-side "Extend session?" dialog and the active expiry it enforces.
(function () {
    'use strict';

    var dialog = document.getElementById('session-timeout-warning');
    if (!dialog) {
        return;
    }

    var warningAfterMs = Number.parseInt(dialog.dataset.warningAfterMs, 10);
    var expireAfterMs = Number.parseInt(dialog.dataset.expireAfterMs, 10);
    var extendUrl = toSafeRelativeUrl(dialog.dataset.extendUrl);
    var expiredUrl = toSafeRelativeUrl(dialog.dataset.expiredUrl);
    var signOutUrl = toSafeRelativeUrl(dialog.dataset.signoutUrl);
    var countdownEl = document.getElementById('session-timeout-countdown');
    var announcementEl = document.getElementById('session-timeout-countdown-announcement');
    var titleEl = document.getElementById('session-timeout-warning-title');
    var extendButton = document.getElementById('session-timeout-extend');
    var signOutButton = document.getElementById('session-timeout-signout');
    var antiforgeryToken = dialog.dataset.antiforgeryToken;

    // Only ever a same-origin relative path (never scheme-relative/absolute) is accepted here -
    // these attributes are server-generated and trustworthy today, but navigating/fetching based on
    // unvalidated DOM-read text is a DOM-based open-redirect risk if that ever changes (CodeQL
    // js/xss-through-dom). Returns null for anything else so callers can fail safe.
    function toSafeRelativeUrl(value) {
        return typeof value === 'string' && /^\/(?!\/)/.test(value) ? value : null;
    }

    if (!extendUrl || !expiredUrl || !signOutUrl) {
        return;
    }

    // Seconds-remaining points at which the hidden live region re-announces the time left - not
    // every second, which would be unusably disruptive for screen reader users.
    var MILLISECONDS_PER_SECOND = 1000;
    var SECONDS_PER_MINUTE = 60;
    var THIRTY_SECONDS_REMAINING = 30;
    var TEN_SECONDS_REMAINING = 10;
    var ANNOUNCE_MILESTONES = new Set([SECONDS_PER_MINUTE, THIRTY_SECONDS_REMAINING, TEN_SECONDS_REMAINING]);

    var idleTimer = null;
    var countdownInterval = null;
    var warningShown = false;
    var expiresAt = 0;
    var announcedMilestones = {};

    function expireNow() {
        window.location.href = expiredUrl;
    }

    function scheduleWarning() {
        clearTimeout(idleTimer);
        idleTimer = setTimeout(showWarning, warningAfterMs);
    }

    // showModal() makes everything behind the dialog inert, traps keyboard focus inside it and moves
    // focus to the autofocus title (not a button, so no button shows its focus state until tabbed to);
    // closing it returns focus to where the user was. The plain "open" attribute is only a fallback for
    // a browser without <dialog> support.
    function openDialog() {
        if (typeof dialog.showModal === 'function') {
            if (!dialog.open) {
                dialog.showModal();
            }
        } else {
            dialog.setAttribute('open', '');
            titleEl.focus();
        }
    }

    function closeDialog() {
        if (typeof dialog.close === 'function') {
            dialog.close();
        } else {
            dialog.removeAttribute('open');
        }
    }

    function showWarning() {
        warningShown = true;
        announcedMilestones = {};
        expiresAt = Date.now() + expireAfterMs;
        updateCountdown();
        openDialog();
        countdownInterval = setInterval(updateCountdown, MILLISECONDS_PER_SECOND);
    }

    function updateCountdown() {
        var remainingSeconds = Math.round((expiresAt - Date.now()) / MILLISECONDS_PER_SECOND);
        if (remainingSeconds <= 0) {
            clearInterval(countdownInterval);
            expireNow();
            return;
        }

        countdownEl.textContent = formatDuration(remainingSeconds);
        announceMilestone(remainingSeconds);
    }

    function plural(count, unit) {
        return `${count} ${unit}${count === 1 ? '' : 's'}`;
    }

    // "2 minutes", "1 minute 30 seconds", "45 seconds" - the same wording SessionTimeoutText renders on the server.
    function formatDuration(totalSeconds) {
        var minutes = Math.floor(totalSeconds / SECONDS_PER_MINUTE);
        var seconds = totalSeconds % SECONDS_PER_MINUTE;
        var parts = [];
        if (minutes > 0) {
            parts.push(plural(minutes, 'minute'));
        }
        if (seconds > 0) {
            parts.push(plural(seconds, 'second'));
        }
        return parts.join(' ');
    }

    function announceMilestone(remainingSeconds) {
        if (!ANNOUNCE_MILESTONES.has(remainingSeconds) || announcedMilestones[remainingSeconds]) {
            return;
        }

        announcedMilestones[remainingSeconds] = true;
        announcementEl.textContent = `Your session will expire in ${formatDuration(remainingSeconds)} unless you extend it.`;
    }

    function hideWarning() {
        warningShown = false;
        announcementEl.textContent = '';
        clearInterval(countdownInterval);
        closeDialog();
        scheduleWarning();
    }

    function onActivity() {
        // Once the dialog is showing, only choosing to extend resets it - ordinary activity no longer
        // silently dismisses a decision the user is meant to make.
        if (!warningShown) {
            scheduleWarning();
        }
    }

    // Escape must not dismiss the dialog (that would hide the warning while the countdown still runs).
    // Some browsers close a modal dialog on a repeated Escape anyway, so reopen it if that happens.
    dialog.addEventListener('cancel', function (event) {
        event.preventDefault();
    });
    // Chromium raises "cancel" from the Escape key's default action, so stopping that default is what
    // reliably keeps the dialog open; the cancel and close handlers below cover other browsers.
    dialog.addEventListener('keydown', function (event) {
        if (event.key === 'Escape') {
            event.preventDefault();
        }
    });
    dialog.addEventListener('close', function () {
        if (warningShown) {
            openDialog();
        }
    });

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

    // "No, sign out" ends the session now through the normal sign-out POST (a form post, because the response
    // redirects the browser to CIDM's sign-out page).
    signOutButton.addEventListener('click', function () {
        warningShown = false;
        clearTimeout(idleTimer);
        clearInterval(countdownInterval);
        extendButton.disabled = true;
        signOutButton.disabled = true;

        var form = document.createElement('form');
        form.method = 'post';
        form.action = signOutUrl;
        var tokenInput = document.createElement('input');
        tokenInput.type = 'hidden';
        tokenInput.name = '__RequestVerificationToken';
        tokenInput.value = antiforgeryToken;
        form.appendChild(tokenInput);
        document.body.appendChild(form);
        form.submit();
    });

    scheduleWarning();
})();
