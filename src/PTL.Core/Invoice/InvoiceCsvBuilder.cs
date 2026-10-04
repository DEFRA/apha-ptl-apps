using System.Globalization;
using System.Text;

namespace PTL.Core.Invoice;

// Reproduces InvoiceGeneration.aspx.vb's CSV construction exactly (docs/analysis/invoice-analysis.md):
// same column order/names, same hardcoded literals, same quoting (every value wrapped in literal
// double quotes, no further escaping), same raw ToString() formatting (no N2/number formatting),
// and the same ragged row shape (Postage/Admin Charge rows have no "Redistribution" value; only
// contract-item rows do). Moved out of the UI layer into Core so it is unit-testable, per
// docs/migration/invoice-migration.md's CSV Generation section - the column shape itself is
// deliberately NOT redesigned.
public static class InvoiceCsvBuilder
{
    private static readonly string[] Headers =
    [
        "Customer", "Customer Number", "Cost Centre Code", "Contract Code", "Special Instructions",
        "Invoice Details", "PO Number", "Customer Type", "Opt Out Of Invoice Generation",
        "Non Fee Paying", "Line Item", "Description", "Unit of Measure", "Selling Price", "Quantity",
        "Total Amt", "VAT Number", "VAT Rating", "Redistribution"
    ];

    public static string Build(InvoicePendingData data)
    {
        var itemsByContract = data.Items.ToLookup(i => i.ContractId);
        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",", Headers));

        foreach (var contract in data.Contracts)
        {
            var items = itemsByContract[contract.ContractId].ToList();
            // Legacy `If (contract.ContractItems.Count > 0) Then` - a contract with no items is
            // still marked invoiced (see IInvoiceRepository), it just contributes no CSV rows.
            if (items.Count == 0)
            {
                continue;
            }

            var lineNumber = 1;

            if (contract.CombinedPostagePriceTotal > 0)
            {
                AppendRow(
                    csv,
                    contract,
                    nonFeePaying: string.Empty,
                    lineNumber: lineNumber,
                    description: "Combined Postage (Courier, Biofreeze and Dry-Ice)",
                    price: contract.CombinedPostagePriceTotal,
                    redistribution: null);
                lineNumber++;
            }

            if (contract.AdministrationCharge > 0)
            {
                AppendRow(
                    csv,
                    contract,
                    nonFeePaying: string.Empty,
                    lineNumber: lineNumber,
                    description: "Admin charge",
                    price: contract.AdministrationCharge,
                    redistribution: null);
                lineNumber++;
            }

            foreach (var item in items)
            {
                AppendRow(
                    csv,
                    contract,
                    nonFeePaying: item.NonFeePaying.ToString(),
                    lineNumber: lineNumber,
                    description: item.Description,
                    price: item.Price * (1 - contract.DiscountRate),
                    redistribution: item.HasOverride.ToString());
                lineNumber++;
            }
        }

        return csv.ToString();
    }

    private static void AppendRow(
        StringBuilder csv,
        InvoiceContractEntity contract,
        string nonFeePaying,
        int lineNumber,
        string description,
        decimal price,
        string? redistribution)
    {
        var priceText = price.ToString(CultureInfo.InvariantCulture);
        var columns = new List<string>
        {
            Quoted(contract.InvoiceOrganisation),
            Quoted(contract.CustomerNumber),
            Quoted("35500"),
            Quoted("CSUT1306"),
            Quoted(contract.SpecialInstructions),
            Quoted(contract.InvoiceDetails),
            Quoted(contract.PurchaseOrderNumber),
            Quoted(contract.CustomerType),
            Quoted(contract.OptOutOfInvoiceGeneration.ToString()),
            Quoted(nonFeePaying),
            Quoted(lineNumber.ToString(CultureInfo.InvariantCulture)),
            Quoted(description),
            Quoted("1"),
            Quoted(priceText),
            Quoted("1"),
            Quoted(priceText),
            Quoted(contract.VatNumber),
            Quoted(contract.VatRating)
        };

        if (redistribution is not null)
        {
            columns.Add(Quoted(redistribution));
        }

        csv.AppendLine(string.Join(",", columns));
    }

    private static string Quoted(string value) => $"\"{value}\"";
}
