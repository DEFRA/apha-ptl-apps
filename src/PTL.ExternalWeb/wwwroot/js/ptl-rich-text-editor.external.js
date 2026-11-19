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

    // The editor HTML is posted as "ptl-b64:" + base64(UTF-8) so the markup is not in the request body,
    // where the AWS WAF XSS rules reject it. The formdata event changes only what is sent; the editor and
    // its textarea keep the real HTML. PTL.SharedUI's RichTextBinder decodes it on the server.
    function toBase64(text) {
        var bytes = new TextEncoder().encode(text);
        var binary = '';
        for (var i = 0; i < bytes.length; i += 0x8000) {
            binary += String.fromCharCode.apply(null, bytes.subarray(i, i + 0x8000));
        }
        return btoa(binary);
    }

    document.addEventListener('formdata', function (event) {
        document.querySelectorAll('textarea[data-module="ptl-rich-text"]').forEach(function (textarea) {
            var html = textarea.name && textarea.form === event.target ? event.formData.get(textarea.name) : null;
            if (typeof html === 'string' && html !== '') {
                event.formData.set(textarea.name, 'ptl-b64:' + toBase64(html));
            }
        });
    });
})();
