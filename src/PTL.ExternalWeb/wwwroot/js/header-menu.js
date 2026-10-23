// Toggles the header "Menu" dropdown panel (see GovUkPageHeaderTagHelper/GovUkMenuPanelTagHelper).
// Same behaviour at every screen width - no desktop/mobile breakpoint distinction.
document.addEventListener('DOMContentLoaded', () => {
  const toggle = document.getElementById('app-menu-toggle');
  const panel = document.getElementById('app-menu-panel');
  if (!toggle || !panel) {
    return;
  }

  function openMenu() {
    toggle.setAttribute('aria-expanded', 'true');
    panel.hidden = false;
  }

  function closeMenu() {
    toggle.setAttribute('aria-expanded', 'false');
    panel.hidden = true;
  }

  toggle.addEventListener('click', (event) => {
    const isExpanded = toggle.getAttribute('aria-expanded') === 'true';
    if (isExpanded) {
      closeMenu();
    } else {
      openMenu();
    }
    event.stopPropagation();
  });

  document.addEventListener('click', (event) => {
    if (panel.hidden) {
      return;
    }
    if (!panel.contains(event.target) && !toggle.contains(event.target)) {
      closeMenu();
    }
  });

  document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape' && !panel.hidden) {
      closeMenu();
      toggle.focus();
    }
  });
});
