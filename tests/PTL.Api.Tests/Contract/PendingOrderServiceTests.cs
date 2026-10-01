using PTL.Api.Tests.Customer;
using PTL.Api.Tests.Lookup;
using PTL.Api.Tests.Scheme;
using PTL.Core.Contract.PendingOrder;
using PTL.Core.Lookup;
using CoreScheme = PTL.Core.Scheme.Scheme;

namespace PTL.Api.Tests.Contract;

public class PendingOrderServiceTests
{
    private const int CurrentYearId = 2026;
    private const int NextYearId = 2027;
    private static readonly Guid CourierPostageId = Guid.NewGuid();
    private static readonly Guid UkCountryId = Guid.NewGuid();

    private sealed record Harness(
        PendingOrderService Service,
        FakePendingOrderRepository PendingOrders,
        FakeContractRepository Contracts,
        FakeParticipantSchemeRepository ParticipantSchemes,
        FakeSchemeRepository Schemes,
        Guid CustomerId,
        Guid PendingContractId,
        Guid SchemeId);

    private static async Task<Harness> CreateAsync(params PendingOrderSchemeEntity[] schemes)
    {
        var customerRepository = new FakeCustomerRepository();
        var customer = await customerRepository.CreateAsync(new PTL.Core.Customer.Customer
        {
            CustomerId = Guid.NewGuid(),
            Name = "Sample Laboratories Ltd",
            CountryId = UkCountryId,
            IsActive = true
        });

        var schemeRepository = new FakeSchemeRepository();
        var realScheme = new CoreScheme
        {
            SchemeId = Guid.NewGuid(),
            YearId = CurrentYearId,
            Identifier = "S1",
            Name = "Sample Scheme",
            Postage = CourierPostageId,
            CombinedPackaging = false,
            DayOfWeekId = Guid.NewGuid(),
            WeekNumber = 1
        };
        await schemeRepository.CreateAsync(realScheme);

        var pendingContractId = Guid.NewGuid();
        var lines = schemes.Length > 0
            ? schemes
            : [new PendingOrderSchemeEntity
            {
                PendingParticipantSchemeId = Guid.NewGuid(),
                PendingContractId = pendingContractId,
                ParticipantId = Guid.NewGuid(),
                ParticipantName = "001: Alpha Lab",
                SchemeId = realScheme.SchemeId,
                SchemeIdentifier = "S1",
                SchemeName = "Sample Scheme",
                DistributionMonthApr = true,
                DistributionMonthMay = true,
                Price = 100m
            }];

        foreach (var line in lines)
        {
            line.PendingContractId = pendingContractId;
            if (line.SchemeId == Guid.Empty)
            {
                line.SchemeId = realScheme.SchemeId;
            }
        }

        var pendingOrders = new FakePendingOrderRepository();
        pendingOrders.Seed(
            new PendingOrderEntity
            {
                PendingContractId = pendingContractId,
                CustomerId = customer.CustomerId,
                YearId = CurrentYearId,
                IsSubmitted = true,
                PurchaseOrderNumber = "PO-1",
                CustomerName = customer.Name,
                CurrencySymbol = "£"
            },
            new PendingOrderSummaryEntity
            {
                PendingContractId = pendingContractId,
                CustomerId = customer.CustomerId,
                YearId = CurrentYearId,
                IsSubmitted = true,
                QalNumber = customer.QalNumber,
                CustomerName = customer.Name,
                OrderSubmitDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            lines);

        var lookupRepository = new FakeLookupRepository
        {
            SystemSettings = new SystemSettingsEntity { CurrentYearId = CurrentYearId, NextYearId = NextYearId },
            Countries = [new CountryEntity { CountryId = UkCountryId, Country = "United Kingdom", CountryType = "UK" }],
            AllYears = [new YearEntity { YearId = CurrentYearId, Year = "2026/27" }, new YearEntity { YearId = NextYearId, Year = "2027/28" }],
            PostagePricingPlans = [new PostagePricingPlanEntity { PostageId = CourierPostageId, Name = "Courier", UKPrice = 10m, EUPrice = 20m, NonEUPrice = 30m, YearId = CurrentYearId }]
        };

        var contracts = new FakeContractRepository();
        var participantSchemes = new FakeParticipantSchemeRepository();

        var service = new PendingOrderService(pendingOrders, contracts, participantSchemes, schemeRepository, customerRepository, lookupRepository);

        return new Harness(service, pendingOrders, contracts, participantSchemes, schemeRepository, customer.CustomerId, pendingContractId, realScheme.SchemeId);
    }

    [Fact]
    public async Task GetPendingOrdersAsync_SplitsOutstandingOrdersByContractYear()
    {
        var harness = await CreateAsync();

        var (currentYear, nextYear) = await harness.Service.GetPendingOrdersAsync();

        Assert.Single(currentYear);
        Assert.Empty(nextYear);
        Assert.Equal("2026/27", currentYear[0].Year);
    }

    [Fact]
    public async Task GetPendingOrderAsync_UnknownOrder_ReturnsNull()
    {
        var harness = await CreateAsync();

        Assert.Null(await harness.Service.GetPendingOrderAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetPendingOrderAsync_PricesEveryLineAndTotalsTheOrder()
    {
        var harness = await CreateAsync();

        var detail = await harness.Service.GetPendingOrderAsync(harness.PendingContractId);

        Assert.NotNull(detail);
        var line = Assert.Single(detail!.Schemes);

        // Courier at the UK price, charged for both selected months.
        Assert.Equal(20m, line.PostagePrice);
        Assert.Equal(120m, line.TotalPrice);
        Assert.Equal(100m, detail.TotalSchemePrice);
        Assert.Equal(20m, detail.TotalPostagePrice);
        Assert.Equal(120m, detail.Total);
    }

    [Fact]
    public async Task GetPendingOrderAsync_ReturnsMonthsInFinancialYearOrder()
    {
        var harness = await CreateAsync();

        var detail = await harness.Service.GetPendingOrderAsync(harness.PendingContractId);

        var line = Assert.Single(detail!.Schemes);
        Assert.Equal(
            ["Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec", "Jan", "Feb", "Mar"],
            line.Months.Select(m => m.Label));
    }

    // Legacy: chk.Enabled = scheme.DistributionMonthX And scheme.CanEditX And scheme.CanEditXParticipants.
    [Fact]
    public async Task GetPendingOrderAsync_EnablesMonthOnlyWhenSchemeOffersItAndNothingIsLocked()
    {
        var harness = await CreateAsync();
        var scheme = await harness.Schemes.GetByIdAsync(harness.SchemeId);
        scheme!.DistributionMonthApr = true;   // offered and fully editable
        scheme.DistributionMonthMay = true;    // offered but the year's distribution is defined
        scheme.CanEditMay = false;
        scheme.DistributionMonthJun = true;    // offered but a distribution has been posted
        scheme.DistributionMonthJul = false;   // not offered at all
        await harness.Schemes.UpdateAsync(scheme);

        var lines = await harness.PendingOrders.GetSchemesAsync(harness.PendingContractId);
        lines[0].CanEditJun = false;

        var detail = await harness.Service.GetPendingOrderAsync(harness.PendingContractId);
        var months = Assert.Single(detail!.Schemes).Months.ToDictionary(m => m.Label);

        Assert.True(months["Apr"].Enabled);
        Assert.False(months["May"].Enabled);
        Assert.False(months["Jun"].Enabled);
        Assert.False(months["Jul"].Enabled);
    }

    // Legacy: Checked = If(Enabled, pendingScheme.DistributionMonthX, scheme.DistributionMonthX).
    [Fact]
    public async Task GetPendingOrderAsync_LockedMonthFallsBackToTheSchemesOwnValue()
    {
        var harness = await CreateAsync();
        var scheme = await harness.Schemes.GetByIdAsync(harness.SchemeId);
        scheme!.DistributionMonthApr = true;
        scheme.DistributionMonthMay = true;
        scheme.CanEditMay = false;
        await harness.Schemes.UpdateAsync(scheme);

        var lines = await harness.PendingOrders.GetSchemesAsync(harness.PendingContractId);
        lines[0].DistributionMonthApr = false;
        lines[0].DistributionMonthMay = false;

        var detail = await harness.Service.GetPendingOrderAsync(harness.PendingContractId);
        var months = Assert.Single(detail!.Schemes).Months.ToDictionary(m => m.Label);

        // Editable, so the participant's own choice wins.
        Assert.False(months["Apr"].Selected);

        // Locked, so the scheme's distribution month is shown instead.
        Assert.True(months["May"].Selected);
    }

    // Legacy FlagCheckboxAsAlreadyParticipating: ticked, locked and highlighted.
    [Fact]
    public async Task GetPendingOrderAsync_AlreadyContractedMonthIsTickedAndLocked()
    {
        var harness = await CreateAsync();
        var scheme = await harness.Schemes.GetByIdAsync(harness.SchemeId);
        scheme!.DistributionMonthApr = true;
        await harness.Schemes.UpdateAsync(scheme);

        var lines = await harness.PendingOrders.GetSchemesAsync(harness.PendingContractId);
        lines[0].DistributionMonthApr = false;
        lines[0].IsContractedApr = true;

        var detail = await harness.Service.GetPendingOrderAsync(harness.PendingContractId);
        var april = Assert.Single(detail!.Schemes).Months.First(m => m.Label == "Apr");

        Assert.True(april.Selected);
        Assert.False(april.Enabled);
        Assert.True(april.AlreadyParticipating);
    }

    [Fact]
    public async Task UpdateSchemeAsync_UnknownScheme_ReturnsFalse()
    {
        var harness = await CreateAsync();

        var updated = await harness.Service.UpdateSchemeAsync(harness.PendingContractId, Guid.NewGuid(), Edit());

        Assert.False(updated);
    }

    [Fact]
    public async Task UpdateSchemeAsync_KnownScheme_ReturnsTrue()
    {
        var harness = await CreateAsync();
        var schemes = await harness.PendingOrders.GetSchemesAsync(harness.PendingContractId);

        var updated = await harness.Service.UpdateSchemeAsync(harness.PendingContractId, schemes[0].PendingParticipantSchemeId, Edit(importExportLicence: true));

        Assert.True(updated);
        Assert.True(schemes[0].ImportExportLicenceRequired);
    }

    [Fact]
    public async Task ApprovePendingOrderAsync_UnknownOrder_ReturnsFalse()
    {
        var harness = await CreateAsync();

        Assert.False(await harness.Service.ApprovePendingOrderAsync(Guid.NewGuid(), "PO-2", "tester"));
    }

    [Fact]
    public async Task ApprovePendingOrderAsync_CreatesOnlineOrderContractWithDerivedPostage()
    {
        var harness = await CreateAsync();

        var approved = await harness.Service.ApprovePendingOrderAsync(harness.PendingContractId, "PO-2", "tester");

        Assert.True(approved);
        Assert.Equal("PO-2", harness.PendingOrders.LastPurchaseOrderNumber);

        var contract = Assert.Single(harness.Contracts.Contracts.Values);
        Assert.True(contract.IsOnlineOrder);
        Assert.True(contract.IsActive);
        Assert.Equal(CurrentYearId, contract.YearId);
        Assert.Equal("PO-2", contract.PurchaseOrderNumber);
        Assert.Equal("tester", contract.ApprovedBy);
        Assert.Equal(0m, contract.AdministrationCharge);
        Assert.Equal(0m, contract.DiscountRate);

        // First contract for this customer/year, so no suffix (legacy getNewContractSuffix).
        Assert.Equal(string.Empty, contract.Suffix);

        // Courier plan present: unit price 10 (UK), two despatches. The other two named plans are
        // absent from the fixture, so legacy's -1 "not derivable" marker applies.
        Assert.Equal(10m, contract.CourierPrice);
        Assert.Equal(2, contract.NumberCourier);
        Assert.Equal(-1m, contract.PostagePrice);
        Assert.Equal(-1, contract.NumberPostage);
    }

    [Fact]
    public async Task ApprovePendingOrderAsync_CreatesOneParticipantSchemePerRetainedLine()
    {
        var removed = new PendingOrderSchemeEntity
        {
            PendingParticipantSchemeId = Guid.NewGuid(),
            ParticipantId = Guid.NewGuid(),
            DistributionMonthJun = true,
            IsRemoved = true,
            Price = 50m
        };
        var retained = new PendingOrderSchemeEntity
        {
            PendingParticipantSchemeId = Guid.NewGuid(),
            ParticipantId = Guid.NewGuid(),
            DistributionMonthApr = true,
            ImportExportLicenceRequired = true,
            DataConsentDeclarationGiven = true,
            Price = 100m
        };
        var harness = await CreateAsync(removed, retained);

        await harness.Service.ApprovePendingOrderAsync(harness.PendingContractId, "PO-2", "tester");

        var created = Assert.Single(harness.ParticipantSchemes.Records.Values);
        Assert.Equal(retained.ParticipantId, created.ParticipantId);
        Assert.True(created.DistributionMonthApr);
        Assert.True(created.ImportExportLicenceRequired);
        Assert.True(created.DataConsentDeclarationGiven);
        Assert.Equal(1, created.NumberOfSetsRequired);
        Assert.True(created.CustomsCertificateRequired);
        Assert.True(created.IsWeightedPricing);
        Assert.False(created.NonFeePaying);
    }

    [Fact]
    public async Task ApprovePendingOrderAsync_ExistingContractsForYear_AssignsNextSuffix()
    {
        var harness = await CreateAsync();
        harness.Contracts.Seed(new PTL.Core.Contract.Contract { ContractId = Guid.NewGuid(), CustomerId = harness.CustomerId, YearId = CurrentYearId });

        await harness.Service.ApprovePendingOrderAsync(harness.PendingContractId, "PO-2", "tester");

        var contract = harness.Contracts.Contracts.Values.Single(c => c.IsOnlineOrder);
        Assert.Equal("A", contract.Suffix);
    }

    [Fact]
    public async Task DeclinePendingOrderAsync_DeletesEverySchemeAndCreatesNoContract()
    {
        var harness = await CreateAsync();
        var schemes = await harness.PendingOrders.GetSchemesAsync(harness.PendingContractId);

        var declined = await harness.Service.DeclinePendingOrderAsync(harness.PendingContractId);

        Assert.True(declined);
        Assert.Equal([schemes[0].PendingParticipantSchemeId], harness.PendingOrders.DeletedSchemeIds);
        Assert.Empty(harness.Contracts.Contracts);
        Assert.Empty(harness.ParticipantSchemes.Records);
    }

    [Fact]
    public async Task DeclinePendingOrderAsync_UnknownOrder_ReturnsFalse()
    {
        var harness = await CreateAsync();

        Assert.False(await harness.Service.DeclinePendingOrderAsync(Guid.NewGuid()));
    }

    private static PendingOrderSchemeEdit Edit(bool importExportLicence = false, bool isRemoved = false) =>
        new(false, false, false, true, true, false, false, false, false, false, false, false, importExportLicence, isRemoved);
}
