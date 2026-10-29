// Toggles the header "Menu" dropdown panel (see GovUkPageHeaderTagHelper/GovUkMenuPanelTagHelper).
// Same behaviour at every screen width - no desktop/mobile breakpoint distinction.
document.addEventListener('DOMContentLoaded', () => {
  const ARIA_EXPANDED = 'aria-expanded';
  const toggle = document.getElementById('app-menu-toggle');
  const panel = document.getElementById('app-menu-panel');
  if (!toggle || !panel) {
    return;
  }

  function openMenu() {
    toggle.setAttribute(ARIA_EXPANDED, 'true');
    panel.hidden = false;
  }

  function closeMenu() {
    toggle.setAttribute(ARIA_EXPANDED, 'false');
    panel.hidden = true;
  }

  toggle.addEventListener('click', (event) => {
    const isExpanded = toggle.getAttribute(ARIA_EXPANDED) === 'true';
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
