// Initialises TinyMCE over any textarea marked data-module="ptl-rich-text" in InternalWeb.
// Self-hosted from wwwroot/lib/tinymce - no CDN. Configuration keeps stored markup compatible.
(function () {
    'use strict';

    var defaultEditorHeight = 400;

    var editorTextareas = document.querySelectorAll('textarea[data-module="ptl-rich-text"]');
    if (editorTextareas.length === 0 || typeof tinymce === 'undefined') {
        return;
    }

    // A GOV.UK tab panel that isn't the default-selected one is rendered with
    // govuk-tabs__panel--hidden (display: none) from the server, on every load including a
    // validation-failure re-render. Initialising TinyMCE while its textarea is hidden leaves the
    // raw markup showing as literal text instead of the WYSIWYG view, so initialisation for a
    // field in a hidden panel is deferred until that panel's hidden class is actually removed.
    function initialiseEditor(textarea) {
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
            plugins: 'lists save charmap',
            toolbar: textarea.dataset.toolbar,
            height: Number.parseInt(textarea.dataset.height, 10) || defaultEditorHeight,
            readonly: textarea.dataset.readonly === 'true',
            menubar: false,
            branding: false,
            statusbar: false,
            promotion: false,
            entity_encoding: 'named',
            encoding: 'xml',
            setup: function (editor) {
                // Legacy SaveContent hook.
                editor.on('SaveContent', function (e) {
                    e.content = e.content.replaceAll('&#39', '&apos');
                });

                // Legacy BeforeSetContent hook - unescapes content that was stored entity-escaped.
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

    editorTextareas.forEach(function (textarea) {
        var tabsPanel = textarea.closest('.govuk-tabs__panel');
        if (!tabsPanel?.classList.contains('govuk-tabs__panel--hidden')) {
            initialiseEditor(textarea);
            return;
        }

        var visibilityObserver = new MutationObserver(function () {
            if (tabsPanel.classList.contains('govuk-tabs__panel--hidden')) {
                return;
            }

            visibilityObserver.disconnect();
            initialiseEditor(textarea);
        });

        visibilityObserver.observe(tabsPanel, { attributes: true, attributeFilter: ['class'] });
    });

    // Move focus from error-summary anchor targets into TinyMCE when a linked field is hidden.
    document.addEventListener('click', function (event) {
        var targetLink = event.target.closest?.('.govuk-error-summary a[href^="#"]');
        var richTextEditor = targetLink ? tinymce.get(targetLink.getAttribute('href').slice(1)) : null;
        if (!richTextEditor) {
            return;
        }

        event.preventDefault();
        richTextEditor.focus();
    });

    // TinyMCE keeps its content in an iframe until asked to write it back.
    document.addEventListener('submit', function () {
        if (typeof tinymce !== 'undefined') {
            tinymce.triggerSave();
        }
    }, true);
})();
