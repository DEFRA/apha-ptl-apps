namespace PTL.ExternalWeb.Features.Distribution;

public sealed class CurrentDistributionsViewModel
{
    public IReadOnlyList<CurrentDistributionRowViewModel> Distributions { get; init; } = [];

    public int TotalCount { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 15;

    public string SortBy { get; init; } = string.Empty;

    public string SortDirection { get; init; } = "asc";
}

public sealed class CurrentDistributionRowViewModel
{
    public required Guid MonthlyDistributionSchemeId { get; init; }
    public required Guid DistributionParticipantId { get; init; }
    public required string DistributionReference { get; init; }
    public required string SchemeCode { get; init; }
    public required string SchemeName { get; init; }
    public required DateTime DistributionDate { get; init; }
    public required DateTime DeadlineDate { get; init; }
    public required bool HasResults { get; init; }
    public required bool Submitted { get; init; }

    // Legacy DistributionsCurrent.GetStatus: Submitted, else In Progress when any result exists, else No.
    public string Status
    {
        get
        {
            if (Submitted)
            {
                return "Submitted";
            }

            return HasResults ? "In Progress" : "No";
        }
    }

    public string StatusTagClass => Status switch
    {
        "Submitted" => "govuk-tag govuk-tag--green",
        "In Progress" => "govuk-tag govuk-tag--light-blue",
        _ => "govuk-tag govuk-tag--grey"
    };
}
