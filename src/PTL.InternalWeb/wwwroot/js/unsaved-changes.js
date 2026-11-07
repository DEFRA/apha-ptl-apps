// Legacy NavigateAway()/SetNavigateAwayDialog, without a browser dialog: while the page holds
// unsaved changes, following a link reveals the GOV.UK notification banner, names where the user was
// heading and offers an explicit way out. Save, Apply and Cancel are submit buttons, so they are
// never intercepted. Progressive enhancement - with scripting off the page behaves as before.
(function () {
    'use strict';

    var banner = document.getElementById('unsaved-changes');
    if (!banner) {
        return;
    }

    var blocked = document.getElementById('unsaved-changes-blocked');
    var blockedText = document.getElementById('unsaved-changes-blocked-text');
    var leave = document.getElementById('unsaved-changes-leave');
    var dirty = !banner.hasAttribute('hidden');

    var watched = document.querySelector('[data-unsaved-watch]');
    if (watched) {
        // Editing arms the warning. The banner stays hidden until a link is followed, unless the page
        // opts in with data-unsaved-reveal to show it as soon as there is something unsaved.
        // Controls marked data-unsaved-ignore (e.g. selections that are never saved) do not count.
        var revealOnChange = watched.hasAttribute('data-unsaved-reveal');
        var markDirty = function (event) {
            if (event.target.closest && event.target.closest('[data-unsaved-ignore]')) {
                return;
            }

            dirty = true;
            if (revealOnChange) {
                banner.removeAttribute('hidden');
            }
        };

        watched.addEventListener('input', markDirty);
        watched.addEventListener('change', markDirty);
    }

    // In-page anchors, downloads, new tabs and the banner's own escape link are left alone.
    function isProtected(link) {
        if (link === leave || link.hasAttribute('download') || link.target === '_blank') {
            return false;
        }

        var href = link.getAttribute('href');
        return !!href && href.charAt(0) !== '#' && href.toLowerCase().indexOf('javascript:') !== 0;
    }

    document.addEventListener('click', function (event) {
        if (!dirty || !event.target.closest) {
            return;
        }

        var link = event.target.closest('a[href]');
        if (!link || !isProtected(link)) {
            return;
        }

        event.preventDefault();
        banner.removeAttribute('hidden');

        var name = (link.textContent || '').replace(/\s+/g, ' ').trim();
        blockedText.textContent = name
            ? 'You selected "' + name + '". Your changes will be lost if you go there now. '
            : 'Your changes will be lost if you leave this page now. ';
        leave.setAttribute('href', link.href);
        blocked.removeAttribute('hidden');
        banner.focus();
    });
})();
