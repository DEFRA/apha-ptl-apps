// Create New Order (legacy PendingContractOrder.aspx): every checkbox change is saved to the draft order on the
// server, and the order body is replaced with the re-priced result, so prices and totals update without a page refresh.
document.addEventListener('DOMContentLoaded', () => {
  const container = document.getElementById('create-order');
  const body = document.getElementById('create-order-body');
  const errorMessage = document.getElementById('create-order-error');
  if (!container || !body || !errorMessage) {
    return;
  }

  const token = container.querySelector('input[name="__RequestVerificationToken"]');

  body.addEventListener('change', async (event) => {
    const input = event.target;
    if (!(input instanceof HTMLInputElement) || !input.dataset.orderField || !token) {
      return;
    }

    const focusId = input.id;
    errorMessage.hidden = true;

    // inert stops a second click racing the save; the body is replaced as soon as the server responds.
    body.inert = true;
    body.setAttribute('aria-busy', 'true');
    let failed = false;

    try {
      const response = await fetch(container.dataset.changeUrl, {
        method: 'POST',
        credentials: 'same-origin',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: new URLSearchParams({
          __RequestVerificationToken: token.value,
          year: container.dataset.year,
          search: container.dataset.search ?? '',
          schemeId: input.dataset.schemeId,
          field: input.dataset.orderField,
          checked: String(input.checked)
        })
      });

      if (!response.ok) {
        throw new Error(`Order change failed: ${response.status}`);
      }

      body.innerHTML = await response.text();
    } catch {
      failed = true;
      input.checked = !input.checked;
      errorMessage.hidden = false;
    } finally {
      body.inert = false;
      body.removeAttribute('aria-busy');
      (failed ? errorMessage : document.getElementById(focusId))?.focus();
    }
  });
});
