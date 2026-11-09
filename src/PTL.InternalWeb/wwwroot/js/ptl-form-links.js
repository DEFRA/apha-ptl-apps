// Row actions on the pending-list pages (Countries, Viewers, External Test Consultants, Internal Test Consultant
// Department) are links, but they act on the whole pending list, which lives in the page's form. A link with
// data-form-submit posts the form to the link's address; data-skip-validation skips client-side validation.
// Without JavaScript the link is a plain GET to the same address, which reloads the page.
(function () {
    'use strict';

    document.addEventListener('click', function (event) {
        if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
            return;
        }

        var link = event.target.closest('a[data-form-submit]');
        var form = link && link.closest('form');
        if (!form) {
            return;
        }

        event.preventDefault();
        form.setAttribute('action', link.getAttribute('href'));

        if (link.hasAttribute('data-skip-validation')) {
            form.noValidate = true;
            form.submit();
        } else {
            form.requestSubmit();
        }
    });
})();
