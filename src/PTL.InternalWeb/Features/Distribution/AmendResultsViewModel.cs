using PTL.Contracts.Distribution;

namespace PTL.InternalWeb.Features.Distribution;

// Amend Results (legacy Scheduling/AmendResults.aspx). Inputs are named with AmendResultsFieldKeys
// so a validation message lands on its own cell; Values holds the posted text when redisplaying.
public sealed class AmendResultsViewModel
{
    public required int YearId { get; init; }
    public required int MonthId { get; init; }
    public required Guid MonthlyDistributionSchemeId { get; init; }
    public required AmendResultsResponse Page { get; init; }
    public IReadOnlyDictionary<string, string> Values { get; init; } = new Dictionary<string, string>();

    // Legacy Audit.aspx, shown when an audited value changed.
    public bool IsAuditStep { get; set; }
    public string? AuditObjectName { get; set; }
    public string? PendingCommand { get; set; }
    public string? AuditWhy { get; set; }

    public string Value(string name, string stored) => Values.TryGetValue(name, out var posted) ? posted : stored;

    public bool IsRange(AmendItemResponse resultItem)
    {
        ArgumentNullException.ThrowIfNull(resultItem);
        var name = AmendResultsFieldKeys.Range(resultItem.ItemId);
        return Values.ContainsKey(name + "_shown") ? Values.ContainsKey(name) : resultItem.IsRange;
    }
}
