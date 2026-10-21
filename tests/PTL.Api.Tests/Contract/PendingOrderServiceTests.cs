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
        PTL.Api.Tests.Participant.FakeParticipantRepository Participants,
        FakePendingOrderNotifier Notifier,
        Guid CustomerId,
        Guid PendingContractId,
        Guid SchemeId);

    private static Task<Harness> CreateAsync(params PendingOrderSchemeEntity[] schemes) => CreateWithActorAsync("tester", schemes);

    private static async Task<Harness> CreateWithActorAsync(string actor, PendingOrderSchemeEntity[] schemes)
    {
        var customerRepository = new FakeCustomerRepository();
        var customer = await customerRepository.CreateAsync(new PTL.Core.Customer.Customer
        {
            CustomerId = Guid.NewGuid(),
            Name = "Sample Laboratories Ltd",
            Email = "customer@example.com",
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

        var participants = new PTL.Api.Tests.Participant.FakeParticipantRepository();
        await participants.CreateAsync(new PTL.Core.Participant.Participant { CustomerId = customer.CustomerId, LabCode = "001", Email = "participant@example.com", Email2 = "participant2@example.com", IsActive = true });
        var notifier = new FakePendingOrderNotifier();
        var service = new PendingOrderService(
            pendingOrders,
            new PendingOrderContractWriter(contracts, participantSchemes),
            schemeRepository,
            customerRepository,
            lookupRepository,
            new FakeAuditActorResolver(actor),
            new PendingOrderDecisionEmails(pendingOrders, customerRepository, participants, notifier));

        return new Harness(service, pendingOrders, contracts, participantSchemes, schemeRepository, participants, notifier, customer.CustomerId, pendingContractId, realScheme.SchemeId);
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
    public async Task GetPendingOrdersAsync_ExcludesUnsubmittedAndDeletedOrders()
    {
        var harness = await CreateAsync();
        var unsubmittedId = Guid.NewGuid();
        var deletedId = Guid.NewGuid();
        harness.PendingOrders.Seed(
            new PendingOrderEntity { PendingContractId = unsubmittedId, YearId = CurrentYearId, IsSubmitted = false },
            new PendingOrderSummaryEntity { PendingContractId = unsubmittedId, YearId = CurrentYearId, IsSubmitted = false });
        harness.PendingOrders.Seed(
            new PendingOrderEntity { PendingContractId = deletedId, YearId = CurrentYearId, IsSubmitted = true, IsDeleted = true },
            new PendingOrderSummaryEntity { PendingContractId = deletedId, YearId = CurrentYearId, IsSubmitted = true, IsDeleted = true });

        var (currentYear, _) = await harness.Service.GetPendingOrdersAsync();

        Assert.Single(currentYear);
        Assert.DoesNotContain(currentYear, o => o.PendingContractId == unsubmittedId || o.PendingContractId == deletedId);
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

    // Legacy: Jan-Mar chk.Enabled = scheme.DistributionMonthX And scheme.CanEditX And scheme.CanEditXParticipants;
    // Apr-Dec chk.Enabled = scheme.DistributionMonthX And scheme.CanEditXParticipants (no scheme.CanEditX).
    [Fact]
    public async Task GetPendingOrderAsync_EnablesMonthOnlyWhenSchemeOffersItAndNothingIsPosted()
    {
        var harness = await CreateAsync();
        var scheme = await harness.Schemes.GetByIdAsync(harness.SchemeId);
        scheme!.DistributionMonthApr = true;   // offered and fully editable
        scheme.DistributionMonthMay = true;    // offered; the scheme's own CanEdit is ignored for Apr-Dec
        scheme.CanEditMay = false;
        scheme.DistributionMonthJun = true;    // offered but a distribution has been posted
        scheme.DistributionMonthJul = false;   // not offered at all
        scheme.DistributionMonthJan = true;    // offered, but Jan-Mar also need the scheme's own CanEdit
        scheme.CanEditJan = false;
        scheme.DistributionMonthFeb = true;
        await harness.Schemes.UpdateAsync(scheme);

        var lines = await harness.PendingOrders.GetSchemesAsync(harness.PendingContractId);
        lines[0].CanEditJun = false;

        var detail = await harness.Service.GetPendingOrderAsync(harness.PendingContractId);
        var months = Assert.Single(detail!.Schemes).Months.ToDictionary(m => m.Label);

        Assert.True(months["Apr"].Enabled);
        Assert.True(months["May"].Enabled);
        Assert.False(months["Jun"].Enabled);
        Assert.False(months["Jul"].Enabled);
        Assert.False(months["Jan"].Enabled);
        Assert.True(months["Feb"].Enabled);
    }

    // Legacy: Checked = If(Enabled, pendingScheme.DistributionMonthX, scheme.DistributionMonthX).
    [Fact]
    public async Task GetPendingOrderAsync_LockedMonthFallsBackToTheSchemesOwnValue()
    {
        var harness = await CreateAsync();
        var scheme = await harness.Schemes.GetByIdAsync(harness.SchemeId);
        scheme!.DistributionMonthApr = true;
        scheme.DistributionMonthMay = true;
        await harness.Schemes.UpdateAsync(scheme);

        var lines = await harness.PendingOrders.GetSchemesAsync(harness.PendingContractId);
        lines[0].DistributionMonthApr = false;
        lines[0].DistributionMonthMay = false;
        lines[0].CanEditMay = false;

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

        Assert.False(await harness.Service.ApprovePendingOrderAsync(Guid.NewGuid(), "PO-2"));
    }

    [Fact]
    public async Task ApprovePendingOrderAsync_CreatesOnlineOrderContractWithDerivedPostage()
    {
        var harness = await CreateAsync();

        var approved = await harness.Service.ApprovePendingOrderAsync(harness.PendingContractId, "PO-2");

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

        await harness.Service.ApprovePendingOrderAsync(harness.PendingContractId, "PO-2");

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

        await harness.Service.ApprovePendingOrderAsync(harness.PendingContractId, "PO-2");

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

    [Fact]
    public async Task GetPendingOrderAsync_DecidedOrUnsubmittedOrder_ReturnsNull()
    {
        var harness = await CreateAsync();
        await harness.Service.DeclinePendingOrderAsync(harness.PendingContractId);

        Assert.Null(await harness.Service.GetPendingOrderAsync(harness.PendingContractId));
    }

    [Fact]
    public async Task UpdateSchemeAsync_LockedMonthsKeepTheirStoredValue_WhileEditableOnesTakeThePostedValue()
    {
        var harness = await CreateAsync();
        var scheme = await harness.Schemes.GetByIdAsync(harness.SchemeId);
        scheme!.DistributionMonthApr = true;
        scheme.DistributionMonthMay = true;
        await harness.Schemes.UpdateAsync(scheme);
        var lines = await harness.PendingOrders.GetSchemesAsync(harness.PendingContractId);
        lines[0].DistributionMonthApr = true;
        lines[0].DistributionMonthMay = false;
        lines[0].CanEditMay = false;   // posted distribution: May is locked, so the page only displays the scheme's own value

        // The locked May posts back ticked (the scheme's value), Apr is un-ticked by the user.
        await harness.Service.UpdateSchemeAsync(harness.PendingContractId, lines[0].PendingParticipantSchemeId, Edit() with { DistributionMonthApr = false, DistributionMonthMay = true });

        Assert.False(lines[0].DistributionMonthApr);
        Assert.False(lines[0].DistributionMonthMay);
    }

    [Fact]
    public async Task ApprovePendingOrderAsync_AlreadyApprovedOrder_ReturnsFalseAndCreatesNothingMore()
    {
        var harness = await CreateAsync();
        Assert.True(await harness.Service.ApprovePendingOrderAsync(harness.PendingContractId, "PO-2"));

        Assert.False(await harness.Service.ApprovePendingOrderAsync(harness.PendingContractId, "PO-3"));

        Assert.Single(harness.Contracts.Contracts);
    }

    [Fact]
    public async Task ApprovePendingOrderAsync_RecordsTheSignedInUserLowerCased()
    {
        var harness = await CreateWithActorAsync("DT2\\ALICE", []);

        await harness.Service.ApprovePendingOrderAsync(harness.PendingContractId, "PO-2");

        Assert.Equal("dt2\\alice", Assert.Single(harness.Contracts.Contracts.Values).ApprovedBy);
    }

    [Fact]
    public async Task ApprovePendingOrderAsync_NobodySignedIn_RecordsGuest()
    {
        var harness = await CreateWithActorAsync("Guest", []);

        await harness.Service.ApprovePendingOrderAsync(harness.PendingContractId, "PO-2");

        Assert.Equal("Guest", Assert.Single(harness.Contracts.Contracts.Values).ApprovedBy);
    }

    [Fact]
    public async Task ApprovePendingOrderAsync_OverLongPurchaseOrderNumber_ThrowsAndChangesNothing()
    {
        var harness = await CreateAsync();

        var ex = await Assert.ThrowsAsync<PendingOrderValidationException>(() => harness.Service.ApprovePendingOrderAsync(harness.PendingContractId, new string('x', 256)));

        Assert.Contains("PurchaseOrderNumber", ex.Errors.Keys);
        Assert.Empty(harness.Contracts.Contracts);
    }

    [Fact]
    public async Task ApprovePendingOrderAsync_OverLongComments_Throws()
    {
        var harness = await CreateAsync();

        var ex = await Assert.ThrowsAsync<PendingOrderValidationException>(() => harness.Service.ApprovePendingOrderAsync(harness.PendingContractId, "PO-2", new string('x', 2001)));

        Assert.Equal("The comments field allows maximum 2000 characters", ex.Errors["Comments"][0]);
    }

    [Fact]
    public async Task ApprovePendingOrderAsync_QueuesApprovalEmailsForCustomerAndTheFirstActiveParticipant()
    {
        var harness = await CreateAsync();

        await harness.Service.ApprovePendingOrderAsync(harness.PendingContractId, "PO-2", "Welcome aboard");

        var contractId = Assert.Single(harness.Contracts.Contracts.Values).ContractId;
        Assert.Equal(
            ["customer@example.com", "participant@example.com", "participant2@example.com"],
            harness.PendingOrders.QueuedNotifications.Select(n => n.Email));
        Assert.All(harness.PendingOrders.QueuedNotifications, n =>
        {
            Assert.True(n.IsApproved);
            Assert.Equal(contractId, n.ContractId);
            Assert.Equal(harness.PendingContractId, n.PendingContractId);
            Assert.Equal(CurrentYearId, n.YearId);
            Assert.Equal("Welcome aboard", n.Notes);
            Assert.Equal("Sample Laboratories Ltd", n.CustomerName);
        });
    }

    [Fact]
    public async Task ApprovePendingOrderAsync_NoSecondParticipantEmail_QueuesTwoEmails()
    {
        var harness = await CreateAsync();
        var participant = (await harness.Participants.GetSummariesAsync(harness.CustomerId, false)).Single();
        var entity = (await harness.Participants.GetByIdAsync(participant.ParticipantId))!;
        entity.Email2 = string.Empty;
        await harness.Participants.UpdateAsync(entity);

        await harness.Service.ApprovePendingOrderAsync(harness.PendingContractId, "PO-2");

        Assert.Equal(2, harness.PendingOrders.QueuedNotifications.Count);
    }

    [Fact]
    public async Task DeclinePendingOrderAsync_QueuesDeclineEmailsWithNoContract()
    {
        var harness = await CreateAsync();

        await harness.Service.DeclinePendingOrderAsync(harness.PendingContractId, "Not eligible");

        Assert.Equal(3, harness.PendingOrders.QueuedNotifications.Count);
        Assert.All(harness.PendingOrders.QueuedNotifications, n =>
        {
            Assert.False(n.IsApproved);
            Assert.Equal(Guid.Empty, n.ContractId);
            Assert.Equal("Not eligible", n.Notes);
        });
    }

    [Fact]
    public async Task DeclinePendingOrderAsync_OverLongComments_ThrowsAndDeletesNothing()
    {
        var harness = await CreateAsync();

        await Assert.ThrowsAsync<PendingOrderValidationException>(() => harness.Service.DeclinePendingOrderAsync(harness.PendingContractId, new string('x', 2001)));

        Assert.Empty(harness.PendingOrders.DeletedSchemeIds);
    }

    [Fact]
    public async Task DeclinePendingOrderAsync_AlreadyDeclinedOrder_ReturnsFalse()
    {
        var harness = await CreateAsync();
        Assert.True(await harness.Service.DeclinePendingOrderAsync(harness.PendingContractId));

        Assert.False(await harness.Service.DeclinePendingOrderAsync(harness.PendingContractId));
    }

    [Fact]
    public async Task ApprovePendingOrderAsync_SendsTheOrdersEmailsOnceTheyAreQueued()
    {
        var harness = await CreateAsync();

        await harness.Service.ApprovePendingOrderAsync(harness.PendingContractId, "PO-2");

        Assert.Equal([harness.PendingContractId], harness.Notifier.SentForOrders);
        Assert.Equal(3, harness.PendingOrders.QueuedNotifications.Count);
    }

    [Fact]
    public async Task DeclinePendingOrderAsync_SendsTheOrdersEmailsOnceTheyAreQueued()
    {
        var harness = await CreateAsync();

        await harness.Service.DeclinePendingOrderAsync(harness.PendingContractId, "Not eligible");

        Assert.Equal([harness.PendingContractId], harness.Notifier.SentForOrders);
    }

    [Fact]
    public async Task ApproveAndDecline_NothingDecided_SendNothing()
    {
        var harness = await CreateAsync();

        Assert.False(await harness.Service.ApprovePendingOrderAsync(Guid.NewGuid(), "PO-2"));
        Assert.False(await harness.Service.DeclinePendingOrderAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<PendingOrderValidationException>(() => harness.Service.ApprovePendingOrderAsync(harness.PendingContractId, "PO-2", new string('x', 2001)));

        Assert.Empty(harness.Notifier.SentForOrders);
    }

    private static PendingOrderSchemeEdit Edit(bool importExportLicence = false, bool isRemoved = false) =>
        new(false, false, false, true, true, false, false, false, false, false, false, false, importExportLicence, isRemoved);
}
