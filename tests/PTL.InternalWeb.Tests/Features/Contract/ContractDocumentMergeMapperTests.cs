using PTL.Contracts.Contract;
using PTL.Core.Contract.Document;
using PTL.InternalWeb.Features.Contract;

namespace PTL.InternalWeb.Tests.Features.Contract;

public class ContractDocumentMergeMapperTests
{
    [Fact]
    public void AddressConfirmation_EmitsOneLetterPerSampleAddressWithBothSchemeRegions()
    {
        var context = CreateContext(
            ContractDocumentTypes.AddressConfirmation,
            sampleAddresses:
            [
                SampleAddress("LAB1", feePaying: ["Salmonella"], nonFeePaying: ["Listeria"]),
                SampleAddress("LAB2", feePaying: ["Campylobacter"], nonFeePaying: [])
            ]);

        var request = ContractDocumentMergeMapper.Build(context);

        Assert.Equal("LAB1", request.MergeValues["LabCode"]);
        Assert.Equal("Salmonella", Assert.Single(request.Regions![ContractDocumentTypes.FeePayingSchemesRegion])["SchemeName"]);
        Assert.Equal("Listeria", Assert.Single(request.Regions[ContractDocumentTypes.NonFeePayingSchemesRegion])["SchemeName"]);

        var second = Assert.Single(request.AdditionalDocuments!);
        Assert.Equal("LAB2", second.MergeValues["LabCode"]);
        Assert.Empty(second.Regions![ContractDocumentTypes.NonFeePayingSchemesRegion]);
    }

    [Fact]
    public void AddressConfirmation_WithNoAddresses_ProducesNoAdditionalDocuments()
    {
        var request = ContractDocumentMergeMapper.Build(CreateContext(ContractDocumentTypes.AddressConfirmation));

        Assert.Empty(request.MergeValues);
        Assert.Empty(request.AdditionalDocuments!);
    }

    [Fact]
    public void RenewalLetter_UsesLongDateFormatForContractDates()
    {
        var context = CreateContext(
            ContractDocumentTypes.RenewalLetter,
            renewal: new ContractRenewalResponse(
                Guid.NewGuid(), Guid.NewGuid(), "QAL/00001", "Sample Laboratories Ltd", "Alice Example",
                "1 High Street", "Testville", string.Empty, string.Empty, string.Empty, "United Kingdom",
                new DateTime(2024, 4, 1), new DateTime(2025, 3, 31), "Renew soon."));

        var request = ContractDocumentMergeMapper.Build(context);

        Assert.Equal("QAL/00001", request.MergeValues["QalNumber"]);
        Assert.Equal(new DateTime(2024, 4, 1).ToLongDateString(), request.MergeValues["CurrentContractStartDate"]);
        Assert.Equal(new DateTime(2025, 3, 31).ToLongDateString(), request.MergeValues["CurrentContractEndDate"]);
        Assert.Equal("Renew soon.", request.MergeValues["AdditionalLetterInfo"]);
    }

    [Fact]
    public void RenewalLetter_WithNoRenewalData_LeavesFieldsBlankRatherThanThrowing()
    {
        var request = ContractDocumentMergeMapper.Build(CreateContext(ContractDocumentTypes.RenewalLetter));

        Assert.Equal(string.Empty, request.MergeValues["QalNumber"]);
        Assert.Equal(string.Empty, request.MergeValues["CurrentContractStartDate"]);
    }

    [Fact]
    public void JobSheet_ExposesBothPrefixedAndUnprefixedAliases()
    {
        var contractId = Guid.NewGuid();
        var context = CreateContext(
            ContractDocumentTypes.JobSheet,
            items: new ContractItemsResponse(
                contractId, "A", 2024, "QAL/00001", "\u00a3", 0.1m, 25m,
                2, 5m, 10m, 3, 4m, 12m, 1, 6m, 6m, 9.5m, 85m, 95.5m, false,
                [
                    new ContractItemSchemeResponse(Guid.NewGuid(), "S1", "Salmonella",
                    [
                        new ContractItemResponse(Guid.NewGuid(), Guid.NewGuid(), "LAB1", "Lab One", "Lab One Ltd", 4, 42.5m, false, false)
                    ])
                ]));

        var request = ContractDocumentMergeMapper.Build(context);

        Assert.Equal(request.MergeValues["Organisation"], request.MergeValues["CustomerOrganisation"]);
        Assert.Equal("QAL/00001", request.MergeValues["QalNumber"]);
        Assert.Equal("Salmonella", Assert.Single(request.Regions![ContractDocumentTypes.ContractItemsRegion])["SchemeName"]);
    }

    [Fact]
    public void Contract_DerivesCompletionDateAsEndOfFinancialYear()
    {
        var context = CreateContext(ContractDocumentTypes.Contract, commencementDate: new DateTime(2024, 6, 1));

        var request = ContractDocumentMergeMapper.Build(context);

        Assert.Equal("31/03/2025", request.MergeValues["CompletionDate"]);
    }

    [Fact]
    public void Contract_CommencingBeforeAprilCompletesInTheSameCalendarYear()
    {
        var context = CreateContext(ContractDocumentTypes.Contract, commencementDate: new DateTime(2024, 2, 1));

        var request = ContractDocumentMergeMapper.Build(context);

        Assert.Equal("31/03/2024", request.MergeValues["CompletionDate"]);
    }

    private static SampleAddressResponse SampleAddress(string labCode, string[] feePaying, string[] nonFeePaying) => new(
        Guid.NewGuid(), Guid.NewGuid(), "QAL/00001", labCode, "Alice Example", "Sample Laboratories Ltd",
        "1 High Street", "Testville", string.Empty, string.Empty, string.Empty, "United Kingdom",
        "01234 567890", string.Empty, "alice@example.test", string.Empty, string.Empty, "Standard", string.Empty,
        [.. feePaying.Select(Scheme)],
        [.. nonFeePaying.Select(Scheme)]);

    private static SampleAddressSchemeResponse Scheme(string name) =>
        new(Guid.NewGuid(), name, "S1", "Jan, Feb", 12);

    private static ContractDocumentContext CreateContext(
        string documentType,
        ContractItemsResponse? items = null,
        IReadOnlyList<SampleAddressResponse>? sampleAddresses = null,
        ContractRenewalResponse? renewal = null,
        DateTime? commencementDate = null)
    {
        var contractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var contract = new ContractResponse(
            contractId, customerId, "Sample Laboratories Ltd", "QAL/00001", 2024, "UT3/306",
            string.Empty, "Alice Example", string.Empty, string.Empty, 0.1m, 25m, 2, 5m, 3, 4m, 1, 6m,
            null, null, null, string.Empty, null, true, false, "A",
            commencementDate ?? new DateTime(2024, 6, 1), string.Empty, false, false, false, null, null);

        return new ContractDocumentContext(
            documentType,
            documentType,
            contract,
            items,
            null,
            [],
            [],
            sampleAddresses ?? [],
            renewal);
    }
}
