// Initialises TinyMCE over any textarea marked data-module="ptl-rich-text" in ExternalWeb.
// If TinyMCE is unavailable, the textarea remains usable as a standard input.
(function () {
    'use strict';

    var DEFAULT_EDITOR_HEIGHT = 400;

    var editors = document.querySelectorAll('textarea[data-module="ptl-rich-text"]');
    if (editors.length === 0 || typeof tinymce === 'undefined') {
        return;
    }

    function setUpEditorContentHooks(editor) {
        editor.on('SaveContent', function (eventArgs) {
            eventArgs.content = eventArgs.content.replaceAll('&#39', '&apos');
        });

        editor.on('BeforeSetContent', function (eventArgs) {
            if (!eventArgs.content?.includes('&lt;')) {
                return;
            }

            eventArgs.content = eventArgs.content
                .replaceAll('&lt;', '<')
                .replaceAll('&gt;', '>')
                .replaceAll('&amp;', '&');
        });
    }

    function startTinyMce(textarea) {
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
            setup: setUpEditorContentHooks
        });
    }

    function initEditor(textarea) {
        if (textarea.dataset.ptlRichTextInitialised === 'true') {
            return;
        }

        textarea.dataset.ptlRichTextInitialised = 'true';

        if (textarea.dataset.ptlServerValue !== undefined) {
            textarea.value = textarea.dataset.ptlServerValue;
        }

        startTinyMce(textarea);
    }

    editors.forEach(function (textarea) {
        var tabPanel = textarea.closest('.govuk-tabs__panel');
        if (!tabPanel?.classList.contains('govuk-tabs__panel--hidden')) {
            initEditor(textarea);
            return;
        }

        var panelObserver = new MutationObserver(function () {
            if (tabPanel.classList.contains('govuk-tabs__panel--hidden')) {
                return;
            }

            panelObserver.disconnect();
            initEditor(textarea);
        });

        panelObserver.observe(tabPanel, { attributes: true, attributeFilter: ['class'] });
    });

    document.addEventListener('click', function (event) {
        var link = event.target.closest?.('.govuk-error-summary a[href^="#"]');
        var editor = link ? tinymce.get(link.getAttribute('href').slice(1)) : null;

        if (!editor) {
            return;
        }

        event.preventDefault();
        editor.focus();
    });

    document.addEventListener('submit', function () {
        if (typeof tinymce !== 'undefined') {
            tinymce.triggerSave();
        }
    }, true);
})();
