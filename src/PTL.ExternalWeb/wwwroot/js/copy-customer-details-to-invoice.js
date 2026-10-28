// "Invoice Details are the same as customer details" checkbox (legacy
// EditCustomerDetails.aspx.vb's CheckboxCopyDetails_CheckedChanged) - copies the Customer Details
// section's values into the matching Invoice Details fields client-side, on check only (no action
// when unchecked, matching legacy).
document.addEventListener('DOMContentLoaded', () => {
  const checkbox = document.getElementById('CopyDetails');
  if (!checkbox) {
    return;
  }

  const fieldMap = [
    ['ContactName', 'InvoiceName'],
    ['Organisation', 'InvoiceOrganisation'],
    ['Address1', 'InvoiceAddress1'],
    ['Address2', 'InvoiceAddress2'],
    ['Address3', 'InvoiceAddress3'],
    ['Address4', 'InvoiceAddress4'],
    ['Address5', 'InvoiceAddress5'],
    ['CountryId', 'InvoiceCountryId'],
    ['Telephone', 'InvoiceTelephone'],
    ['Telephone2', 'InvoiceTelephone2'],
    ['Email', 'InvoiceEmail']
  ];

  checkbox.addEventListener('change', () => {
    if (!checkbox.checked) {
      return;
    }

    fieldMap.forEach(([sourceId, targetId]) => {
      const source = document.getElementById(sourceId);
      const target = document.getElementById(targetId);
      if (source && target) {
        target.value = source.value;
      }
    });
  });
});
