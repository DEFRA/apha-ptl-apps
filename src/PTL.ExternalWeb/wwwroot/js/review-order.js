// Pending order (legacy PendingContractOrder.aspx stage 2): Submit stays disabled until the terms - and any data
// consent - are ticked. The page works without this script; the server rejects an unticked submit with an error.
document.addEventListener('DOMContentLoaded', () => {
  const terms = document.getElementById('TermsAccepted');
  const submit = document.getElementById('submit-order');
  if (!terms || !submit) {
    return;
  }

  const consents = [...document.querySelectorAll('input[name="ConsentedSchemeIds"]')];

  const update = () => {
    const ready = terms.checked && consents.every((consent) => consent.checked);
    submit.disabled = !ready;
    submit.setAttribute('aria-disabled', String(!ready));
  };

  terms.addEventListener('change', update);
  consents.forEach((consent) => consent.addEventListener('change', update));
  update();
});
