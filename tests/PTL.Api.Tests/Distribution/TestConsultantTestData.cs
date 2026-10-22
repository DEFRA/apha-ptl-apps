using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

// A small distribution as spgMonthlyDistributionSchemeForTestConsultant returns it: one test with a method item, a
// result item and two samples, and two participants (the second a nil return).
internal static class TestConsultantTestData
{
    public static readonly Guid SchemeId = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    public static readonly Guid ConsultantId = Guid.Parse("a2000000-0000-0000-0000-000000000001");
    public static readonly Guid OtherConsultantId = Guid.Parse("a2000000-0000-0000-0000-000000000002");
    public static readonly Guid TestId = Guid.Parse("a3000000-0000-0000-0000-000000000001");
    public static readonly Guid MethodId = Guid.Parse("a4000000-0000-0000-0000-000000000001");
    public static readonly Guid ResultItemId = Guid.Parse("a5000000-0000-0000-0000-000000000001");
    public static readonly Guid SampleOne = Guid.Parse("a6000000-0000-0000-0000-000000000001");
    public static readonly Guid SampleTwo = Guid.Parse("a6000000-0000-0000-0000-000000000002");
    public static readonly Guid ParticipantOne = Guid.Parse("a7000000-0000-0000-0000-000000000001");
    public static readonly Guid ParticipantTwo = Guid.Parse("a7000000-0000-0000-0000-000000000002");

    public static TestConsultantTabulationEntity Entity(
        bool submitted = false, string comments = "", string author = "", bool hasRatings = false, bool withParticipantInformation = true)
    {
        var source = new TabulationSource
        {
            TabulationId = SchemeId,
            DistributionTabulationId = SchemeId,
            Distribution = "D1a/S1",
            SchemeIdentifier = "BT",
            SchemeName = "Brucella",
            DistributionDate = new DateTime(2026, 5, 3),
            Comments = "<p>Scheme comments</p>",
            StandardTabulationText = "<p>Standard text</p>",
            TestConsultantComments = comments,
            TestConsultantCommentsAuthor = author,
            HasRatings = hasRatings,
        };

        var test = new TabulationSourceTest { TestId = TestId, Name = "PCR" };
        test.Samples.Add(new(SampleOne, null, "26/1", "Positive"));
        test.Samples.Add(new(SampleTwo, null, "26/2", "Negative"));
        test.MethodItems.Add(new(MethodId, "Method Details"));
        test.ResultItems.Add(new(ResultItemId, "Result"));
        source.Tests.Add(test);

        var first = new TabulationSourceParticipant { ParticipantId = ParticipantOne, LabCode = "L001" };
        first.MethodResults[MethodId] = "In-house PCR";
        first.ResultItemResults[(SampleOne, ResultItemId)] = "Positive";
        first.ResultItemResults[(SampleTwo, ResultItemId)] = "Negative";
        first.SampleRatings[SampleOne] = "2";
        first.SampleRatings[SampleTwo] = "2";
        source.Participants.Add(first);
        source.Participants.Add(new TabulationSourceParticipant { ParticipantId = ParticipantTwo, LabCode = "L002", NilReturnReason = "Late" });

        source.IntendedMethodResults[MethodId] = "PCR";
        source.IntendedResultItemResults[(SampleOne, ResultItemId)] = ("Positive", string.Empty);

        var entity = new TestConsultantTabulationEntity { Source = source, IsCommentSubmitted = submitted };
        entity.SampleDetails.Add(new TestConsultantSampleInformation("26/1", "Positive", "Details of sample one"));
        if (withParticipantInformation)
        {
            entity.Participants.Add(new TestConsultantParticipantInformation("L001", true, string.Empty));
            entity.Participants.Add(new TestConsultantParticipantInformation("L002", false, "Postal delay"));
        }

        return entity;
    }

    public static TestConsultantSchemeState State(
        Guid? first = null, bool submitted = false, string comments = "", Guid? author = null) =>
        new(first ?? ConsultantId, null, null, submitted, comments, author ?? Guid.Empty);
}
