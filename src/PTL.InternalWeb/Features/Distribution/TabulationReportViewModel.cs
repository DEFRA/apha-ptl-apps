using PTL.Contracts.Distribution;

namespace PTL.InternalWeb.Features.Distribution;

// Tabulation / Issued Tabulation. PublishedTabulationId is set for the Issued Tabulation.
public sealed class TabulationReportViewModel
{
    public required int YearId { get; init; }
    public required int MonthId { get; init; }
    public required Guid TabulationId { get; init; }
    public Guid? PublishedTabulationId { get; init; }
    public required TabulationReportResponse Report { get; init; }
}

public sealed class PublishedTabulationsViewModel
{
    public required int YearId { get; init; }
    public required int MonthId { get; init; }
    public required Guid TabulationId { get; init; }
    public required PublishedTabulationsResponse Page { get; init; }
}

// Assessment Results / Issued Assessment Results. PublishedTabulationId is set for the issued chart.
public sealed class AssessmentResultsViewModel
{
    public required int YearId { get; init; }
    public required int MonthId { get; init; }
    public required Guid TabulationId { get; init; }
    public Guid? PublishedTabulationId { get; init; }
    public required Guid TestId { get; init; }
    public required AssessmentResultsResponse Results { get; init; }

    // Tallest bar, used to scale the chart; at least 1 so an empty chart still draws its axes.
    public int MaxFrequency => Math.Max(1, Results.Scores.Count == 0 ? 0 : Results.Scores.Max(s => s.Frequency));
}
