using PTL.Core.Contract.Document;

namespace PTL.Api.Tests.Contract;

public class ContractDocumentTypesTests
{
    [Theory]
    [InlineData("Contract", ContractDocumentTypes.Contract)]
    [InlineData("contracts", ContractDocumentTypes.Contract)]
    [InlineData("Address Confirmation", ContractDocumentTypes.AddressConfirmation)]
    [InlineData("AddressConfirmation", ContractDocumentTypes.AddressConfirmation)]
    [InlineData("address-confirmation", ContractDocumentTypes.AddressConfirmation)]
    [InlineData("AddressConfirmationLetters", ContractDocumentTypes.AddressConfirmation)]
    [InlineData("Job Sheet", ContractDocumentTypes.JobSheet)]
    [InlineData("jobsheets", ContractDocumentTypes.JobSheet)]
    [InlineData("Renewal Letter", ContractDocumentTypes.RenewalLetter)]
    [InlineData("renewalletters", ContractDocumentTypes.RenewalLetter)]
    [InlineData("ContractRenewal", ContractDocumentTypes.RenewalLetter)]
    public void TryResolve_KnownAlias_ReturnsCanonicalName(string input, string expected)
    {
        var resolved = ContractDocumentTypes.TryResolve(input, out var canonical);

        Assert.True(resolved);
        Assert.Equal(expected, canonical);
    }

    // Punctuation, spacing and casing are all stripped before lookup.
    [Theory]
    [InlineData("  JOB   SHEET  ")]
    [InlineData("job_sheet")]
    [InlineData("Job.Sheet")]
    public void TryResolve_IgnoresCasingAndNonAlphanumerics(string input)
    {
        Assert.True(ContractDocumentTypes.TryResolve(input, out var canonical));
        Assert.Equal(ContractDocumentTypes.JobSheet, canonical);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Invoice")]
    [InlineData("!!!")]
    public void TryResolve_UnknownOrEmpty_ReturnsFalseAndEmptyName(string? input)
    {
        var resolved = ContractDocumentTypes.TryResolve(input, out var canonical);

        Assert.False(resolved);
        Assert.Equal(string.Empty, canonical);
    }

    [Fact]
    public void RegionNames_MatchTemplateMergeRegions()
    {
        Assert.Equal("ContractItems", ContractDocumentTypes.ContractItemsRegion);
        Assert.Equal("FeePayingSchemes", ContractDocumentTypes.FeePayingSchemesRegion);
        Assert.Equal("NonFeePayingSchemes", ContractDocumentTypes.NonFeePayingSchemesRegion);
    }
}
