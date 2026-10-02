using PTL.Core.Invoice;

namespace PTL.Api.Tests.Invoice;

public class InvoiceCsvBuilderTests
{
    private static InvoiceContractEntity Contract(
        Guid contractId,
        decimal administrationCharge = 0m,
        decimal discountRate = 0m,
        int numberPostage = 0,
        decimal postagePrice = 0m,
        bool optOut = false) => new()
        {
            ContractId = contractId,
            QalNumber = "QAL001",
            Suffix = "A",
            InvoiceOrganisation = "Test Org",
            InvoiceAddress1 = "Unit 1",
            InvoiceAddress2 = "Test Street",
            InvoiceCountry = "UK",
            CustomerNumber = "CUST1",
            CustomerType = "Business",
            VatNumber = "VAT1",
            VatRating = "Standard",
            PurchaseOrderNumber = "PO1",
            AdministrationCharge = administrationCharge,
            DiscountRate = discountRate,
            NumberPostage = numberPostage,
            PostagePrice = postagePrice,
            OptOutOfInvoiceGeneration = optOut
        };

    private static InvoiceContractItemEntity Item(Guid contractId, decimal price = 100m, bool nonFeePaying = false, bool hasOverride = false) => new()
    {
        ContractId = contractId,
        SchemeIdentifier = "PT0001",
        SchemeName = "Scheme One",
        Price = price,
        NonFeePaying = nonFeePaying,
        HasOverride = hasOverride
    };

    [Fact]
    public void Build_WritesExactHeaderRow()
    {
        var csv = InvoiceCsvBuilder.Build(new InvoicePendingData([], []));

        var firstLine = csv.Split(Environment.NewLine)[0];
        Assert.Equal(
            "Customer,Customer Number,Cost Centre Code,Contract Code,Special Instructions,Invoice Details,PO Number,Customer Type,Opt Out Of Invoice Generation,Non Fee Paying,Line Item,Description,Unit of Measure,Selling Price,Quantity,Total Amt,VAT Number,VAT Rating,Redistribution",
            firstLine);
    }

    [Fact]
    public void Build_ContractWithNoItems_IsSkippedEntirely()
    {
        var contractId = Guid.NewGuid();
        var csv = InvoiceCsvBuilder.Build(new InvoicePendingData([Contract(contractId)], []));

        var dataLines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Skip(1);
        Assert.Empty(dataLines);
    }

    [Fact]
    public void Build_ZeroPostageAndAdminCharge_OmitsPostageAndAdminChargeRows()
    {
        var contractId = Guid.NewGuid();
        var csv = InvoiceCsvBuilder.Build(new InvoicePendingData([Contract(contractId)], [Item(contractId)]));

        Assert.DoesNotContain("Combined Postage", csv);
        Assert.DoesNotContain("Admin charge", csv);
    }

    [Fact]
    public void Build_PositivePostageTotal_EmitsCombinedPostageRowWithoutRedistributionColumn()
    {
        var contractId = Guid.NewGuid();
        var contract = Contract(contractId, numberPostage: 2, postagePrice: 5m);
        var csv = InvoiceCsvBuilder.Build(new InvoicePendingData([contract], [Item(contractId)]));

        var line = csv.Split(Environment.NewLine).Single(l => l.Contains("Combined Postage"));
        Assert.Equal(18, line.Split("\",\"").Length);
    }

    [Fact]
    public void Build_PositiveAdministrationCharge_EmitsAdminChargeRow()
    {
        var contractId = Guid.NewGuid();
        var contract = Contract(contractId, administrationCharge: 50m);
        var csv = InvoiceCsvBuilder.Build(new InvoicePendingData([contract], [Item(contractId)]));

        Assert.Contains("Admin charge", csv);
    }

    [Fact]
    public void Build_ContractItemRow_AppliesDiscountRateAndIncludesRedistributionColumn()
    {
        var contractId = Guid.NewGuid();
        var contract = Contract(contractId, discountRate: 0.1m);
        var item = Item(contractId, price: 100m, hasOverride: true);
        var csv = InvoiceCsvBuilder.Build(new InvoicePendingData([contract], [item]));

        var line = csv.Split(Environment.NewLine).Single(l => l.Contains("PT0001 Scheme One"));
        Assert.Contains("90", line);
        Assert.Equal(19, line.Split("\",\"").Length);
        Assert.EndsWith("\"True\"", line);
    }

    [Fact]
    public void Build_OptOutAndNonFeePaying_AppearInRowsButAreNotFiltered()
    {
        var contractId = Guid.NewGuid();
        var contract = Contract(contractId, optOut: true);
        var item = Item(contractId, nonFeePaying: true);
        var csv = InvoiceCsvBuilder.Build(new InvoicePendingData([contract], [item]));

        var line = csv.Split(Environment.NewLine).Single(l => l.Contains("PT0001 Scheme One"));
        Assert.Contains("\"True\"", line);
    }

    [Fact]
    public void Build_UsesHardcodedCostCentreAndContractCodeLiterals()
    {
        var contractId = Guid.NewGuid();
        var csv = InvoiceCsvBuilder.Build(new InvoicePendingData([Contract(contractId)], [Item(contractId)]));

        Assert.Contains("\"35500\"", csv);
        Assert.Contains("\"CSUT1306\"", csv);
    }
}
