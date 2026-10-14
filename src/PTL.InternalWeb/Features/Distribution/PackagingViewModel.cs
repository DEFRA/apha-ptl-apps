using PTL.Contracts.Distribution;

namespace PTL.InternalWeb.Features.Distribution;

// Sample Packaging screen - legacy Internal User/PackagingSheet.aspx. The packaging grid itself is
// the API contract (PackagingSheetResponse) - there is no InternalWeb-only shape to map it to.
public sealed class PackagingViewModel
{
#pragma warning disable S6964
    public int YearId { get; set; }

    public int MonthId { get; set; }
#pragma warning restore S6964

    public string MonthDescription { get; set; } = string.Empty;

    // The Packaging dropdown: null is "- Please Select -", otherwise 0/1/2.
    public int? Mode { get; set; }

    // The Week dropdown (Group Address and Participant Combined only): 0 is "- Please Select -".
#pragma warning disable S6964
    public int Week { get; set; }
#pragma warning restore S6964

    // Packed/Posted ticks as "{mode}:{week}:{distributionParticipantId}". Ticks made on another
    // option/week are carried in hidden fields so Save/Apply still persist them (legacy keeps every
    // fetched sheet in ViewState and saves them all).
    public List<string> PackedTicks { get; set; } = [];

    public List<string> PostedTicks { get; set; } = [];

    // The Print column's ticked rows (PackagingBlockResponse.PrintKey).
    public List<string> PrintKeys { get; set; } = [];

    public PackagingSheetResponse? Sheet { get; set; }

    // Legacy "Print Error" / mail-merge messages (LitConfirmPrint): the strong message, then the
    // detail underneath it (e.g. the reason a Customs document could not be produced).
    public string? Message { get; set; }

    public string? MessageDetail { get; set; }

    public string PrintTitle { get; set; } = string.Empty;

    public string? ModeError { get; set; }

    public string? WeekError { get; set; }

    public bool HasSelectionError => ModeError is not null || WeekError is not null;

    public bool ShowWeek => Mode is 0 or 1;

    public bool ShowCustoms => (Mode == 1 && Week > 0) || Mode == 2;

    public static string TickKey(int mode, int week, Guid distributionParticipantId) => $"{mode}:{week}:{distributionParticipantId}";

    public string RowTickKey(Guid distributionParticipantId) =>
        TickKey(Sheet?.Mode ?? 0, Sheet?.Week ?? 0, distributionParticipantId);

    public bool IsPacked(Guid distributionParticipantId) => PackedTicks.Contains(RowTickKey(distributionParticipantId));

    public bool IsPosted(Guid distributionParticipantId) => PostedTicks.Contains(RowTickKey(distributionParticipantId));

    // Ticks belonging to a sheet that is not the one on screen.
    public IEnumerable<string> CarriedTicks(IEnumerable<string> ticks)
    {
        var shown = new HashSet<string>();
        foreach (var scheme in Sheet?.Blocks.SelectMany(b => b.Schemes) ?? [])
        {
            foreach (var participant in scheme.Participants)
            {
                shown.Add(RowTickKey(participant.DistributionParticipantId));
            }

            if (scheme.DistributionParticipantId is { } id)
            {
                shown.Add(RowTickKey(id));
            }
        }

        return ticks.Where(t => !shown.Contains(t)).Distinct();
    }
}

public sealed class PackagingLabelsViewModel
{
    public int YearId { get; set; }

    public int MonthId { get; set; }

    public int Mode { get; set; }

    public int Week { get; set; }

    public string MonthDescription { get; set; } = string.Empty;

    public IReadOnlyList<PackagingLabelResponse> Labels { get; set; } = [];
}
