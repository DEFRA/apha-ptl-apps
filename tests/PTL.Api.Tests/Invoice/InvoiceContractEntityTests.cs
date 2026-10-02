using PTL.Core.Invoice;

namespace PTL.Api.Tests.Invoice;

// CustomerId/YearId and the InvoiceDetails nested-address-line logic aren't touched by
// InvoiceServiceTests/InvoiceCsvBuilderTests, so cover them directly here (these entities live in
// PTL.Core, which PTL.Api.Tests - not PTL.Data.Tests, which excludes PTL.Core - measures).
public class InvoiceContractEntityTests
{
    [Fact]
    public void InvoiceDetails_AllAddressLinesAndCountryPopulated_JoinsEveryLine()
    {
        var contract = new InvoiceContractEntity
        {
            CustomerId = Guid.NewGuid(),
            YearId = 2026,
            InvoiceAddress1 = "1 High Street",
            InvoiceAddress2 = "Town",
            InvoiceAddress3 = "County",
            InvoiceAddress4 = "Region",
            InvoiceAddress5 = "Postcode",
            InvoiceCountry = "United Kingdom"
        };

        Assert.Equal("1 High Street, Town, County, Region, Postcode, United Kingdom", contract.InvoiceDetails);
    }

    [Fact]
    public void CustomerId_AndYearId_RoundTrip()
    {
        var customerId = Guid.NewGuid();
        var contract = new InvoiceContractEntity { CustomerId = customerId, YearId = 2026 };

        Assert.Equal(customerId, contract.CustomerId);
        Assert.Equal(2026, contract.YearId);
    }

    [Fact]
    public void ParticipantSchemeId_RoundTrips()
    {
        var participantSchemeId = Guid.NewGuid();
        var item = new InvoiceContractItemEntity { ParticipantSchemeId = participantSchemeId };

        Assert.Equal(participantSchemeId, item.ParticipantSchemeId);
    }
}
