document.addEventListener('click', event => {
    const hideButton = event.target.closest('[data-hide-cookie-confirmation="true"]');
    if (!hideButton) {
        return;
    }

    const banner = document.getElementById('cookie-confirmation-banner');
    if (banner) {
        banner.remove();
    }

    window.scrollTo({ top: 0, left: 0, behavior: 'auto' });

    const pageHeading = document.querySelector('#main-content h1');
    if (pageHeading instanceof HTMLElement) {
        pageHeading.setAttribute('tabindex', '-1');
        pageHeading.focus({ preventScroll: true });
        return;
    }

    const mainContent = document.getElementById('main-content');
    if (mainContent) {
        mainContent.setAttribute('tabindex', '-1');
        mainContent.focus({ preventScroll: true });
        return;
    }

    const skipLink = document.querySelector('.govuk-skip-link');
    if (skipLink instanceof HTMLElement) {
        skipLink.focus({ preventScroll: true });
    }
});
