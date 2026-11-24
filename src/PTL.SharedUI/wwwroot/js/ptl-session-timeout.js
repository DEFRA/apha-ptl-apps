// Inactivity-warning plumbing shared by InternalWeb and ExternalWeb as /_content/PTL.SharedUI/js/ptl-session-timeout.js.
// Each app's own session-timeout.js loads after it and owns the warning's markup, wording and focus handling.
window.ptlSessionTimeout = (function () {
    'use strict';

    var MILLISECONDS_PER_SECOND = 1000;
    // Seconds-remaining points at which the hidden live region re-announces the time left - not
    // every second, which would be unusably disruptive for screen reader users.
    var ONE_MINUTE_REMAINING = 60;
    var THIRTY_SECONDS_REMAINING = 30;
    var TEN_SECONDS_REMAINING = 10;
    var ANNOUNCE_MILESTONES = new Set([ONE_MINUTE_REMAINING, THIRTY_SECONDS_REMAINING, TEN_SECONDS_REMAINING]);
    var ACTIVITY_EVENTS = ['mousemove', 'keydown', 'scroll', 'touchstart', 'click'];

    // Only ever a same-origin relative path (never scheme-relative/absolute) is accepted here -
    // these attributes are server-generated and trustworthy today, but navigating/fetching based on
    // unvalidated DOM-read text is a DOM-based open-redirect risk if that ever changes (CodeQL
    // js/xss-through-dom). Returns null for anything else so callers can fail safe.
    function toSafeRelativeUrl(value) {
        return typeof value === 'string' && /^\/(?!\/)/.test(value) ? value : null;
    }

    // Calls onIdle after warningAfterMs without activity. Once the warning is showing, only the user's
    // explicit choice resets it - ordinary activity no longer silently dismisses a decision they are meant to make.
    function idleTimer(warningAfterMs, onIdle, isWarningShown) {
        var timer = null;

        function schedule() {
            clearTimeout(timer);
            timer = setTimeout(onIdle, warningAfterMs);
        }

        function onActivity() {
            if (!isWarningShown()) {
                schedule();
            }
        }

        ACTIVITY_EVENTS.forEach(function (eventName) {
            document.addEventListener(eventName, onActivity, { passive: true });
        });

        return {
            schedule: schedule,
            cancel: function () {
                clearTimeout(timer);
            }
        };
    }

    // Calls onTick with the whole seconds left now and every second after, then onExpired once none are left.
    function countdown(durationMs, onTick, onExpired) {
        var expiresAt = Date.now() + durationMs;
        var interval = null;

        function tick() {
            var remainingSeconds = Math.round((expiresAt - Date.now()) / MILLISECONDS_PER_SECOND);
            if (remainingSeconds <= 0) {
                clearInterval(interval);
                onExpired();
                return;
            }

            onTick(remainingSeconds);
        }

        tick();
        interval = setInterval(tick, MILLISECONDS_PER_SECOND);

        return {
            stop: function () {
                clearInterval(interval);
            }
        };
    }

    // Writes messageFor(seconds) into the live region once per milestone per warning.
    function milestoneAnnouncer(element, messageFor) {
        var announced = {};

        return {
            reset: function () {
                announced = {};
            },
            clear: function () {
                element.textContent = '';
            },
            announce: function (remainingSeconds) {
                if (!ANNOUNCE_MILESTONES.has(remainingSeconds) || announced[remainingSeconds]) {
                    return;
                }

                announced[remainingSeconds] = true;
                element.textContent = messageFor(remainingSeconds);
            }
        };
    }

    function extendSession(url, antiforgeryToken, onExtended, onFailed) {
        fetch(url, {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: `__RequestVerificationToken=${encodeURIComponent(antiforgeryToken)}`
        }).then(function (response) {
            if (response.ok) {
                onExtended();
            } else {
                onFailed();
            }
        }, function () {
            onFailed();
        });
    }

    return {
        toSafeRelativeUrl: toSafeRelativeUrl,
        idleTimer: idleTimer,
        countdown: countdown,
        milestoneAnnouncer: milestoneAnnouncer,
        extendSession: extendSession
    };
})();
