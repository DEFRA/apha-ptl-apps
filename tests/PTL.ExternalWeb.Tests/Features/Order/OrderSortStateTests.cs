using PTL.Contracts.CreateOrder;
using PTL.ExternalWeb.Features.Order;

namespace PTL.ExternalWeb.Tests.Features.Order;

public class OrderSortStateTests
{
    private static CreateOrderSchemeResponse Scheme(string identifier, string name) =>
        new(Guid.NewGuid(), identifier, name, "Monthly", 100m, false, false, true, 0m, 0m, 0m, false, []);

    private static readonly IReadOnlyList<CreateOrderSchemeResponse> Schemes =
        [Scheme("PT300", "beta"), Scheme("PT100", "Gamma"), Scheme("PT200", "Alpha")];

    private static string[] Names(IEnumerable<CreateOrderSchemeResponse> schemes) => [.. schemes.Select(s => s.Name)];

    [Fact]
    public void ATableWithNoChoice_KeepsItsOrderAndShowsNoSort()
    {
        var state = OrderSortState.Empty;

        Assert.Same(Schemes, state.Apply(OrderSortState.ParticipatingTable, Schemes));
        Assert.Equal(("", ""), state.For(OrderSortState.ParticipatingTable));
    }

    [Fact]
    public void ThePendingOrderSummary_StartsOnSchemeNameAscending()
    {
        Assert.Equal(("name", "asc"), OrderSortState.Empty.For(OrderSortState.SummaryTable));
        Assert.Equal(["Alpha", "beta", "Gamma"], Names(OrderSortState.Empty.Apply(OrderSortState.SummaryTable, Schemes)));
    }

    [Fact]
    public void Toggled_SortsAscendingFirstThenFlipsEachClick()
    {
        var table = OrderSortState.ParticipatingTable;

        var first = OrderSortState.Empty.Toggled(table, "code");
        var second = first.Toggled(table, "code");
        var third = second.Toggled(table, "code");

        Assert.Equal(("code", "asc"), first.For(table));
        Assert.Equal(("code", "desc"), second.For(table));
        Assert.Equal(("code", "asc"), third.For(table));
    }

    [Fact]
    public void ChangingColumn_StartsAscendingAndOnlyOneColumnIsSortedPerTable()
    {
        var table = OrderSortState.ParticipatingTable;

        var state = OrderSortState.Empty.Toggled(table, "code").Toggled(table, "code").Toggled(table, "name");

        Assert.Equal(("name", "asc"), state.For(table));
        Assert.Single(state.Values);
    }

    [Fact]
    public void EachTableKeepsItsOwnSort()
    {
        var monthly = OrderSortState.ScheduleTable("Monthly");
        var quarterly = OrderSortState.ScheduleTable("Quarterly");

        var state = OrderSortState.Empty.Toggled(monthly, "name").Toggled(quarterly, "code").Toggled(quarterly, "code");

        Assert.Equal(("name", "asc"), state.For(monthly));
        Assert.Equal(("code", "desc"), state.For(quarterly));
        Assert.Equal(("", ""), state.For(OrderSortState.ParticipatingTable));
    }

    [Theory]
    [InlineData("code", "asc", new[] { "PT100", "PT200", "PT300" })]
    [InlineData("code", "desc", new[] { "PT300", "PT200", "PT100" })]
    public void Apply_SortsByCode(string column, string direction, string[] expected)
    {
        var state = OrderSortState.Parse([$"{column}|{direction}|participating"]);

        Assert.Equal(expected, state.Apply(OrderSortState.ParticipatingTable, Schemes).Select(s => s.Identifier));
    }

    [Theory]
    [InlineData("asc", new[] { "Alpha", "beta", "Gamma" })]
    [InlineData("desc", new[] { "Gamma", "beta", "Alpha" })]
    public void Apply_SortsByNameIgnoringCase(string direction, string[] expected)
    {
        var state = OrderSortState.Parse([$"name|{direction}|participating"]);

        Assert.Equal(expected, Names(state.Apply(OrderSortState.ParticipatingTable, Schemes)));
    }

    [Fact]
    public void Values_RoundTripThroughParse_AndAScheduleNameMayContainTheSeparator()
    {
        var table = OrderSortState.ScheduleTable("Monthly | Special");
        var state = OrderSortState.Empty.Toggled(table, "name");

        var parsed = OrderSortState.Parse(state.Values);

        Assert.Equal(("name", "asc"), parsed.For(table));
    }

    [Theory]
    [InlineData("price|asc|participating")]
    [InlineData("code|sideways|participating")]
    [InlineData("code|asc|")]
    [InlineData("code|asc")]
    [InlineData("nonsense")]
    [InlineData("")]
    [InlineData(null)]
    public void Parse_IgnoresValuesThatAreNotAKnownColumnAndDirection(string? value)
    {
        var state = OrderSortState.Parse([value]);

        Assert.Empty(state.Values);
    }

    [Fact]
    public void Parse_LaterChoiceForTheSameTableWins_AndNullInputIsEmpty()
    {
        var state = OrderSortState.Parse(["code|asc|participating", "name|desc|participating"]);

        Assert.Equal(("name", "desc"), state.For(OrderSortState.ParticipatingTable));
        Assert.Empty(OrderSortState.Parse(null).Values);
    }

    [Fact]
    public void Parse_CapsTheNumberOfTables()
    {
        var values = Enumerable.Range(0, 100).Select(i => $"code|asc|schedule:{i}");

        Assert.Equal(40, OrderSortState.Parse(values).Values.Count);
    }

    [Fact]
    public void Headers_LinkToTheToggledSortAndReportTheCurrentOne()
    {
        var table = OrderSortState.ParticipatingTable;
        var state = OrderSortState.Empty.Toggled(table, "code");

        var headers = state.Headers(table, values => "/Order/CurrentYear?" + string.Join('&', values));
        var code = headers.Header("Scheme code", "code");
        var name = headers.Header("Scheme name", "name");

        Assert.Equal("asc", code.SortDirection);
        Assert.Equal("/Order/CurrentYear?code|desc|participating", code.Url);
        Assert.Equal("/Order/CurrentYear?name|asc|participating", name.Url);
    }
}
