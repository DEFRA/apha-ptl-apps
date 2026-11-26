// Behaviour that used to sit in inline on* handlers and javascript: links, which the Content-Security-Policy refuses.
// Shared by InternalWeb and ExternalWeb as /_content/PTL.SharedUI/js/ptl-unobtrusive.js.
(function () {
    'use strict';

    // Page-size selector: picking a size submits its form straight away.
    document.addEventListener('change', function (event) {
        const select = event.target.closest?.('[data-ptl-auto-submit]');
        if (select?.form) {
            select.form.submit();
        }
    });

    // "Back" links that return to the previous page in the browser history.
    document.addEventListener('click', function (event) {
        const link = event.target.closest?.('[data-ptl-back]');
        if (link) {
            event.preventDefault();
            history.back();
        }
    });
})();
