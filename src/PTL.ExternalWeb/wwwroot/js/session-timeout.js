// Inactivity warning: session-timeout.js. Independent of the underlying auth cookie/token refresh -
// this only drives the client-side "Extend session?" dialog and the active expiry it enforces.
// The timers, announcements and extend call come from PTL.SharedUI's ptl-session-timeout.js.
(function () {
    'use strict';

    var sessionTimeout = window.ptlSessionTimeout;
    var dialog = document.getElementById('session-timeout-warning');
    if (!dialog) {
        return;
    }

    var warningAfterMs = Number.parseInt(dialog.dataset.warningAfterMs, 10);
    var expireAfterMs = Number.parseInt(dialog.dataset.expireAfterMs, 10);
    var extendUrl = sessionTimeout.toSafeRelativeUrl(dialog.dataset.extendUrl);
    var expiredUrl = sessionTimeout.toSafeRelativeUrl(dialog.dataset.expiredUrl);
    var signOutUrl = sessionTimeout.toSafeRelativeUrl(dialog.dataset.signoutUrl);
    var countdownEl = document.getElementById('session-timeout-countdown');
    var announcementEl = document.getElementById('session-timeout-countdown-announcement');
    var titleEl = document.getElementById('session-timeout-warning-title');
    var extendButton = document.getElementById('session-timeout-extend');
    var signOutButton = document.getElementById('session-timeout-signout');
    var antiforgeryToken = dialog.dataset.antiforgeryToken;

    if (!extendUrl || !expiredUrl || !signOutUrl) {
        return;
    }

    var SECONDS_PER_MINUTE = 60;

    var warningShown = false;
    var countdown = null;
    var announcer = sessionTimeout.milestoneAnnouncer(announcementEl, function (remainingSeconds) {
        return `Your session will expire in ${formatDuration(remainingSeconds)} unless you extend it.`;
    });
    var idle = sessionTimeout.idleTimer(warningAfterMs, showWarning, function () {
        return warningShown;
    });

    function expireNow() {
        window.location.href = expiredUrl;
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
        announcer.reset();
        countdown = sessionTimeout.countdown(expireAfterMs, updateCountdown, expireNow);
        openDialog();
    }

    function updateCountdown(remainingSeconds) {
        countdownEl.textContent = formatDuration(remainingSeconds);
        announcer.announce(remainingSeconds);
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

    function hideWarning() {
        warningShown = false;
        announcer.clear();
        countdown.stop();
        closeDialog();
        idle.schedule();
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

    extendButton.addEventListener('click', function () {
        sessionTimeout.extendSession(extendUrl, antiforgeryToken, hideWarning, expireNow);
    });

    // "No, sign out" ends the session now through the normal sign-out POST (a form post, because the response
    // redirects the browser to CIDM's sign-out page).
    signOutButton.addEventListener('click', function () {
        warningShown = false;
        idle.cancel();
        if (countdown) {
            countdown.stop();
        }
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

    idle.schedule();
})();
