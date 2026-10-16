using PTL.Contracts.Distribution;

namespace PTL.InternalWeb.Features.Distribution;

// Monthly Distributions - Tabulations screen - legacy Scheduling/Tabulations.aspx (sitemap title
// "Tabulations"; page title "Monthly Distributions - <MonthName> <Year>"). The scheme/tabulation/
// action shape is the API contract itself - there is no InternalWeb-only shape to map it to.
public sealed class TabulationsViewModel
{
    public int YearId { get; init; }

    public int MonthId { get; init; }

    // Legacy "<MonthName> <Year>".
    public string MonthDescription { get; init; } = string.Empty;

    public IReadOnlyList<TabulationsSchemeResponse> Schemes { get; init; } = [];

    // Sends chosen but not saved yet; posted back as hidden "staged" values "{schemeId}:{command}".
    public IReadOnlyList<TabulationStagedSendRequest> Staged { get; init; } = [];
}
