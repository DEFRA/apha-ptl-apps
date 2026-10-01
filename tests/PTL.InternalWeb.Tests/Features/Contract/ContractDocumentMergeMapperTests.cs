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
    public void JobSheet_WithKnownCustomer_PopulatesCustomerFieldsFromCustomerRecord()
    {
        var countryId = Guid.NewGuid();
        var vatRatingId = Guid.NewGuid();
        var customer = new PTL.Contracts.Customer.CustomerResponse(
            CustomerId: Guid.NewGuid(), QalNumber: "QAL/00001", RegisteredFileNumber: string.Empty, Name: "Sample Laboratories Ltd",
            PreviousName: string.Empty, CustomerTypeId: Guid.Empty, VatNumber: "GB123456789", VatRatingId: vatRatingId,
            AccountNumber: "ACC001", CustomerFinanceId: string.Empty, ContactName: "Alice Example", Organisation: "Sample Laboratories Ltd",
            Address1: "1 High Street", Address2: "Testville", Address3: string.Empty, Address4: string.Empty, Address5: string.Empty,
            CountryId: countryId, Telephone: "01234 567890", Telephone2: string.Empty, Fax: "01234 567891", Email: "alice@example.test",
            CurrencyId: Guid.Empty, Comments: string.Empty, InitialStartDate: DateTime.UtcNow, PostageArrangements: string.Empty,
            PaymentNonUK: false, InvoiceName: string.Empty, InvoiceOrganisation: string.Empty, InvoiceAddress1: string.Empty,
            InvoiceAddress2: string.Empty, InvoiceAddress3: string.Empty, InvoiceAddress4: string.Empty, InvoiceAddress5: string.Empty,
            InvoiceCountryId: countryId, InvoiceTelephone: string.Empty, InvoiceTelephone2: string.Empty, InvoiceFax: string.Empty,
            InvoiceEmail: string.Empty, IsActive: true, CanOrderOnline: true, InactiveDate: null, CustomerStatusId: null);

        var context = CreateContext(ContractDocumentTypes.JobSheet) with
        {
            Customer = customer,
            Countries = [new PTL.Contracts.Lookup.CountryResponse(countryId, "United Kingdom")],
            VatRatings = [new PTL.Contracts.Lookup.VatRatingResponse(vatRatingId, "Standard")]
        };

        var request = ContractDocumentMergeMapper.Build(context);

        Assert.Equal("Alice Example", request.MergeValues["ContactName"]);
        Assert.Equal("Sample Laboratories Ltd", request.MergeValues["Organisation"]);
        Assert.Equal("1 High Street", request.MergeValues["AddressLine1"]);
        Assert.Equal("United Kingdom", request.MergeValues["Country"]);
        Assert.Equal("01234 567890", request.MergeValues["Telephone"]);
        Assert.Equal("01234 567891", request.MergeValues["Fax"]);
        Assert.Equal("alice@example.test", request.MergeValues["Email"]);
        Assert.Equal("ACC001", request.MergeValues["AccountNumber"]);
        Assert.Equal("GB123456789", request.MergeValues["VatNumber"]);
        Assert.Equal("Standard", request.MergeValues["VatRating"]);
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

    [Fact]
    public void Contract_WithNoCommencementDate_LeavesCompletionDateEmpty()
    {
        var context = CreateContext(ContractDocumentTypes.Contract);
        context = context with { Contract = context.Contract with { CommencementDate = null } };

        var request = ContractDocumentMergeMapper.Build(context);

        Assert.Equal(string.Empty, request.MergeValues["CompletionDate"]);
    }

    [Fact]
    public void Contract_ResolvesCountryAndVatRatingNamesFromLookupLists()
    {
        var countryId = Guid.NewGuid();
        var invoiceCountryId = Guid.NewGuid();
        var vatRatingId = Guid.NewGuid();
        var customer = new PTL.Contracts.Customer.CustomerResponse(
            CustomerId: Guid.NewGuid(), QalNumber: "QAL/00001", RegisteredFileNumber: string.Empty, Name: "Sample Laboratories Ltd",
            PreviousName: string.Empty, CustomerTypeId: Guid.Empty, VatNumber: string.Empty, VatRatingId: vatRatingId,
            AccountNumber: string.Empty, CustomerFinanceId: string.Empty, ContactName: "Alice Example", Organisation: "Sample Laboratories Ltd",
            Address1: string.Empty, Address2: string.Empty, Address3: string.Empty, Address4: string.Empty, Address5: string.Empty,
            CountryId: countryId, Telephone: string.Empty, Telephone2: string.Empty, Fax: string.Empty, Email: string.Empty,
            CurrencyId: Guid.Empty, Comments: string.Empty, InitialStartDate: DateTime.UtcNow, PostageArrangements: string.Empty,
            PaymentNonUK: false, InvoiceName: string.Empty, InvoiceOrganisation: string.Empty, InvoiceAddress1: string.Empty,
            InvoiceAddress2: string.Empty, InvoiceAddress3: string.Empty, InvoiceAddress4: string.Empty, InvoiceAddress5: string.Empty,
            InvoiceCountryId: invoiceCountryId, InvoiceTelephone: string.Empty, InvoiceTelephone2: string.Empty, InvoiceFax: string.Empty,
            InvoiceEmail: string.Empty, IsActive: true, CanOrderOnline: true, InactiveDate: null, CustomerStatusId: null);
        var context = CreateContext(ContractDocumentTypes.Contract) with
        {
            Customer = customer,
            Countries = [new PTL.Contracts.Lookup.CountryResponse(countryId, "United Kingdom"), new PTL.Contracts.Lookup.CountryResponse(invoiceCountryId, "France")],
            VatRatings = [new PTL.Contracts.Lookup.VatRatingResponse(vatRatingId, "Standard")]
        };

        var request = ContractDocumentMergeMapper.Build(context);

        Assert.Equal("United Kingdom", request.MergeValues["Country"]);
        Assert.Equal("France", request.MergeValues["InvoiceCountry"]);
        Assert.Equal("Standard", request.MergeValues["VatRating"]);
    }

    [Fact]
    public void Build_UnsupportedDocumentType_ThrowsArgumentOutOfRangeException()
    {
        var context = CreateContext("UnsupportedType");

        Assert.Throws<ArgumentOutOfRangeException>(() => ContractDocumentMergeMapper.Build(context));
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
