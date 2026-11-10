using PTL.Contracts.CreateOrder;
using PTL.ExternalWeb.ViewSupport;

namespace PTL.ExternalWeb.Features.Order;

// Legacy kept one sorted column per scheme table (participating, each schedule, summary). Here each choice is a repeated
// "sort" query value, "column|direction|table", so the page stays a plain GET and survives the in-place order updates.
public sealed class OrderSortState
{
    public const string ParticipatingTable = "participating";
    public const string SummaryTable = "summary";

    private const string CodeColumn = "code";
    private const string NameColumn = "name";
    private const string Ascending = "asc";
    private const string Descending = "desc";
    private const int MaxChoices = 40;
    private const int MaxTableLength = 200;

    public static readonly OrderSortState Empty = new([]);

    private readonly Dictionary<string, (string Column, string Direction)> choices;

    private OrderSortState(Dictionary<string, (string Column, string Direction)> choices) => this.choices = choices;

    public static string ScheduleTable(string schedule) => "schedule:" + schedule;

    // Anything that is not a known column and direction is dropped, so a hand-edited address can only fall back to the default order.
    public static OrderSortState Parse(IEnumerable<string?>? values)
    {
        var parsed = new Dictionary<string, (string Column, string Direction)>(StringComparer.Ordinal);
        foreach (var value in values ?? [])
        {
            var parts = value?.Split('|', 3);
            if (parts is { Length: 3 }
                && parts[0] is CodeColumn or NameColumn
                && parts[1] is Ascending or Descending
                && parts[2].Length is > 0 and <= MaxTableLength
                && (parsed.Count < MaxChoices || parsed.ContainsKey(parts[2])))
            {
                parsed[parts[2]] = (parts[0], parts[1]);
            }
        }

        return new OrderSortState(parsed);
    }

    // The explicit choices, as they go back into links and form fields.
    public IReadOnlyList<string> Values => [.. choices.Select(c => $"{c.Value.Column}|{c.Value.Direction}|{c.Key}")];

    // An empty column means the table keeps its default order. Legacy sorted the pending-order summary by name from the start.
    public (string Column, string Direction) For(string table)
    {
        if (choices.TryGetValue(table, out var choice))
        {
            return choice;
        }

        return table == SummaryTable ? (NameColumn, Ascending) : (string.Empty, string.Empty);
    }

    // Legacy toggled: an unsorted or descending column sorts ascending, an ascending one descending.
    public OrderSortState Toggled(string table, string column)
    {
        var (currentColumn, currentDirection) = For(table);
        var next = new Dictionary<string, (string Column, string Direction)>(choices, StringComparer.Ordinal)
        {
            [table] = (column, currentColumn == column && currentDirection == Ascending ? Descending : Ascending)
        };
        return new OrderSortState(next);
    }

    public IReadOnlyList<CreateOrderSchemeResponse> Apply(string table, IReadOnlyList<CreateOrderSchemeResponse> schemes)
    {
        var (column, direction) = For(table);
        if (column.Length == 0)
        {
            return schemes;
        }

        Func<CreateOrderSchemeResponse, string> key = column == CodeColumn ? s => s.Identifier : s => s.Name;
        return direction == Descending
            ? [.. schemes.OrderByDescending(key, StringComparer.OrdinalIgnoreCase)]
            : [.. schemes.OrderBy(key, StringComparer.OrdinalIgnoreCase)];
    }

    // Column headers for one table; urlForSort turns the sort values a header would apply into that page's address.
    public SortableHeaderContext Headers(string table, Func<IReadOnlyList<string>, string> urlForSort)
    {
        var (column, direction) = For(table);
        return new SortableHeaderContext(column, direction, clicked => urlForSort(Toggled(table, clicked).Values));
    }
}
