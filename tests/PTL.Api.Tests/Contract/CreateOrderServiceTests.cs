using Microsoft.Extensions.Options;
using PTL.Api.Tests.Customer;
using PTL.Api.Tests.Lookup;
using PTL.Api.Tests.Participant;
using PTL.Core.Contract.CreateOrder;
using PTL.Core.Contract.PendingOrder;
using PTL.Core.Lookup;
using CoreCustomer = PTL.Core.Customer.Customer;
using CoreParticipant = PTL.Core.Participant.Participant;

namespace PTL.Api.Tests.Contract;

public class CreateOrderServiceTests
{
    private const int CurrentYearId = 10;
    private const int NextYearId = 11;
    private static readonly string[] MonthNames = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    private sealed class Fixture
    {
        public Guid CustomerId { get; } = Guid.NewGuid();
        public Guid ParticipantId { get; } = Guid.NewGuid();
        public Guid PostageId { get; } = Guid.NewGuid();
        public FakeCreateOrderStore Store { get; } = new();
        public FakeParticipantRepository Participants { get; } = new();
        public FakeCustomerRepository Customers { get; } = new();
        public FakeLookupRepository Lookup { get; } = new();
        public CreateOrderService Service { get; private set; } = null!;
        public Guid CurrencyId { get; } = Guid.NewGuid();
        public Guid CountryId { get; } = Guid.NewGuid();

        public static async Task<Fixture> CreateAsync(bool canOrderOnline = true, Guid? customerTypeId = null)
        {
            var fixture = new Fixture();
            await fixture.Customers.CreateAsync(new CoreCustomer
            {
                CustomerId = fixture.CustomerId,
                CanOrderOnline = canOrderOnline,
                CurrencyId = fixture.CurrencyId,
                CountryId = fixture.CountryId,
                CustomerTypeId = customerTypeId ?? Guid.NewGuid()
            });
            await fixture.Participants.CreateAsync(new CoreParticipant { ParticipantId = fixture.ParticipantId, CustomerId = fixture.CustomerId, LabCode = "LAB001", IsActive = true });

            fixture.Lookup.SystemSettings = new SystemSettingsEntity { CurrentYearId = CurrentYearId, NextYearId = NextYearId };
            fixture.Lookup.AllYears = [new YearEntity { YearId = CurrentYearId, Year = "2026/27" }, new YearEntity { YearId = NextYearId, Year = "2027/28" }];
            fixture.Lookup.Countries = [new CountryEntity { CountryId = fixture.CountryId, Country = "United Kingdom", CountryType = "UK" }];
            fixture.Lookup.PostagePricingPlans = [new PostagePricingPlanEntity { PostageId = fixture.PostageId, Name = "Courier", UKPrice = 5m, EUPrice = 7m, NonEUPrice = 9m, YearId = CurrentYearId }];

            fixture.Service = new CreateOrderService(
                fixture.Store,
                fixture.Store,
                fixture.Participants,
                fixture.Customers,
                fixture.Lookup,
                Options.Create(new CreateOrderOptions()));
            return fixture;
        }

        public AvailableSchemeEntity AddScheme(string name, string identifier, string schedule = "Monthly", bool commercial = true, bool priced = true, params int[] months)
        {
            var scheme = new AvailableSchemeEntity
            {
                SchemeId = Guid.NewGuid(),
                Name = name,
                Identifier = identifier,
                Schedule = schedule,
                ComerciallyAvailable = commercial,
                Postage = PostageId
            };

            foreach (var month in months.Length == 0 ? [4, 5, 6] : months)
            {
                SetProperty(scheme, $"DistributionMonth{MonthNames[month - 1]}", true);
            }

            Store.AvailableSchemes.Add(scheme);
            if (priced)
            {
                Lookup.SchemeCurrencies = [.. Lookup.SchemeCurrencies, new SchemeCurrencyEntity { SchemeId = scheme.SchemeId, CurrencyId = CurrencyId, Price = 100m, CurrencySymbol = "£" }];
            }

            return scheme;
        }

        public async Task<CreateOrderPage> OrderAsync(OrderYear year = OrderYear.Current)
        {
            var result = await Service.GetOrderAsync(ParticipantId, year);
            Assert.Equal(CreateOrderAccess.Allowed, result.Access);
            return result.Page!;
        }

        public async Task<CreateOrderPage> ChangeAsync(Guid schemeId, string field, bool isChecked, OrderYear year = OrderYear.Current)
        {
            var result = await Service.ApplyChangeAsync(ParticipantId, year, new CreateOrderLineChange(schemeId, field, isChecked));
            Assert.Equal(CreateOrderAccess.Allowed, result.Access);
            return result.Page!;
        }

        public PendingOrderSchemeEntity Line(Guid schemeId, int yearId = CurrentYearId)
        {
            var order = Store.Orders.Single(o => o.CustomerId == CustomerId && o.YearId == yearId && !o.IsDeleted && !o.IsSubmitted);
            return Store.LinesFor(order.PendingContractId).Single(l => l.SchemeId == schemeId);
        }
    }

    private static void SetProperty(object target, string name, bool value) => target.GetType().GetProperty(name)!.SetValue(target, value);

    private static CreateOrderScheme Row(CreateOrderPage page, Guid schemeId) =>
        page.ParticipatingSchemes.Concat(page.AvailableSchemes.SelectMany(g => g.Schemes)).Single(s => s.SchemeId == schemeId);

    private static string[] PendingMonths(CreateOrderScheme scheme) =>
        [.. scheme.Months.Where(m => m.Selected && !m.AlreadyParticipating).Select(m => m.Label)];

    [Fact]
    public async Task GetEligibility_SoleActiveParticipantAndOnlineOrdering_AllowsBothYears()
    {
        var fixture = await Fixture.CreateAsync();

        var eligibility = await fixture.Service.GetEligibilityAsync(fixture.ParticipantId);

        Assert.Equal(new CreateOrderEligibility(true, true, true), eligibility);
    }

    [Fact]
    public async Task GetEligibility_SubmittedOrderForCurrentYear_BlocksOnlyThatYear()
    {
        var fixture = await Fixture.CreateAsync();
        fixture.Store.SeedSubmittedOrder(fixture.CustomerId, CurrentYearId);

        var eligibility = await fixture.Service.GetEligibilityAsync(fixture.ParticipantId);

        Assert.Equal(new CreateOrderEligibility(true, false, true), eligibility);
    }

    [Fact]
    public async Task GetEligibility_AnotherActiveParticipantOnCustomer_BlocksOrdering()
    {
        var fixture = await Fixture.CreateAsync();
        await fixture.Participants.CreateAsync(new CoreParticipant { ParticipantId = Guid.NewGuid(), CustomerId = fixture.CustomerId, LabCode = "LAB002", IsActive = true });

        var eligibility = await fixture.Service.GetEligibilityAsync(fixture.ParticipantId);

        Assert.Equal(new CreateOrderEligibility(false, false, false), eligibility);
    }

    [Fact]
    public async Task GetEligibility_InactiveSecondParticipant_StillAllowsOrdering()
    {
        var fixture = await Fixture.CreateAsync();
        await fixture.Participants.CreateAsync(new CoreParticipant { ParticipantId = Guid.NewGuid(), CustomerId = fixture.CustomerId, LabCode = "LAB002", IsActive = false });

        var eligibility = await fixture.Service.GetEligibilityAsync(fixture.ParticipantId);

        Assert.True(eligibility!.CanOrderOnline);
    }

    [Fact]
    public async Task GetEligibility_CustomerCannotOrderOnline_BlocksOrdering()
    {
        var fixture = await Fixture.CreateAsync(canOrderOnline: false);

        var eligibility = await fixture.Service.GetEligibilityAsync(fixture.ParticipantId);

        Assert.Equal(new CreateOrderEligibility(false, false, false), eligibility);
    }

    [Fact]
    public async Task GetEligibility_UnknownParticipant_ReturnsNull()
    {
        var fixture = await Fixture.CreateAsync();

        Assert.Null(await fixture.Service.GetEligibilityAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetOrder_UnknownParticipant_ReturnsNotFound()
    {
        var fixture = await Fixture.CreateAsync();

        var result = await fixture.Service.GetOrderAsync(Guid.NewGuid(), OrderYear.Current);

        Assert.Equal(CreateOrderAccess.NotFound, result.Access);
    }

    [Fact]
    public async Task GetOrder_SubmittedOrderForYear_ReturnsNotEligible()
    {
        var fixture = await Fixture.CreateAsync();
        fixture.Store.SeedSubmittedOrder(fixture.CustomerId, NextYearId);

        var result = await fixture.Service.GetOrderAsync(fixture.ParticipantId, OrderYear.Next);

        Assert.Equal(CreateOrderAccess.NotEligible, result.Access);
        Assert.Null(result.Page);
        Assert.Equal(0, fixture.Store.CreatedOrders);
    }

    [Fact]
    public async Task GetOrder_FirstVisit_CreatesDraftOnceAndGroupsCommercialSchemesByScheduleThenName()
    {
        var fixture = await Fixture.CreateAsync();
        var zinc = fixture.AddScheme("Zinc", "PT2", schedule: "Quarterly");
        var alpha = fixture.AddScheme("Alpha", "PT1", schedule: "Monthly");
        fixture.AddScheme("Hidden", "PT3", commercial: false);

        var page = await fixture.OrderAsync();
        await fixture.OrderAsync();

        Assert.Equal(1, fixture.Store.CreatedOrders);
        Assert.Equal("2026/27", page.Year);
        Assert.StartsWith("QAL/", page.QalNumber);
        Assert.Equal("LAB001", page.LabCode);
        Assert.Equal(["Monthly", "Quarterly"], page.AvailableSchemes.Select(g => g.Schedule));
        Assert.Equal([alpha.SchemeId, zinc.SchemeId], page.AvailableSchemes.SelectMany(g => g.Schemes).Select(s => s.SchemeId));
        Assert.Equal(100m, Row(page, alpha.SchemeId).AnnualPrice);
        Assert.False(page.CanProceed);
    }

    [Fact]
    public async Task GetOrder_AphaCustomer_AlsoListsNonCommercialSchemes()
    {
        var fixture = await Fixture.CreateAsync(customerTypeId: new CreateOrderOptions().AphaCustomerTypeId);
        var hidden = fixture.AddScheme("Hidden", "PT3", commercial: false);

        var page = await fixture.OrderAsync();

        Assert.True(page.IsAphaCustomer);
        Assert.Contains(page.AvailableSchemes.SelectMany(g => g.Schemes), s => s.SchemeId == hidden.SchemeId);
    }

    [Fact]
    public async Task GetOrder_SchemeWithoutPriceForCustomerCurrency_IsNotListed()
    {
        var fixture = await Fixture.CreateAsync();
        fixture.AddScheme("Unpriced", "PT9", priced: false);

        var page = await fixture.OrderAsync();

        Assert.Empty(page.AvailableSchemes);
    }

    [Fact]
    public async Task ApplyChange_SelectScheme_PreTicksEditableMonthsAndPricesTheLine()
    {
        var fixture = await Fixture.CreateAsync();
        var scheme = fixture.AddScheme("Alpha", "PT1");
        scheme.CanEditMay = false;
        await fixture.OrderAsync();

        var page = await fixture.ChangeAsync(scheme.SchemeId, "select", true);

        var row = Row(page, scheme.SchemeId);
        Assert.True(row.Selected);
        Assert.Equal(["Apr", "Jun"], PendingMonths(row));
        Assert.Equal(2 * FakeCreateOrderStore.PricePerMonth, row.SchemePrice);
        Assert.Equal(10m, row.PostagePrice);
        Assert.Equal(30m, row.TotalPrice);
        Assert.Equal(20m, page.SchemeTotal);
        Assert.Equal(10m, page.PostageTotal);
        Assert.Equal(30m, page.OrderTotal);
        Assert.True(page.CanProceed);
    }

    [Fact]
    public async Task ApplyChange_UntickMonth_RecalculatesPricesAndTotals()
    {
        var fixture = await Fixture.CreateAsync();
        var scheme = fixture.AddScheme("Alpha", "PT1");
        await fixture.OrderAsync();
        await fixture.ChangeAsync(scheme.SchemeId, "select", true);

        var page = await fixture.ChangeAsync(scheme.SchemeId, "apr", false);

        var row = Row(page, scheme.SchemeId);
        Assert.Equal(["May", "Jun"], PendingMonths(row));
        Assert.Equal(20m, row.SchemePrice);
        Assert.Equal(10m, row.PostagePrice);
        Assert.Equal(30m, page.OrderTotal);
    }

    [Fact]
    public async Task ApplyChange_TickMonthTheSchemeDoesNotOffer_IsIgnored()
    {
        var fixture = await Fixture.CreateAsync();
        var scheme = fixture.AddScheme("Alpha", "PT1");
        await fixture.OrderAsync();
        await fixture.ChangeAsync(scheme.SchemeId, "select", true);

        var page = await fixture.ChangeAsync(scheme.SchemeId, "dec", true);

        Assert.Equal(["Apr", "May", "Jun"], PendingMonths(Row(page, scheme.SchemeId)));
    }

    [Fact]
    public async Task ApplyChange_TickMonthAlreadyPosted_IsIgnored()
    {
        var fixture = await Fixture.CreateAsync();
        var scheme = fixture.AddScheme("Alpha", "PT1");
        scheme.CanEditMay = false;
        await fixture.OrderAsync();
        await fixture.ChangeAsync(scheme.SchemeId, "select", true);

        var page = await fixture.ChangeAsync(scheme.SchemeId, "may", true);

        var may = Row(page, scheme.SchemeId).Months.Single(m => m.Label == "May");
        Assert.False(may.Enabled);
        Assert.False(may.Selected);
    }

    [Fact]
    public async Task ApplyChange_ImportPermit_ClearedWhenLastMonthIsUnticked()
    {
        var fixture = await Fixture.CreateAsync();
        var scheme = fixture.AddScheme("Alpha", "PT1", months: [4, 5]);
        await fixture.OrderAsync();
        await fixture.ChangeAsync(scheme.SchemeId, "select", true);
        var withPermit = await fixture.ChangeAsync(scheme.SchemeId, "importexport", true);
        Assert.True(Row(withPermit, scheme.SchemeId).ImportPermitRequired);

        await fixture.ChangeAsync(scheme.SchemeId, "apr", false);
        var page = await fixture.ChangeAsync(scheme.SchemeId, "may", false);

        Assert.False(Row(page, scheme.SchemeId).ImportPermitRequired);
        Assert.False(page.CanProceed);
    }

    [Fact]
    public async Task ApplyChange_UnselectScheme_ExcludesItFromTotals()
    {
        var fixture = await Fixture.CreateAsync();
        var scheme = fixture.AddScheme("Alpha", "PT1");
        await fixture.OrderAsync();
        await fixture.ChangeAsync(scheme.SchemeId, "select", true);

        var page = await fixture.ChangeAsync(scheme.SchemeId, "select", false);

        Assert.Equal(0m, page.SchemeTotal);
        Assert.Equal(0m, page.PostageTotal);
        Assert.False(page.CanProceed);
        Assert.False(Row(page, scheme.SchemeId).Selected);
    }

    [Fact]
    public async Task ApplyChange_SchemeNotListedForCustomer_IsIgnored()
    {
        var fixture = await Fixture.CreateAsync();
        var hidden = fixture.AddScheme("Hidden", "PT3", commercial: false);
        await fixture.OrderAsync();

        var page = await fixture.ChangeAsync(hidden.SchemeId, "select", true);

        Assert.Empty(fixture.Store.LinesFor(fixture.Store.Orders.Single().PendingContractId));
        Assert.Empty(page.AvailableSchemes);
    }

    [Fact]
    public async Task ApplyChange_MonthForSchemeThatIsNotSelected_IsIgnored()
    {
        var fixture = await Fixture.CreateAsync();
        var scheme = fixture.AddScheme("Alpha", "PT1");
        await fixture.OrderAsync();
        await fixture.ChangeAsync(scheme.SchemeId, "select", true);
        await fixture.ChangeAsync(scheme.SchemeId, "select", false);

        await fixture.ChangeAsync(scheme.SchemeId, "apr", false);

        Assert.True(fixture.Line(scheme.SchemeId).DistributionMonthApr);
    }

    [Fact]
    public async Task ApplyChange_UnknownField_Throws()
    {
        var fixture = await Fixture.CreateAsync();
        var scheme = fixture.AddScheme("Alpha", "PT1");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            fixture.Service.ApplyChangeAsync(fixture.ParticipantId, OrderYear.Current, new CreateOrderLineChange(scheme.SchemeId, "bogus", true)));
    }

    [Fact]
    public async Task ApplyChange_NotEligible_ReturnsNotEligibleWithoutChanging()
    {
        var fixture = await Fixture.CreateAsync();
        var scheme = fixture.AddScheme("Alpha", "PT1");
        fixture.Store.SeedSubmittedOrder(fixture.CustomerId, CurrentYearId);

        var result = await fixture.Service.ApplyChangeAsync(fixture.ParticipantId, OrderYear.Current, new CreateOrderLineChange(scheme.SchemeId, "select", true));

        Assert.Equal(CreateOrderAccess.NotEligible, result.Access);
        Assert.Equal(0, fixture.Store.CreatedOrders);
    }

    [Fact]
    public async Task GetOrder_ParticipantAlreadyHoldsMonth_ShowsItTickedAndLockedAndKeepsItOutOfThePendingOrder()
    {
        var fixture = await Fixture.CreateAsync();
        var scheme = fixture.AddScheme("Alpha", "PT1", commercial: false);
        fixture.Store.Participating.Add((CurrentYearId, new ParticipatingSchemeEntity
        {
            ParticipantId = fixture.ParticipantId,
            SchemeId = scheme.SchemeId,
            Identifier = "PT1",
            Name = "Alpha",
            Schedule = "Monthly",
            DistributionMonthApr = true
        }));

        var page = await fixture.OrderAsync();
        var afterSelect = await fixture.ChangeAsync(scheme.SchemeId, "select", true);

        var row = Row(page, scheme.SchemeId);
        Assert.Contains(page.ParticipatingSchemes, s => s.SchemeId == scheme.SchemeId);
        Assert.Empty(page.AvailableSchemes);
        var april = row.Months.Single(m => m.Label == "Apr");
        Assert.True(april.Selected);
        Assert.True(april.AlreadyParticipating);
        Assert.False(april.Enabled);
        Assert.Equal(["May", "Jun"], PendingMonths(Row(afterSelect, scheme.SchemeId)));
        Assert.False(fixture.Line(scheme.SchemeId).DistributionMonthApr);
    }

    [Fact]
    public async Task GetOrder_ParticipatingSchemeOrderedTwice_MergesMonths()
    {
        var fixture = await Fixture.CreateAsync();
        var scheme = fixture.AddScheme("Alpha", "PT1");
        foreach (var month in new[] { "DistributionMonthApr", "DistributionMonthMay" })
        {
            var held = new ParticipatingSchemeEntity { ParticipantId = fixture.ParticipantId, SchemeId = scheme.SchemeId, Identifier = "PT1", Name = "Alpha", Schedule = "Monthly" };
            SetProperty(held, month, true);
            fixture.Store.Participating.Add((CurrentYearId, held));
        }

        var page = await fixture.OrderAsync();

        var row = Assert.Single(page.ParticipatingSchemes);
        Assert.Equal(["Apr", "May"], row.Months.Where(m => m.AlreadyParticipating).Select(m => m.Label));
    }

    [Fact]
    public async Task GetOrder_PendingMonthNoLongerEditable_IsDeselected()
    {
        var fixture = await Fixture.CreateAsync();
        var scheme = fixture.AddScheme("Alpha", "PT1");
        scheme.CanEditApr = false;
        var order = Guid.NewGuid();
        await fixture.Store.CreatePendingOrderAsync(order, fixture.CustomerId, CurrentYearId);
        fixture.Store.SeedLine(order, new PendingOrderSchemeEntity
        {
            PendingParticipantSchemeId = Guid.NewGuid(),
            ParticipantId = fixture.ParticipantId,
            SchemeId = scheme.SchemeId,
            IsSelected = true,
            DistributionMonthApr = true,
            DistributionMonthMay = true
        });

        var page = await fixture.OrderAsync();

        Assert.Equal(["May"], PendingMonths(Row(page, scheme.SchemeId)));
        Assert.Equal(1, fixture.Store.CreatedOrders);
    }

    [Fact]
    public async Task GetOrder_NextYearWithNothingOrdered_RenewsSchemesHeldThisYear()
    {
        var fixture = await Fixture.CreateAsync();
        var nextYearScheme = fixture.AddScheme("Alpha", "PT1", commercial: false);
        fixture.Store.Participating.Add((CurrentYearId, new ParticipatingSchemeEntity
        {
            ParticipantId = fixture.ParticipantId,
            SchemeId = Guid.NewGuid(),
            Identifier = "PT1",
            Name = "Alpha",
            Schedule = "Monthly",
            DistributionMonthApr = true
        }));
        fixture.Store.Participating.Add((CurrentYearId, new ParticipatingSchemeEntity
        {
            ParticipantId = fixture.ParticipantId,
            SchemeId = Guid.NewGuid(),
            Identifier = "NOT-OFFERED-NEXT-YEAR",
            Name = "Gone",
            Schedule = "Monthly"
        }));

        var page = await fixture.OrderAsync(OrderYear.Next);
        var again = await fixture.OrderAsync(OrderYear.Next);

        var row = Assert.Single(page.ParticipatingSchemes);
        Assert.Equal(nextYearScheme.SchemeId, row.SchemeId);
        Assert.True(row.Selected);
        Assert.Equal(["Apr", "May", "Jun"], PendingMonths(row));
        Assert.True(page.CanProceed);
        Assert.Single(fixture.Store.LinesFor(fixture.Store.Orders.Single(o => o.YearId == NextYearId).PendingContractId));
        Assert.Single(again.ParticipatingSchemes);
    }

    [Fact]
    public async Task Clear_DeletesLinesAndDraftAndTheNextVisitStartsAFreshDraft()
    {
        var fixture = await Fixture.CreateAsync();
        var scheme = fixture.AddScheme("Alpha", "PT1");
        await fixture.OrderAsync();
        await fixture.ChangeAsync(scheme.SchemeId, "select", true);
        var firstDraft = fixture.Store.Orders.Single();

        var access = await fixture.Service.ClearAsync(fixture.ParticipantId, OrderYear.Current);
        var page = await fixture.OrderAsync();

        Assert.Equal(CreateOrderAccess.Allowed, access);
        Assert.True(firstDraft.IsDeleted);
        Assert.Empty(fixture.Store.LinesFor(firstDraft.PendingContractId));
        Assert.Equal(2, fixture.Store.CreatedOrders);
        Assert.False(Row(page, scheme.SchemeId).Selected);
    }

    [Fact]
    public async Task Clear_NotEligible_ReturnsNotEligible()
    {
        var fixture = await Fixture.CreateAsync(canOrderOnline: false);

        Assert.Equal(CreateOrderAccess.NotEligible, await fixture.Service.ClearAsync(fixture.ParticipantId, OrderYear.Current));
    }

    [Fact]
    public async Task Clear_NoDraft_IsANoOp()
    {
        var fixture = await Fixture.CreateAsync();

        Assert.Equal(CreateOrderAccess.Allowed, await fixture.Service.ClearAsync(fixture.ParticipantId, OrderYear.Current));
        Assert.Empty(fixture.Store.Orders);
    }

    [Fact]
    public void ParticipatingSchemeEntity_Holds_UnknownMonth_IsFalse()
    {
        Assert.False(new ParticipatingSchemeEntity { DistributionMonthApr = true }.Holds(13));
        Assert.False(new AvailableSchemeEntity { DistributionMonthApr = true }.Offers(0));
        Assert.False(new AvailableSchemeEntity().CanEdit(13));
    }

    [Fact]
    public void PendingOrderSchemeMonthExtensions_SetMonth_RejectsUnknownMonth()
    {
        var line = new PendingOrderSchemeEntity();

        Assert.Throws<ArgumentOutOfRangeException>(() => line.SetMonth(13, true));
        Assert.False(line.GetMonth(13));
        Assert.False(line.HasAnyMonth());
    }

    [Fact]
    public void PendingOrderSchemeMonthExtensions_SetAndGetMonth_RoundTripEveryMonth()
    {
        var line = new PendingOrderSchemeEntity();

        for (var month = 1; month <= 12; month++)
        {
            line.SetMonth(month, true);
            Assert.True(line.GetMonth(month));
        }

        Assert.True(line.HasAnyMonth());
    }

    [Fact]
    public void AvailableSchemeEntity_OffersAndCanEdit_ReadEveryMonth()
    {
        var scheme = new AvailableSchemeEntity();
        for (var month = 1; month <= 12; month++)
        {
            SetProperty(scheme, $"DistributionMonth{MonthNames[month - 1]}", true);
            SetProperty(scheme, $"CanEdit{MonthNames[month - 1]}", false);
        }

        Assert.All(Enumerable.Range(1, 12), month =>
        {
            Assert.True(scheme.Offers(month));
            Assert.False(scheme.CanEdit(month));
        });
    }

    [Fact]
    public void ParticipatingSchemeEntity_MergeMonthsFrom_AndHolds_CoverEveryMonth()
    {
        var other = new ParticipatingSchemeEntity();
        for (var month = 1; month <= 12; month++)
        {
            SetProperty(other, $"DistributionMonth{MonthNames[month - 1]}", true);
        }

        var merged = new ParticipatingSchemeEntity();
        merged.MergeMonthsFrom(other);

        Assert.All(Enumerable.Range(1, 12), month => Assert.True(merged.Holds(month)));
    }
}
