using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

// Shared builders for the Monthly Distributions - Tabulations tests. The baseline scheme is the
// plain "deadline passed, nothing sent yet" state so a test only configures what it exercises.
internal static class TabulationsTestData
{
    public static readonly DateTime Now = new(2026, 5, 12, 12, 0, 0);

    public static DateTime Today => Now.Date;

    public static TabulationEntity Tabulation(string name = "Tab One", int publishState = 0, int publishCount = 0, Guid? id = null) =>
        new() { TabulationId = id ?? Guid.NewGuid(), Name = name, PublishState = publishState, PublishCount = publishCount };

    // Accounted + submitted by default; the flags switch it into the other legacy states.
    public static TabulationAssessmentStatusEntity Status(bool assessmentSubmitted = true, bool accounted = true) =>
        new()
        {
            AssessmentSubmitted = assessmentSubmitted ? Today : null,
            SubmissionDate = accounted ? Today : null,
        };

    public static TabulationsSchemeEntity NewScheme(Action<TabulationsSchemeEntity>? configure = null)
    {
        var scheme = new TabulationsSchemeEntity
        {
            MonthlyDistributionSchemeId = Guid.NewGuid(),
            SchemeIdentifier = "BT",
            SchemeName = "Brucella",
            DistributionReference = "D1",
            DistributionReferenceSuffix = "a",
            ScheduleCode = "S1",
            DeadlineDate = Today.AddDays(-1),
            ResultsIssueTargetDate = Today.AddDays(30),
            HasIntendedResults = true,
            Tabulations = [Tabulation()],
        };
        configure?.Invoke(scheme);
        return scheme;
    }

    public static TabulationSchemeActions Compute(TabulationsSchemeEntity scheme) => TabulationActionRules.Compute(scheme, Now);

    // Pins the local zone to UTC so the service's local "today" matches Now on any machine.
    public static TimeProvider Clock() => new UtcFixedTimeProvider(new DateTimeOffset(Now, TimeSpan.Zero));

    private sealed class UtcFixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
