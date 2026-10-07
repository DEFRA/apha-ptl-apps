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
    public void InvoiceDetails_OnlyAddress1_ReturnsAddress1Only()
    {
        var contract = new InvoiceContractEntity { InvoiceAddress1 = "1 High Street" };

        Assert.Equal("1 High Street", contract.InvoiceDetails);
    }

    [Fact]
    public void InvoiceDetails_Address1And2Only_StopsBeforeAddress3()
    {
        var contract = new InvoiceContractEntity { InvoiceAddress1 = "1 High Street", InvoiceAddress2 = "Town" };

        Assert.Equal("1 High Street, Town", contract.InvoiceDetails);
    }

    [Fact]
    public void InvoiceDetails_Address1To3Only_StopsBeforeAddress4()
    {
        var contract = new InvoiceContractEntity { InvoiceAddress1 = "1 High Street", InvoiceAddress2 = "Town", InvoiceAddress3 = "County" };

        Assert.Equal("1 High Street, Town, County", contract.InvoiceDetails);
    }

    [Fact]
    public void InvoiceDetails_Address1To4Only_StopsBeforeAddress5()
    {
        var contract = new InvoiceContractEntity
        {
            InvoiceAddress1 = "1 High Street",
            InvoiceAddress2 = "Town",
            InvoiceAddress3 = "County",
            InvoiceAddress4 = "Region"
        };

        Assert.Equal("1 High Street, Town, County, Region", contract.InvoiceDetails);
    }

    [Fact]
    public void InvoiceDetails_NoCountry_OmitsCountrySuffix()
    {
        var contract = new InvoiceContractEntity { InvoiceAddress1 = "1 High Street", InvoiceAddress2 = "Town" };

        Assert.DoesNotContain("United Kingdom", contract.InvoiceDetails);
    }

    [Fact]
    public void CombinedPostagePriceTotal_SumsAllThreePostageTypes()
    {
        var contract = new InvoiceContractEntity
        {
            NumberPostage = 2,
            PostagePrice = 1.5m,
            NumberCourier = 1,
            CourierPrice = 5m,
            NumberSpecialDelivery = 3,
            SpecialDeliveryPrice = 2m
        };

        Assert.Equal(14m, contract.CombinedPostagePriceTotal);
    }

    [Fact]
    public void SpecialInstructions_ConcatenatesQalNumberAndSuffixWithoutSeparators()
    {
        var contract = new InvoiceContractEntity { QalNumber = "QAL/00001", Suffix = "A" };

        Assert.Equal("Provision of PT Services Contract Ref No.QAL/00001A", contract.SpecialInstructions);
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
