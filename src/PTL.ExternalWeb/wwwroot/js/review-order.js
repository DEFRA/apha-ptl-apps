// Pending order (legacy PendingContractOrder.aspx stage 2): Submit stays disabled until the terms - and any data
// consent - are ticked. The page works without this script; the server rejects an unticked submit with an error.
document.addEventListener('DOMContentLoaded', () => {
  // Legacy's Print ran window.print() on this page; it needs script, so the button stays hidden without it.
  const print = document.getElementById('print-order');
  if (print) {
    print.hidden = false;
    print.addEventListener('click', () => window.print());
  }

  // A saved PDF is named after the page title, so print without the trailing "- GOV.UK".
  const pageTitle = document.title;
  window.addEventListener('beforeprint', () => {
    document.title = pageTitle.replace(/ - GOV\.UK$/, '');
  });
  window.addEventListener('afterprint', () => {
    document.title = pageTitle;
  });

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
