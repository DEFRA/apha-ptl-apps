using PTL.Contracts.Distribution;
using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

// A small Amend Results scheme (legacy AmendedResults.MonthlyDistributionScheme) shared by the service
// and controller tests.
internal static class AmendResultsTestData
{
    public static readonly Guid SchemeId = Guid.Parse("a0000000-0000-0000-0000-000000000001");

    public static readonly Guid Culture = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    public static readonly Guid NoSamples = Guid.Parse("a1000000-0000-0000-0000-000000000002");
    public static readonly Guid Serology = Guid.Parse("a1000000-0000-0000-0000-000000000003");

    public static readonly Guid MethodRegex = Guid.Parse("a2000000-0000-0000-0000-000000000001");
    public static readonly Guid MethodList = Guid.Parse("a2000000-0000-0000-0000-000000000002");
    public static readonly Guid ResultSingle = Guid.Parse("a3000000-0000-0000-0000-000000000001");
    public static readonly Guid ResultRange = Guid.Parse("a3000000-0000-0000-0000-000000000002");
    public static readonly Guid ResultSerology = Guid.Parse("a3000000-0000-0000-0000-000000000003");
    public static readonly Guid ResultTypeId = Guid.Parse("a3100000-0000-0000-0000-000000000001");
    public static readonly Guid Category = Guid.Parse("a4000000-0000-0000-0000-000000000001");

    public static readonly Guid SampleA = Guid.Parse("a5000000-0000-0000-0000-000000000001");
    public static readonly Guid SampleB = Guid.Parse("a5000000-0000-0000-0000-000000000002");
    public static readonly Guid SampleC = Guid.Parse("a5000000-0000-0000-0000-000000000003");
    public static readonly Guid SampleTestId = Guid.Parse("a5100000-0000-0000-0000-000000000001");

    public static readonly Guid Lab1 = Guid.Parse("a6000000-0000-0000-0000-000000000001");
    public static readonly Guid Lab2 = Guid.Parse("a6000000-0000-0000-0000-000000000002");
    public static readonly Guid Lab3 = Guid.Parse("a6000000-0000-0000-0000-000000000003");
    public static readonly Guid Receipt1 = Guid.Parse("a6100000-0000-0000-0000-000000000001");
    public static readonly Guid Receipt3 = Guid.Parse("a6100000-0000-0000-0000-000000000003");
    public static readonly Guid SampleRating1 = Guid.Parse("a6200000-0000-0000-0000-000000000001");
    public static readonly Guid SampleRating2 = Guid.Parse("a6200000-0000-0000-0000-000000000002");

    public static readonly Guid RatingMinus1 = Guid.Parse("a7000000-0000-0000-0000-000000000001");
    public static readonly Guid Rating0 = Guid.Parse("a7000000-0000-0000-0000-000000000002");
    public static readonly Guid Rating1 = Guid.Parse("a7000000-0000-0000-0000-000000000003");
    public static readonly Guid Rating2 = Guid.Parse("a7000000-0000-0000-0000-000000000004");
    public static readonly Guid RatingNotScored = Guid.Parse("a7000000-0000-0000-0000-000000000005");
    public static readonly Guid RatingOther = Guid.Parse("a7000000-0000-0000-0000-000000000006");

    public const string Title = "D1a/S1 BT: Brucella";
    public const string AuditObject = "Amended Results for D1a/S1 BT: Brucella";

    public static List<AmendRatingOption> RatingOptions() =>
    [
        new(RatingMinus1, "-1"), new(Rating0, "0"), new(Rating1, "1"), new(Rating2, "2"),
        new(RatingNotScored, "Not Scored"), new(RatingOther, "N/A"),
    ];

    // Culture: regex + list method items, a single and a range result item, a category, samples "S02" and "S01".
    // NoSamples: excluded from the page. Serology: not assessment-submitted, shares "S02" and adds "S10".
    // Lab1 returned everything; Lab2 is a nil return (with a stray rating row); Lab3 only has an assessment.
    public static AmendResultsSource Source(bool storeRatings = true)
    {
        var source = new AmendResultsSource
        {
            MonthlyDistributionSchemeId = SchemeId,
            SchemeName = "Brucella",
            SchemeIdentifier = "BT",
            DistributionReference = "D1",
            DistributionReferenceSuffix = "a",
            ScheduleCode = "S1",
            StoreRatings = storeRatings,
            RequiresAssessment = true,
        };

        var culture = new AmendSourceTest { TestId = Culture, Name = "Culture", IsAssessmentSubmitted = true };
        culture.MethodItems.Add(new(MethodRegex, "Titre", "^[0-9]+$", "Numeric"));
        culture.MethodItems.Add(new(MethodList, "Kit", "Pos|Neg", "List"));
        culture.ResultItems.Add(new(ResultSingle, "Count", "^[0-9]+$", "Numeric", false, ResultTypeId, 3));
        culture.ResultItems.Add(new(ResultRange, "Span", "^[0-9]+$", "Numeric", true, ResultTypeId, 4));
        culture.CategoryItems.Add(new TabulationSourceItem(Category, "Clinical"));
        culture.Samples.Add(new(SampleA, "S02"));
        culture.Samples.Add(new(SampleB, "S01"));

        var noSamples = new AmendSourceTest { TestId = NoSamples, Name = "Unused" };
        noSamples.MethodItems.Add(new(Guid.NewGuid(), "Orphan", "^.*$", "Any"));

        var serology = new AmendSourceTest { TestId = Serology, Name = "Serology" };
        serology.ResultItems.Add(new(ResultSerology, "ELISA", "Pos|Neg", "List"));
        serology.Samples.Add(new(SampleA, "S02"));
        serology.Samples.Add(new(SampleC, "S10"));

        source.Tests.AddRange([culture, noSamples, serology]);

        source.IntendedMethodResults[MethodRegex] = "5";
        source.IntendedResultItemResults[(SampleA, ResultSingle)] = ("10", "");
        source.IntendedResultItemResults[(SampleA, ResultRange)] = ("1", "9");
        source.IntendedResultItemResults[(SampleC, ResultSerology)] = ("Pos", "");

        var lab1 = new AmendSourceParticipant { DistributionParticipantId = Lab1, LabCode = "L001", NilReturnReason = "stale" };
        lab1.MethodResults[MethodRegex] = "7";
        lab1.MethodResults[MethodList] = "Pos";
        lab1.ResultItemResults[(SampleA, ResultSingle)] = "12";
        lab1.ResultItemResults[(SampleA, ResultRange)] = "3";
        lab1.CategoryResults[(SampleA, Category)] = (3, SampleTestId);
        lab1.Assessments[(Culture, SampleA)] = (Receipt1, "ok", 2);
        lab1.Assessments[(Serology, SampleA)] = (Guid.NewGuid(), "serology", 2);
        lab1.Ratings[SampleA] = (SampleRating1, Rating2);

        var lab2 = new AmendSourceParticipant { DistributionParticipantId = Lab2, LabCode = "L002", NilReturnReason = "Lost" };
        lab2.Ratings[SampleA] = (SampleRating2, Rating0);

        var lab3 = new AmendSourceParticipant { DistributionParticipantId = Lab3, LabCode = "L003" };
        lab3.Assessments[(Culture, SampleB)] = (Receipt3, "late", 4);

        source.Participants.AddRange([lab1, lab2, lab3]);
        return source;
    }

    public static AmendResultsSaveRequest Request(
        IReadOnlyList<AmendIntendedValueRequest>? intendedMethods = null,
        IReadOnlyList<AmendIntendedValueRequest>? intendedResults = null,
        IReadOnlyList<AmendRangeRequest>? ranges = null,
        IReadOnlyList<AmendParticipantValueRequest>? methodResults = null,
        IReadOnlyList<AmendParticipantValueRequest>? resultItemResults = null,
        IReadOnlyList<AmendParticipantScoreRequest>? categoryResults = null,
        IReadOnlyList<AmendAssessmentRequest>? assessments = null,
        IReadOnlyList<AmendNilReturnRequest>? nilReturns = null,
        IReadOnlyList<AmendRatingRequest>? ratings = null,
        string? auditWhy = null) =>
        new(intendedMethods ?? [], intendedResults ?? [], ranges ?? [], methodResults ?? [], resultItemResults ?? [],
            categoryResults ?? [], assessments ?? [], nilReturns ?? [], ratings ?? [], auditWhy);

    // Posts every stored value back unchanged (what the page does when nothing is edited).
    public static AmendResultsSaveRequest UnchangedRequest() => Request(
        [new(MethodRegex, Guid.Empty, "5", null)],
        [new(ResultSingle, SampleA, "10", ""), new(ResultRange, SampleA, "1", "9"), new(ResultSerology, SampleC, "Pos", null)],
        [new(ResultSingle, false), new(ResultRange, true)],
        [new(Lab1, MethodRegex, Guid.Empty, "7"), new(Lab1, MethodList, Guid.Empty, "Pos")],
        [new(Lab1, ResultSingle, SampleA, "12"), new(Lab1, ResultRange, SampleA, "3")],
        [new(Lab1, Category, SampleA, 3)],
        [new(Lab1, Culture, SampleA, "ok", 2), new(Lab3, Culture, SampleB, "late", 4)],
        [new(Lab2, "Lost")],
        [new(Lab1, SampleA, Rating2)]);
}
