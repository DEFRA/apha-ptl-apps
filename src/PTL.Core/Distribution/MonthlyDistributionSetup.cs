using System.Globalization;

namespace PTL.Core.Distribution;

// sppSetupMonthlyDistribution result set 2 - one row per scheme distributed in the month. Nullable
// because legacy read it through SafeDataReader, which turns a NULL into the type's default.
public sealed class MonthSetupSchemeRow
{
    public Guid SchemeId { get; set; }
    public int? WeekNumber { get; set; }
    public int? DeadlineDays { get; set; }
    public bool? CommentsRequired { get; set; }
    public int? DistributionReference { get; set; }
    public string? ScheduleCode { get; set; }
    public int? DayOffset { get; set; }
    public string? SchemeName { get; set; }
    public string? SchemeIdentifier { get; set; }
    public int? NumberOfSamples { get; set; }
    public bool? IsAsAvailable { get; set; }
    public bool? StoreRatings { get; set; }
    public bool? CombinedPackaging { get; set; }
    public DateTime? VersionDate { get; set; }
}

// sppSetupMonthlyDistribution result set 3 - the participants enrolled on those schemes.
public sealed class MonthSetupParticipantRow
{
    public Guid SchemeId { get; set; }
    public Guid ParticipantId { get; set; }
    public string? LabCode { get; set; }
    public string? LabName { get; set; }
    public int? NumberOfSetsRequired { get; set; }
    public Guid ContractId { get; set; }
}

public sealed record MonthSetupPlan(
    Guid MonthlyDistributionId,
    int YearId,
    int MonthId,
    IReadOnlyList<MonthlyDistributionSchemeEntity> Schemes,
    IReadOnlyList<DistributionParticipantEntity> Participants);

public delegate MonthSetupPlan MonthSetupPlanner(IReadOnlyList<MonthSetupSchemeRow> schemes, IReadOnlyList<MonthSetupParticipantRow> participants);

// Legacy SetupMonthlyDistribution.DataPortal_Execute: the posting, deadline and results dates of
// every scheme are worked out from the first Friday before the month's first Monday.
public static class MonthlyDistributionPlanner
{
    // Legacy: results are due 7 days after the deadline, 14 more when external comments are required.
    private const int ResultsIssueDays = 7;
    private const int ExternalCommentsDays = 14;
    private const int DaysInWeek = 7;

    public static MonthSetupPlan Build(
        int yearId, int monthId, IReadOnlyList<MonthSetupSchemeRow> schemeRows, IReadOnlyList<MonthSetupParticipantRow> participantRows)
    {
        ArgumentNullException.ThrowIfNull(schemeRows);
        ArgumentNullException.ThrowIfNull(participantRows);

        var monthlyDistributionId = Guid.NewGuid();
        var firstFriday = FirstFriday(yearId, monthId);

        var schemes = new List<MonthlyDistributionSchemeEntity>(schemeRows.Count);
        var schemesBySchemeId = new Dictionary<Guid, MonthlyDistributionSchemeEntity>();
        foreach (var row in schemeRows)
        {
            var scheme = ToScheme(monthlyDistributionId, yearId, monthId, firstFriday, row);
            schemes.Add(scheme);
            schemesBySchemeId.TryAdd(scheme.SchemeId, scheme);
        }

        var participants = new List<DistributionParticipantEntity>(participantRows.Count);
        foreach (var row in participantRows)
        {
            if (!schemesBySchemeId.TryGetValue(row.SchemeId, out var scheme))
            {
                continue;
            }

            // Legacy NewParticipant: no individual deadline, no reason, not an additional participant.
            participants.Add(new DistributionParticipantEntity
            {
                DistributionParticipantId = Guid.NewGuid(),
                ParticipantId = row.ParticipantId,
                LabCode = row.LabCode ?? string.Empty,
                LabName = row.LabName ?? string.Empty,
                MonthlyDistributionSchemeId = scheme.MonthlyDistributionSchemeId,
                DeadlineDate = null,
                SchemeDeadlineDate = scheme.DeadlineDate,
                DeadlineChangeReason = string.Empty,
                ContractId = row.ContractId,
                AdditionalParticipant = false,
                NumberOfSetsRequired = row.NumberOfSetsRequired ?? 0,
            });
        }

        return new MonthSetupPlan(monthlyDistributionId, yearId, monthId, schemes, participants);
    }

    // Legacy: the first Monday that falls inside the month, then the Friday three days before it.
    private static DateTime FirstFriday(int yearId, int monthId)
    {
        var firstOfMonth = new DateTime(yearId, monthId, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var firstMonday = firstOfMonth.AddDays(1 - (int)firstOfMonth.DayOfWeek);
        if (firstMonday.Month != monthId)
        {
            firstMonday = firstMonday.AddDays(DaysInWeek);
        }

        return firstMonday.AddDays(-3);
    }

    private static MonthlyDistributionSchemeEntity ToScheme(
        Guid monthlyDistributionId, int yearId, int monthId, DateTime firstFriday, MonthSetupSchemeRow row)
    {
        var weekOffset = DaysInWeek * ((row.WeekNumber ?? 0) - 1);
        var dayOffset = row.DayOffset ?? 0;
        var postingDate = firstFriday.AddDays(dayOffset + weekOffset);

        var overseasPostingDate = postingDate;
        if (row.CombinedPackaging == true)
        {
            // Combined packaging: the overseas posting date is a Tuesday, a week later when the
            // posting day is later than that.
            overseasPostingDate = firstFriday.AddDays(-3 + weekOffset);
            if (dayOffset > 4)
            {
                overseasPostingDate = overseasPostingDate.AddDays(DaysInWeek);
            }
        }

        var deadlineDate = postingDate.AddDays(row.DeadlineDays ?? 0);
        var resultsIssueTargetDate = deadlineDate.AddDays(ResultsIssueDays);
        if (row.CommentsRequired == true)
        {
            resultsIssueTargetDate = resultsIssueTargetDate.AddDays(ExternalCommentsDays);
        }

        var isAsAvailable = row.IsAsAvailable == true;
        return new MonthlyDistributionSchemeEntity
        {
            MonthlyDistributionSchemeId = Guid.NewGuid(),
            MonthlyDistributionId = monthlyDistributionId,
            SchemeId = row.SchemeId,
            YearId = yearId,
            MonthId = monthId,
            SchemeIdentifier = row.SchemeIdentifier ?? string.Empty,
            SchemeName = row.SchemeName ?? string.Empty,
            DistributionReference = (row.DistributionReference ?? 0).ToString(CultureInfo.InvariantCulture),
            DistributionReferenceSuffix = string.Empty,
            ScheduleCode = row.ScheduleCode ?? string.Empty,
            DistributionDate = postingDate,
            OverseasPostingDate = overseasPostingDate,
            DeadlineDate = deadlineDate,
            ResultsIssueTargetDate = resultsIssueTargetDate,
            Comments = string.Empty,
            HasIntendedResults = false,
            HasSampleNumbersDefined = false,

            // Legacy NewMonthlyDistributionScheme: a scheme distributed "as available" starts cancelled.
            IsCancelled = isAsAvailable,
            IsAsAvailable = isAsAvailable,
            StoreRatings = row.StoreRatings == true,
            SchemeVersionDate = row.VersionDate ?? default,
        };
    }
}
