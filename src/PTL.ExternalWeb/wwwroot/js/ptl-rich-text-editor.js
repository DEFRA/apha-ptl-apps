// Initialises TinyMCE over any textarea marked data-module="ptl-rich-text". If TinyMCE is
// unavailable, the textarea remains usable as a standard input.
(function () {
    'use strict';

    var DEFAULT_EDITOR_HEIGHT = 400;

    var editors = document.querySelectorAll('textarea[data-module="ptl-rich-text"]');
    if (editors.length === 0 || typeof tinymce === 'undefined') {
        return;
    }

    function initEditor(textarea) {
        if (textarea.dataset.ptlRichTextInitialised === 'true') {
            return;
        }

        textarea.dataset.ptlRichTextInitialised = 'true';

        if (textarea.dataset.ptlServerValue !== undefined) {
            textarea.value = textarea.dataset.ptlServerValue;
        }

        tinymce.init({
            target: textarea,
            base_url: '/lib/tinymce',
            license_key: 'gpl',
            plugins: 'lists save charmap',
            toolbar: textarea.dataset.toolbar,
            height: Number.parseInt(textarea.dataset.height, 10) || DEFAULT_EDITOR_HEIGHT,
            readonly: textarea.dataset.readonly === 'true',
            menubar: false,
            branding: false,
            statusbar: false,
            promotion: false,
            entity_encoding: 'named',
            encoding: 'xml',
            setup: function (editor) {
                editor.on('SaveContent', function (e) {
                    e.content = e.content.replaceAll('&#39', '&apos');
                });

                editor.on('BeforeSetContent', function (e) {
                    if (e.content?.includes('&lt;')) {
                        e.content = e.content
                            .replaceAll('&lt;', '<')
                            .replaceAll('&gt;', '>')
                            .replaceAll('&amp;', '&');
                    }
                });
            }
        });
    }

    editors.forEach(function (textarea) {
        var panel = textarea.closest('.govuk-tabs__panel');
        if (!panel?.classList.contains('govuk-tabs__panel--hidden')) {
            initEditor(textarea);
            return;
        }

        var observer = new MutationObserver(function () {
            if (!panel.classList.contains('govuk-tabs__panel--hidden')) {
                observer.disconnect();
                initEditor(textarea);
            }
        });
        observer.observe(panel, { attributes: true, attributeFilter: ['class'] });
    });

    document.addEventListener('click', function (event) {
        var link = event.target.closest?.('.govuk-error-summary a[href^="#"]');
        var editor = link ? tinymce.get(link.getAttribute('href').slice(1)) : null;
        if (editor) {
            event.preventDefault();
            editor.focus();
        }
    });

    document.addEventListener('submit', function () {
        if (typeof tinymce !== 'undefined') {
            tinymce.triggerSave();
        }
    }, true);
})();
