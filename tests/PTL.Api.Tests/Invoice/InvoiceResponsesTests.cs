using PTL.Contracts.Invoice;

namespace PTL.Api.Tests.Invoice;

public class InvoiceResponsesTests
{
    [Fact]
    public void InvoiceCsvDownloadResponse_EqualValues_AreEqual()
    {
        var a = new InvoiceCsvDownloadResponse("PT_Invoices_2026-01-01.csv", [1, 2, 3]);
        var b = new InvoiceCsvDownloadResponse("PT_Invoices_2026-01-01.csv", [1, 2, 3]);

        Assert.Equal(a.FileName, b.FileName);
        Assert.Equal(a with { }, a);
    }
}
