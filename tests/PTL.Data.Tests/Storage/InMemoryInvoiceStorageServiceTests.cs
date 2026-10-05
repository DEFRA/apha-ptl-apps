using PTL.Data.Storage;

namespace PTL.Data.Tests.Storage;

public class InMemoryInvoiceStorageServiceTests
{
    [Fact]
    public async Task SaveAsync_ThenGetAsync_ReturnsStoredContent()
    {
        var service = new InMemoryInvoiceStorageService();

        await service.SaveAsync("invoices/PT_Invoices_2026-01-01.csv", [1, 2, 3], "text/csv");
        var result = await service.GetAsync("invoices/PT_Invoices_2026-01-01.csv");

        Assert.Equal(new byte[] { 1, 2, 3 }, result);
    }

    [Fact]
    public async Task GetAsync_MissingKey_ReturnsNull()
    {
        var service = new InMemoryInvoiceStorageService();

        var result = await service.GetAsync("missing.csv");

        Assert.Null(result);
    }

    [Fact]
    public async Task ListAsync_ReturnsKeysMatchingPrefixInOrdinalOrder()
    {
        var service = new InMemoryInvoiceStorageService();
        await service.SaveAsync("invoices/b.csv", [1], "text/csv");
        await service.SaveAsync("invoices/a.csv", [2], "text/csv");
        await service.SaveAsync("templates/other.csv", [3], "text/csv");

        var result = await service.ListAsync("invoices/");

        Assert.Equal(["invoices/a.csv", "invoices/b.csv"], result);
    }
}
