// Initialises TinyMCE over any textarea marked data-module="ptl-rich-text". Self-hosted from
// wwwroot/lib/tinymce - no CDN. Configuration mirrors the legacy Scheme.aspx tinyMCE.init call so
// stored markup stays byte-compatible with existing fldInstructions data.
(function () {
    'use strict';

    var editors = document.querySelectorAll('textarea[data-module="ptl-rich-text"]');
    if (editors.length === 0 || typeof tinymce === 'undefined') {
        return;
    }

    editors.forEach(function (textarea) {
        tinymce.init({
            target: textarea,
            base_url: '/lib/tinymce',
            license_key: 'gpl',
            plugins: 'charmap',
            toolbar: textarea.getAttribute('data-toolbar'),
            height: parseInt(textarea.getAttribute('data-height'), 10) || 400,
            readonly: textarea.getAttribute('data-readonly') === 'true',
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
                    e.content = e.content.replace(/&#39/g, '&apos');
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
    });

    // TinyMCE keeps its content in an iframe until asked to write it back.
    document.addEventListener('submit', function () {
        if (typeof tinymce !== 'undefined') {
            tinymce.triggerSave();
        }
    }, true);
})();
