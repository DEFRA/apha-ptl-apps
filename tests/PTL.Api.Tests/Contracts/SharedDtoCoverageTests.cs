using PTL.Contracts.AdministrationCharge;
using PTL.Contracts.Contract;
using PTL.Contracts.Customer;
using PTL.Contracts.Participant;
using PTL.Contracts.Scheme;

namespace PTL.Api.Tests.Contracts;

// Reads every property getter on the wire DTOs that no other test fully consumes. Coverlet only
// counts a record property as covered when its getter actually runs, so constructing a DTO (or
// deserializing one) is not enough on its own - see the DTO coverage tests under Contract/ and
// Participant/ for the same pattern.
public class SharedDtoCoverageTests
{
    [Fact]
    public void CustomerResponse_ExposesEveryProperty()
    {
        var customerId = Guid.NewGuid();
        var customerTypeId = Guid.NewGuid();
        var vatRatingId = Guid.NewGuid();
        var countryId = Guid.NewGuid();
        var currencyId = Guid.NewGuid();
        var invoiceCountryId = Guid.NewGuid();
        var customerStatusId = Guid.NewGuid();
        var initialStartDate = new DateTime(2024, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var inactiveDate = new DateTime(2025, 3, 31, 0, 0, 0, DateTimeKind.Utc);

        var response = new CustomerResponse(
            CustomerId: customerId,
            QalNumber: "QAL/00001",
            RegisteredFileNumber: "RF/123",
            Name: "Sample Laboratories Ltd",
            PreviousName: "Old Labs Ltd",
            CustomerTypeId: customerTypeId,
            VatNumber: "GB123456789",
            VatRatingId: vatRatingId,
            AccountNumber: "ACC-1",
            CustomerFinanceId: "FIN-1",
            ContactName: "Alice Example",
            Organisation: "Sample Organisation",
            Address1: "1 Test Street",
            Address2: "Testville",
            Address3: "Test District",
            Address4: "Test County",
            Address5: "TE1 1ST",
            CountryId: countryId,
            Telephone: "01234 567890",
            Telephone2: "01234 567891",
            Fax: "01234 567892",
            Email: "alice@example.com",
            CurrencyId: currencyId,
            Comments: "Some comments",
            InitialStartDate: initialStartDate,
            PostageArrangements: "Courier",
            PaymentNonUK: true,
            InvoiceName: "Invoice Contact",
            InvoiceOrganisation: "Invoice Organisation",
            InvoiceAddress1: "2 Invoice Street",
            InvoiceAddress2: "Invoiceville",
            InvoiceAddress3: "Invoice District",
            InvoiceAddress4: "Invoice County",
            InvoiceAddress5: "IN1 1CE",
            InvoiceCountryId: invoiceCountryId,
            InvoiceTelephone: "01234 111111",
            InvoiceTelephone2: "01234 222222",
            InvoiceFax: "01234 333333",
            InvoiceEmail: "invoices@example.com",
            IsActive: true,
            CanOrderOnline: true,
            InactiveDate: inactiveDate,
            CustomerStatusId: customerStatusId);

        Assert.Equal(customerId, response.CustomerId);
        Assert.Equal("QAL/00001", response.QalNumber);
        Assert.Equal("RF/123", response.RegisteredFileNumber);
        Assert.Equal("Sample Laboratories Ltd", response.Name);
        Assert.Equal("Old Labs Ltd", response.PreviousName);
        Assert.Equal(customerTypeId, response.CustomerTypeId);
        Assert.Equal("GB123456789", response.VatNumber);
        Assert.Equal(vatRatingId, response.VatRatingId);
        Assert.Equal("ACC-1", response.AccountNumber);
        Assert.Equal("FIN-1", response.CustomerFinanceId);
        Assert.Equal("Alice Example", response.ContactName);
        Assert.Equal("Sample Organisation", response.Organisation);
        Assert.Equal("1 Test Street", response.Address1);
        Assert.Equal("Testville", response.Address2);
        Assert.Equal("Test District", response.Address3);
        Assert.Equal("Test County", response.Address4);
        Assert.Equal("TE1 1ST", response.Address5);
        Assert.Equal(countryId, response.CountryId);
        Assert.Equal("01234 567890", response.Telephone);
        Assert.Equal("01234 567891", response.Telephone2);
        Assert.Equal("01234 567892", response.Fax);
        Assert.Equal("alice@example.com", response.Email);
        Assert.Equal(currencyId, response.CurrencyId);
        Assert.Equal("Some comments", response.Comments);
        Assert.Equal(initialStartDate, response.InitialStartDate);
        Assert.Equal("Courier", response.PostageArrangements);
        Assert.True(response.PaymentNonUK);
        Assert.Equal("Invoice Contact", response.InvoiceName);
        Assert.Equal("Invoice Organisation", response.InvoiceOrganisation);
        Assert.Equal("2 Invoice Street", response.InvoiceAddress1);
        Assert.Equal("Invoiceville", response.InvoiceAddress2);
        Assert.Equal("Invoice District", response.InvoiceAddress3);
        Assert.Equal("Invoice County", response.InvoiceAddress4);
        Assert.Equal("IN1 1CE", response.InvoiceAddress5);
        Assert.Equal(invoiceCountryId, response.InvoiceCountryId);
        Assert.Equal("01234 111111", response.InvoiceTelephone);
        Assert.Equal("01234 222222", response.InvoiceTelephone2);
        Assert.Equal("01234 333333", response.InvoiceFax);
        Assert.Equal("invoices@example.com", response.InvoiceEmail);
        Assert.True(response.IsActive);
        Assert.True(response.CanOrderOnline);
        Assert.Equal(inactiveDate, response.InactiveDate);
        Assert.Equal(customerStatusId, response.CustomerStatusId);
    }

    [Fact]
    public void ParticipantResponse_ExposesEveryProperty()
    {
        var participantId = Guid.NewGuid();
        var ssoId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var labTypeId = Guid.NewGuid();
        var countryId = Guid.NewGuid();
        var inactiveDate = new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var inactiveErrorDate = new DateTime(2025, 1, 3, 0, 0, 0, DateTimeKind.Utc);

        var response = new ParticipantResponse(
            ParticipantId: participantId,
            SsoId: ssoId,
            CustomerId: customerId,
            LabCode: "LAB1",
            LabName: "Lab One",
            LabTypeId: labTypeId,
            ContactName: "Bob Example",
            Organisation: "Lab One Ltd",
            Address1: "3 Lab Street",
            Address2: "Labville",
            Address3: "Lab District",
            Address4: "Lab County",
            Address5: "LA1 1AB",
            CountryId: countryId,
            Telephone: "01234 444444",
            Fax: "01234 555555",
            Email: "bob@example.com",
            Email2: "bob.alt@example.com",
            Comments: "Lab comments",
            IsActive: true,
            InactiveDate: inactiveDate,
            InactiveError: true,
            InactiveErrorDate: inactiveErrorDate);

        Assert.Equal(participantId, response.ParticipantId);
        Assert.Equal(ssoId, response.SsoId);
        Assert.Equal(customerId, response.CustomerId);
        Assert.Equal("LAB1", response.LabCode);
        Assert.Equal("Lab One", response.LabName);
        Assert.Equal(labTypeId, response.LabTypeId);
        Assert.Equal("Bob Example", response.ContactName);
        Assert.Equal("Lab One Ltd", response.Organisation);
        Assert.Equal("3 Lab Street", response.Address1);
        Assert.Equal("Labville", response.Address2);
        Assert.Equal("Lab District", response.Address3);
        Assert.Equal("Lab County", response.Address4);
        Assert.Equal("LA1 1AB", response.Address5);
        Assert.Equal(countryId, response.CountryId);
        Assert.Equal("01234 444444", response.Telephone);
        Assert.Equal("01234 555555", response.Fax);
        Assert.Equal("bob@example.com", response.Email);
        Assert.Equal("bob.alt@example.com", response.Email2);
        Assert.Equal("Lab comments", response.Comments);
        Assert.True(response.IsActive);
        Assert.Equal(inactiveDate, response.InactiveDate);
        Assert.True(response.InactiveError);
        Assert.Equal(inactiveErrorDate, response.InactiveErrorDate);
    }

    [Fact]
    public void ParticipantSummaryAndSearchDtos_ExposeEveryProperty()
    {
        var participantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var summary = new ParticipantSummaryResponse(participantId, customerId, "LAB1", "Lab One", "Bob Example", IsActive: true);

        Assert.Equal(participantId, summary.ParticipantId);
        Assert.Equal(customerId, summary.CustomerId);
        Assert.Equal("LAB1", summary.LabCode);
        Assert.Equal("Lab One", summary.LabName);
        Assert.Equal("Bob Example", summary.ContactName);
        Assert.True(summary.IsActive);

        var request = new ParticipantSearchRequest(customerId, "lab", IncludeInactive: true, Page: 2, PageSize: 50);

        Assert.Equal(customerId, request.CustomerId);
        Assert.Equal("lab", request.SearchTerm);
        Assert.True(request.IncludeInactive);
        Assert.Equal(2, request.Page);
        Assert.Equal(50, request.PageSize);

        var defaults = new ParticipantSearchRequest(customerId);

        Assert.Null(defaults.SearchTerm);
        Assert.False(defaults.IncludeInactive);
        Assert.Equal(1, defaults.Page);
        Assert.Equal(20, defaults.PageSize);

        var searchResponse = new ParticipantSearchResponse([summary], TotalCount: 1, Page: 2, PageSize: 50);

        Assert.Same(summary, Assert.Single(searchResponse.Items));
        Assert.Equal(1, searchResponse.TotalCount);
        Assert.Equal(2, searchResponse.Page);
        Assert.Equal(50, searchResponse.PageSize);
    }

    [Fact]
    public void SchemeSummaryAndHistoryDtos_ExposeEveryProperty()
    {
        var sharedId = Guid.NewGuid();
        var currentSchemeId = Guid.NewGuid();
        var nextSchemeId = Guid.NewGuid();
        var recentSchemeId = Guid.NewGuid();

        var summary = new SchemeSummaryResponse(
            sharedId, 2026,
            currentSchemeId, "S1", "Current Scheme",
            nextSchemeId, "S2", "Next Scheme",
            recentSchemeId, "S0", "Recent Scheme");

        Assert.Equal(sharedId, summary.SharedId);
        Assert.Equal(2026, summary.YearId);
        Assert.Equal(currentSchemeId, summary.CurrentSchemeId);
        Assert.Equal("S1", summary.CurrentIdentifier);
        Assert.Equal("Current Scheme", summary.CurrentName);
        Assert.Equal(nextSchemeId, summary.NextSchemeId);
        Assert.Equal("S2", summary.NextIdentifier);
        Assert.Equal("Next Scheme", summary.NextName);
        Assert.Equal(recentSchemeId, summary.RecentSchemeId);
        Assert.Equal("S0", summary.RecentIdentifier);
        Assert.Equal("Recent Scheme", summary.RecentName);

        var schemeId = Guid.NewGuid();
        var history = new SchemeHistoryResponse(schemeId, sharedId, 2025, "S1", "Historic Scheme");

        Assert.Equal(schemeId, history.SchemeId);
        Assert.Equal(sharedId, history.SharedId);
        Assert.Equal(2025, history.YearId);
        Assert.Equal("S1", history.Identifier);
        Assert.Equal("Historic Scheme", history.Name);

        var searchRequest = new SchemeSearchRequest(2026, "salmonella", Page: 3, PageSize: 15);

        Assert.Equal(2026, searchRequest.YearId);
        Assert.Equal("salmonella", searchRequest.SearchTerm);
        Assert.Equal(3, searchRequest.Page);
        Assert.Equal(15, searchRequest.PageSize);
    }

    [Fact]
    public void SaveResultDtos_ExposeEveryProperty()
    {
        var fieldErrors = new Dictionary<string, string[]> { ["UTNumber"] = ["Enter a UT number"] };

        var contractSaveResult = new ContractSaveResult(false, null, fieldErrors);

        Assert.False(contractSaveResult.Success);
        Assert.Null(contractSaveResult.Contract);
        Assert.Same(fieldErrors, contractSaveResult.FieldErrors);

        var schemeSaveResult = new SchemeSaveResult(false, null, fieldErrors);

        Assert.False(schemeSaveResult.Success);
        Assert.Null(schemeSaveResult.Scheme);
        Assert.Same(fieldErrors, schemeSaveResult.FieldErrors);

        var price = new AdministrationChargeCurrencyPriceResponse(Guid.NewGuid(), 12.5m);
        var chargeSaveResult = new AdministrationChargeSaveResult(true, price, new Dictionary<string, string[]>());

        Assert.True(chargeSaveResult.Success);
        Assert.Same(price, chargeSaveResult.Price);
        Assert.Empty(chargeSaveResult.FieldErrors);
    }
}
