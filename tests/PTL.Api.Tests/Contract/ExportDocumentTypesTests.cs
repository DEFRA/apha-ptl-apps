using PTL.Core.Contract.Export.Templates;

namespace PTL.Api.Tests.Contract;

public class ExportDocumentTypesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryResolve_NullOrWhitespace_ReturnsFalse(string? input)
    {
        Assert.False(ExportDocumentTypes.TryResolve(input, out var storageName, out var displayName));
        Assert.Equal(string.Empty, storageName);
        Assert.Equal(string.Empty, displayName);
    }

    [Fact]
    public void TryResolve_UnknownAlias_ReturnsFalse() =>
        Assert.False(ExportDocumentTypes.TryResolve("not-a-real-type", out _, out _));

    [Fact]
    public void TryResolve_KnownAlias_ReturnsStorageAndDisplayName()
    {
        var resolved = ExportDocumentTypes.TryResolve("contract", out var storageName, out var displayName);

        Assert.True(resolved);
        Assert.Equal(ExportDocumentTypes.Contracts, storageName);
        Assert.Equal("Export Contracts", displayName);
    }

    [Fact]
    public void DisplayNameFor_KnownAlias_ReturnsTheFriendlyName() =>
        Assert.Equal("Export Job Sheets", ExportDocumentTypes.DisplayNameFor("jobsheets"));

    [Fact]
    public void DisplayNameFor_UnknownAlias_ReturnsTheInputUnchanged() =>
        Assert.Equal("Invoices", ExportDocumentTypes.DisplayNameFor("Invoices"));
}
