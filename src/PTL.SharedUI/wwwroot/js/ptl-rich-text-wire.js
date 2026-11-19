// Posts the HTML of every rich text editor textarea (data-module="ptl-rich-text") as
// "ptl-b64:" + base64(UTF-8), so the markup is not in the request body, where the AWS WAF XSS rules
// reject it. The formdata event changes only what is sent; the editor and its textarea keep the real
// HTML. PTL.SharedUI's RichTextModelBinder decodes it on the server.
// Shared by InternalWeb and ExternalWeb as /_content/PTL.SharedUI/js/ptl-rich-text-wire.js.
(function () {
    'use strict';

    var prefix = 'ptl-b64:';
    var chunkSize = 0x8000;

    function toBase64(text) {
        var bytes = new TextEncoder().encode(text);
        var binary = '';
        for (var i = 0; i < bytes.length; i += chunkSize) {
            binary += String.fromCodePoint.apply(null, bytes.subarray(i, i + chunkSize));
        }
        return btoa(binary);
    }

    document.addEventListener('formdata', function (event) {
        document.querySelectorAll('textarea[data-module="ptl-rich-text"]').forEach(function (textarea) {
            var html = textarea.name && textarea.form === event.target ? event.formData.get(textarea.name) : null;
            if (typeof html === 'string' && html !== '') {
                event.formData.set(textarea.name, prefix + toBase64(html));
            }
        });
    });
})();
