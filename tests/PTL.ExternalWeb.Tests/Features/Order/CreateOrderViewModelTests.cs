using PTL.Contracts.CreateOrder;
using PTL.ExternalWeb.Features.Order;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Features.Order;

public class CreateOrderViewModelTests
{
    private static CreateOrderSchemeResponse Scheme(string name, string identifier, string schedule) =>
        new(Guid.NewGuid(), identifier, name, schedule, 100m, false, false, true, 0m, 0m, 0m, false, []);

    private static IReadOnlyList<CreateOrderScheduleGroupResponse> Groups() =>
    [
        new("Monthly", [Scheme("Microbiology", "PT200", "Monthly"), Scheme("Chemistry", "PT300", "Monthly")]),
        new("Quarterly", [Scheme("Water quality", "PT400", "Quarterly")])
    ];

    [Fact]
    public void Filter_BlankSearch_ReturnsEveryGroupUnchanged()
    {
        var groups = Groups();

        Assert.Same(groups, CreateOrderSearch.Filter(groups, null));
        Assert.Same(groups, CreateOrderSearch.Filter(groups, "   "));
    }

    [Fact]
    public void Filter_MatchesNameCaseInsensitivelyAndDropsEmptyGroups()
    {
        var result = CreateOrderSearch.Filter(Groups(), "WATER");

        var group = Assert.Single(result);
        Assert.Equal("Quarterly", group.Schedule);
        Assert.Equal("Water quality", Assert.Single(group.Schemes).Name);
    }

    [Fact]
    public void Filter_MatchesIdentifierAndSchedule()
    {
        Assert.Equal("Chemistry", Assert.Single(Assert.Single(CreateOrderSearch.Filter(Groups(), "pt300")).Schemes).Name);
        Assert.Equal(2, Assert.Single(CreateOrderSearch.Filter(Groups(), "monthly")).Schemes.Count);
    }

    [Fact]
    public void Filter_EveryWordMustMatch_InAnyOrder()
    {
        Assert.Single(CreateOrderSearch.Filter(Groups(), "quality water"));
        Assert.Empty(CreateOrderSearch.Filter(Groups(), "water chemistry"));
    }

    [Fact]
    public void ViewModel_VisibleGroups_AppliesTheSearchTerm()
    {
        var model = new CreateOrderViewModel { YearSlug = "current", Order = FakeCreateOrderApiClient.SampleOrder(), Search = "zzz" };

        Assert.Empty(model.VisibleGroups);
        Assert.Single(new CreateOrderViewModel { YearSlug = "current", Order = FakeCreateOrderApiClient.SampleOrder(), Search = "micro" }.VisibleGroups);
    }

    [Fact]
    public void FormatMoney_UsesCurrencySymbolAndTwoDecimalPlaces()
    {
        var model = new CreateOrderViewModel { YearSlug = "next", Order = FakeCreateOrderApiClient.SampleOrder() };

        Assert.Equal("£1,234.50", model.FormatMoney(1234.5m));
        Assert.Equal("£0.00", model.FormatMoney(0m));
    }
}
