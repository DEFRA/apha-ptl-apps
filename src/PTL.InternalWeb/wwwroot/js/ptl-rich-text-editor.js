// Initialises TinyMCE over any textarea marked data-module="ptl-rich-text". Self-hosted from
// wwwroot/lib/tinymce - no CDN. Configuration mirrors the legacy Scheme.aspx tinyMCE.init call so
// stored markup stays byte-compatible with existing fldInstructions data.
(function () {
    'use strict';

    var DEFAULT_EDITOR_HEIGHT = 400;

    var editors = document.querySelectorAll('textarea[data-module="ptl-rich-text"]');
    if (editors.length === 0 || typeof tinymce === 'undefined') {
        return;
    }

    // A GOV.UK tab panel that isn't the default-selected one is rendered with
    // govuk-tabs__panel--hidden (display: none) from the server, on every load including a
    // validation-failure re-render. Initialising TinyMCE while its textarea is hidden leaves the
    // raw markup showing as literal text instead of the WYSIWYG view, so initialisation for a
    // field in a hidden panel is deferred until that panel's hidden class is actually removed.
    function initEditor(textarea) {
        if (textarea.dataset.ptlRichTextInitialised === 'true') {
            return;
        }

        textarea.dataset.ptlRichTextInitialised = 'true';

        // Chrome/Firefox restore a textarea's own remembered value on a POST-triggered reload,
        // which wins over the server's freshly rendered value and leaves TinyMCE reading stale,
        // already-escaped text. data-ptl-server-value isn't subject to that restoration, so force
        // it back onto the field before TinyMCE ever reads it.
        if (textarea.dataset.ptlServerValue !== undefined) {
            textarea.value = textarea.dataset.ptlServerValue;
        }

        tinymce.init({
            target: textarea,
            base_url: '/lib/tinymce',
            license_key: 'gpl',
            plugins: 'charmap',
            toolbar: textarea.dataset.toolbar,
            height: Number.parseInt(textarea.dataset.height, 10) || DEFAULT_EDITOR_HEIGHT,
            readonly: textarea.dataset.readonly === 'true',
            menubar: false,
            branding: false,
            statusbar: false,
            promotion: false,
            // Legacy stored named entities (&deg; &nbsp;) - keep emitting them so existing rows and
            // newly saved rows look the same in the database.
            entity_encoding: 'named',
            encoding: 'xml',
            setup: function (editor) {
                // Legacy SaveContent hook.
                editor.on('SaveContent', function (e) {
                    e.content = e.content.replaceAll('&#39', '&apos');
                });

                // The error summary links to the field id, so focus has to reach the editor.
                editor.on('init', function () {
                    var container = editor.getContainer();
                    if (container) {
                        container.setAttribute('data-editor-for', textarea.id);
                    }
                });
            }
        });
    }

    editors.forEach(function (textarea) {
        var panel = textarea.closest('.govuk-tabs__panel');
        if (!panel || !panel.classList.contains('govuk-tabs__panel--hidden')) {
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

    // TinyMCE keeps its content in an iframe until asked to write it back.
    document.addEventListener('submit', function () {
        if (typeof tinymce !== 'undefined') {
            tinymce.triggerSave();
        }
    }, true);
})();
