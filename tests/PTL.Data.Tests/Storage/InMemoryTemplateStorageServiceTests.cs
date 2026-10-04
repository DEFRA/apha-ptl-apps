using System.Text;
using PTL.Data.Storage;

namespace PTL.Data.Tests.Storage;

public class InMemoryTemplateStorageServiceTests
{
    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);

    [Fact]
    public async Task SaveAsync_ThenGetAsync_RoundTripsContent()
    {
        var service = new InMemoryTemplateStorageService();

        await service.SaveAsync("templates/contract.docx", Bytes("doc"), "application/octet-stream");
        var stored = await service.GetAsync("templates/contract.docx");

        Assert.NotNull(stored);
        Assert.Equal("doc", Encoding.UTF8.GetString(stored!));
    }

    [Fact]
    public async Task SaveAsync_SameKeyTwice_OverwritesContent()
    {
        var service = new InMemoryTemplateStorageService();

        await service.SaveAsync("k", Bytes("first"), "application/octet-stream");
        await service.SaveAsync("k", Bytes("second"), "application/octet-stream");

        Assert.Equal("second", Encoding.UTF8.GetString((await service.GetAsync("k"))!));
    }

    [Fact]
    public async Task GetAsync_UnknownKey_ReturnsNull()
    {
        var service = new InMemoryTemplateStorageService();

        Assert.Null(await service.GetAsync("missing"));
    }

    [Fact]
    public async Task DeleteAsync_RemovesStoredContent()
    {
        var service = new InMemoryTemplateStorageService();
        await service.SaveAsync("k", Bytes("doc"), "application/octet-stream");

        await service.DeleteAsync("k");

        Assert.Null(await service.GetAsync("k"));
    }

    [Fact]
    public async Task DeleteAsync_UnknownKey_DoesNotThrow()
    {
        var service = new InMemoryTemplateStorageService();

        await service.DeleteAsync("missing");
    }

    [Fact]
    public async Task ListAsync_NoPrefix_ReturnsEveryKeySorted()
    {
        var service = new InMemoryTemplateStorageService();
        await service.SaveAsync("b", Bytes("1"), "application/octet-stream");
        await service.SaveAsync("a", Bytes("2"), "application/octet-stream");

        Assert.Equal(["a", "b"], await service.ListAsync());
    }

    [Fact]
    public async Task ListAsync_FiltersByPrefixIgnoringSurroundingSlashesAndCase()
    {
        var service = new InMemoryTemplateStorageService();
        await service.SaveAsync("templates/contract.docx", Bytes("1"), "application/octet-stream");
        await service.SaveAsync("invoices/inv.pdf", Bytes("2"), "application/octet-stream");

        Assert.Equal(["templates/contract.docx"], await service.ListAsync("/templates/"));
        Assert.Equal(["templates/contract.docx"], await service.ListAsync("TEMPLATES"));
    }

    [Fact]
    public async Task ListAsync_EmptyStore_ReturnsEmpty()
    {
        var service = new InMemoryTemplateStorageService();

        Assert.Empty(await service.ListAsync("templates"));
    }
}
