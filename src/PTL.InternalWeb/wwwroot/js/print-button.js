// Wires [data-ptl-print] buttons to window.print() so pages need no inline onclick (CSP).
(function () {
    "use strict";

    document.addEventListener("DOMContentLoaded", function () {
        document.querySelectorAll("[data-ptl-print]").forEach(function (button) {
            button.addEventListener("click", function () {
                window.print();
            });
        });
    });
})();
