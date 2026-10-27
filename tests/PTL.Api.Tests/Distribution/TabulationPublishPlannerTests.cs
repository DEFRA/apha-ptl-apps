using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

// Legacy Published/PublishTabulation.vb: the order of the calls, the parameters each procedure gets and how the new published ids
// are linked back to the draft rows.
public class TabulationPublishPlannerTests
{
    private static readonly Guid TabulationId = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid SchemeId = Guid.Parse("a0000000-0000-0000-0000-000000000002");
    private static readonly Guid Sample1 = Guid.Parse("b0000000-0000-0000-0000-000000000001");
    private static readonly Guid Sample2 = Guid.Parse("b0000000-0000-0000-0000-000000000002");
    private static readonly Guid Test1 = Guid.Parse("c0000000-0000-0000-0000-000000000001");
    private static readonly Guid SampleTest1 = Guid.Parse("c1000000-0000-0000-0000-000000000001");
    private static readonly Guid ResultItem1 = Guid.Parse("d0000000-0000-0000-0000-000000000001");
    private static readonly Guid MethodItem1 = Guid.Parse("d1000000-0000-0000-0000-000000000001");
    private static readonly Guid Category1 = Guid.Parse("e0000000-0000-0000-0000-000000000001");
    private static readonly Guid Criterion1 = Guid.Parse("e1000000-0000-0000-0000-000000000001");
    private static readonly Guid Criterion2 = Guid.Parse("e1000000-0000-0000-0000-000000000002");
    private static readonly Guid Participant1 = Guid.Parse("f0000000-0000-0000-0000-000000000001");
    private static readonly Guid Assessor = Guid.Parse("f1000000-0000-0000-0000-000000000001");
    private static readonly Guid Receipt = Guid.Parse("f2000000-0000-0000-0000-000000000001");
    private static readonly Guid Attachment1 = Guid.Parse("99000000-0000-0000-0000-000000000001");

    private static readonly DateTime Created = new(2026, 10, 26, 11, 30, 0);

    private static TabulationPublishSource Source(bool requiresAssessment = true, bool hasRatings = true) =>
        new(
            new PublishHeader(
                TabulationId, SchemeId, "FINAL REPORT", "Brucella", "BT", requiresAssessment, "17208", "/BA", "PT0074",
                "Scheme comments", "Consultant comments", "Dr A", "QAU", new DateTime(2026, 5, 3), new DateTime(2025, 4, 1),
                hasRatings, false, true, true, false, "Standard text"),
            new PublishScheme(6, 2026, true, requiresAssessment),
            [new PublishSample(Sample1, "26/1", "Sample one"), new PublishSample(Sample2, "26/2", "Sample two")],
            [
                new PublishTest(
                    Test1, "Culture", new DateTime(2026, 6, 1), Assessor, "Ann Assessor",
                    [new PublishTestSample(Sample1, SampleTest1)],
                    [new PublishNamedItem(ResultItem1, "Isolate")],
                    [new PublishNamedItem(MethodItem1, "Medium")],
                    [new PublishCategoryItem(Category1, "Morphology", [new PublishNamedItem(Criterion1, "Shape"), new PublishNamedItem(Criterion2, "Colour")])]),
            ],
            [
                new PublishParticipant(
                    Participant1, "L001", "None",
                    [new PublishRating(Sample1, "Good")],
                    [new PublishMethodResult(MethodItem1, "Blood agar")],
                    [new PublishResultItemResult(ResultItem1, Sample1, "Positive")],
                    [new PublishAssessment(SampleTest1, Receipt, "Well done", Assessor, "Ann Assessor", new DateTime(2026, 7, 1), 4, new DateTime(2026, 6, 20), null, null)],
                    [new PublishCategoryResult(Category1, Sample1, 3, [Criterion1, Criterion2])]),
            ],
            [new PublishIntendedMethod(MethodItem1, "Blood agar")],
            [new PublishIntendedResult(ResultItem1, Sample1, "Positive", "Pos")],
            [new PublishAttachment(Attachment1, "support.pdf", "application/pdf", [1, 2, 3], new DateTime(2026, 6, 2))]);

    // Ids come out of the planner in a fixed, readable sequence.
    private static Func<Guid> Ids()
    {
        var next = 0;
        return () => new Guid(0x1000 + ++next, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    private static TabulationPublishPlan Plan(TabulationPublishSource? source = null) =>
        TabulationPublishPlanner.Plan(source ?? Source(), "Jane Smith", Created, Ids());

    private static object? Value(PublishCommand command, string name) =>
        command.Parameters.Single(p => p.Key == name).Value;

    private static PublishCommand Single(TabulationPublishPlan plan, string procedure, Func<PublishCommand, bool>? where = null) =>
        plan.Commands.Single(c => c.Procedure == procedure && (where is null || where(c)));

    [Fact]
    public void Plan_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => TabulationPublishPlanner.Plan(null!, "x", Created));
        Assert.Throws<ArgumentNullException>(() => TabulationPublishPlanner.Plan(Source(), null!, Created));
    }

    [Fact]
    public void Plan_CallsTheLegacyProceduresInLegacyOrder()
    {
        var plan = Plan();

        Assert.Equal(
            [
                "spiPublishedTabulation",
                "spiPublishedSample", "spiPublishedSample",
                "spiPublishedTest", "spiPublishedSampleTest", "spiPublishedTestResultItem", "spiPublishedTestMethodItem",
                "spiPublishedCategoryItem", "spiPublishedCriterionItem", "spiPublishedCriterionItem",
                "spiPublishedIntendedMethodItem", "spiPublishedIntendedResultItem",
                "spiPublishedParticipant", "spiPublishedSampleRating", "spiPublishedParticipantMethodItem", "spiPublishedParticipantResultItem",
                "spiPublishedParticipantSampleTest", "spiPublishedParticipantCategoryItem", "spiPublishedParticipantCriterionItem", "spiPublishedParticipantCriterionItem",
                "spiPublishedAttachment",
            ],
            plan.Commands.Select(c => c.Procedure));
    }

    private static readonly string[] TabulationParameterNames =
    [
        "DistributionTabulationId", "Name", "Signatory", "CreationDate", "DistributionDate", "Distribution", "SchemeIdentifier", "SchemeName", "MonthId", "YearId",
        "SampleComments", "TestConsultantComments", "TestConsultantCommentsAuthor", "TestConsultantCommentsDept", "IsAccredited", "SchemeVersionDate", "HasRatings",
        "IntendedResultsOnly", "SingleParticipantTabulation", "AvailableToParticipants", "AvailableToViewers", "RequiresAssessment", "StandardTabulationText",
    ];

    [Fact]
    public void Plan_TabulationRow_CarriesTheLegacyParameters()
    {
        var plan = Plan();

        var tabulation = Single(plan, "spiPublishedTabulation");

        Assert.Equal(plan.PublishedTabulationId, Value(tabulation, "PublishedTabulationId"));
        Assert.Equal(
            new object?[]
            {
                TabulationId, "FINAL REPORT", "Jane Smith", Created, new DateTime(2026, 5, 3), "17208/BA/PT0074", "BT", "Brucella", 6, 2026,
                "Scheme comments", "Consultant comments", "Dr A", "QAU", true, new DateTime(2025, 4, 1), true, false, true, true, false, true, "Standard text",
            },
            TabulationParameterNames.Select(name => Value(tabulation, name)).ToArray());
    }

    [Fact]
    public void Plan_EveryNewRowGetsAFreshId_AndTheTabulationIdComesFirst()
    {
        var plan = Plan();

        Assert.Equal(new Guid(0x1001, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), plan.PublishedTabulationId);
        var ids = plan.Commands.SelectMany(c => c.Parameters).Where(p => p.Key.StartsWith("Published", StringComparison.Ordinal) && p.Key.EndsWith("Id", StringComparison.Ordinal) && p.Key != "PublishedTabulationId" && p.Key != "PublishedParticipantId" && p.Key != "PublishedTestId" && p.Key != "PublishedSampleId" && p.Key != "PublishedCategoryItemId" && p.Key != "PublishedParticipantCategoryItemId" && p.Key != "PublishedSampleTestId" && p.Key != "PublishedTestResultItemId" && p.Key != "PublishedTestMethodItemId" && p.Key != "PublishedCriterionItemId").ToList();
        Assert.NotEmpty(ids);
        Assert.Equal(ids.Count, ids.Select(p => p.Value).Distinct().Count());
    }

    [Fact]
    public void Plan_LinksEachRowBackToThePublishedRowsItReferences()
    {
        var plan = Plan();

        var sample = Single(plan, "spiPublishedSample", c => (Guid)Value(c, "SampleId")! == Sample1);
        var test = Single(plan, "spiPublishedTest");
        var sampleTest = Single(plan, "spiPublishedSampleTest");
        var resultItem = Single(plan, "spiPublishedTestResultItem");
        var methodItem = Single(plan, "spiPublishedTestMethodItem");
        var category = Single(plan, "spiPublishedCategoryItem");
        var participant = Single(plan, "spiPublishedParticipant");

        Assert.Equal(Value(sample, "PublishedSampleId"), Value(sampleTest, "PublishedSampleId"));
        Assert.Equal(Value(test, "PublishedTestId"), Value(sampleTest, "PublishedTestId"));
        Assert.Equal(plan.PublishedTabulationId, Value(test, "PublishedTabulationId"));
        Assert.Equal(Value(test, "PublishedTestId"), Value(resultItem, "PublishedTestId"));
        Assert.Equal(Value(test, "PublishedTestId"), Value(methodItem, "PublishedTestId"));
        Assert.Equal(Value(test, "PublishedTestId"), Value(category, "PublishedTestId"));

        var intendedMethod = Single(plan, "spiPublishedIntendedMethodItem");
        var intendedResult = Single(plan, "spiPublishedIntendedResultItem");
        Assert.Equal(Value(methodItem, "PublishedTestMethodItemId"), Value(intendedMethod, "PublishedTestMethodItemId"));
        Assert.Equal(Value(resultItem, "PublishedTestResultItemId"), Value(intendedResult, "PublishedTestResultItemId"));
        Assert.Equal(Value(sample, "PublishedSampleId"), Value(intendedResult, "PublishedSampleId"));

        var participantMethod = Single(plan, "spiPublishedParticipantMethodItem");
        var participantResult = Single(plan, "spiPublishedParticipantResultItem");
        var rating = Single(plan, "spiPublishedSampleRating");
        Assert.Equal(Value(participant, "PublishedParticipantId"), Value(participantMethod, "PublishedParticipantId"));
        Assert.Equal(Value(methodItem, "PublishedTestMethodItemId"), Value(participantMethod, "PublishedTestMethodItemId"));
        Assert.Equal(Value(resultItem, "PublishedTestResultItemId"), Value(participantResult, "PublishedTestResultItemId"));
        Assert.Equal(Value(sample, "PublishedSampleId"), Value(participantResult, "PublishedSampleId"));
        Assert.Equal(Value(sample, "PublishedSampleId"), Value(rating, "PublishedSampleId"));
    }

    [Fact]
    public void Plan_AssessmentRows_ReferenceTheirPublishedSampleTestCategoryAndCriteria()
    {
        var plan = Plan();

        var sampleTest = Single(plan, "spiPublishedSampleTest");
        var category = Single(plan, "spiPublishedCategoryItem");
        var assessment = Single(plan, "spiPublishedParticipantSampleTest");
        var participantCategory = Single(plan, "spiPublishedParticipantCategoryItem");
        var criteria = plan.Commands.Where(c => c.Procedure == "spiPublishedCriterionItem").ToList();
        var participantCriteria = plan.Commands.Where(c => c.Procedure == "spiPublishedParticipantCriterionItem").ToList();

        Assert.Equal(Value(sampleTest, "PublishedSampleTestId"), Value(assessment, "PublishedSampleTestId"));
        Assert.Equal((Receipt, "Well done", Assessor, "Ann Assessor", 4), (Value(assessment, "SampleReceiptId"), Value(assessment, "AssessmentComment"), Value(assessment, "AssessorId"), Value(assessment, "AssessorName"), Value(assessment, "AssessmentScore")));
        Assert.Equal(Value(category, "PublishedCategoryItemId"), Value(participantCategory, "PublishedCategoryItemId"));
        Assert.Equal(3, Value(participantCategory, "Score"));
        Assert.Equal(criteria.Select(c => Value(c, "PublishedCriterionItemId")), participantCriteria.Select(c => Value(c, "PublishedCriterionItemId")));
        Assert.All(participantCriteria, c => Assert.Equal(Value(participantCategory, "PublishedParticipantCategoryItemId"), Value(c, "PublishedParticipantCategoryItemId")));
    }

    [Fact]
    public void Plan_NumbersTestsAndTheirItemsFromOne()
    {
        var plan = Plan();

        Assert.Equal(1, Value(Single(plan, "spiPublishedTest"), "Order"));
        Assert.Equal(1, Value(Single(plan, "spiPublishedTestResultItem"), "Order"));
        Assert.Equal(1, Value(Single(plan, "spiPublishedTestMethodItem"), "Order"));
        Assert.Equal(1, Value(Single(plan, "spiPublishedCategoryItem"), "Order"));
        Assert.Equal([1, 2], plan.Commands.Where(c => c.Procedure == "spiPublishedCriterionItem").Select(c => Value(c, "Order")));
    }

    [Fact]
    public void Plan_TestRow_CarriesTheAssessmentSubmittedDateAndAssessor()
    {
        var test = Single(Plan(), "spiPublishedTest");

        Assert.Equal(("Culture", new DateTime(2026, 6, 1), Assessor, "Ann Assessor"), (Value(test, "Name"), Value(test, "AssessmentSubmitted"), Value(test, "AssessorId"), Value(test, "AssessorName")));
    }

    [Fact]
    public void Plan_OptionalDatesStayNull()
    {
        var source = Source() with
        {
            Tests = [Source().Tests[0] with { AssessmentSubmitted = null }],
            Attachments = [Source().Attachments[0] with { UploadDate = null }],
        };

        var plan = Plan(source);

        Assert.Null(Value(Single(plan, "spiPublishedTest"), "AssessmentSubmitted"));
        Assert.Null(Value(Single(plan, "spiPublishedAttachment"), "UploadDate"));
        Assert.Null(Value(Single(plan, "spiPublishedParticipantSampleTest"), "NonSubmissionDate"));
        Assert.Null(Value(Single(plan, "spiPublishedParticipantSampleTest"), "NonCapableDate"));
    }

    [Fact]
    public void Plan_SchemeNotNeedingAssessment_SkipsCategoriesCriteriaAndAssessments()
    {
        var plan = Plan(Source(requiresAssessment: false));

        Assert.DoesNotContain(plan.Commands, c => c.Procedure is "spiPublishedCategoryItem" or "spiPublishedCriterionItem" or "spiPublishedParticipantSampleTest" or "spiPublishedParticipantCategoryItem" or "spiPublishedParticipantCriterionItem");
        Assert.Equal(false, Value(Single(plan, "spiPublishedTabulation"), "RequiresAssessment"));
    }

    [Fact]
    public void Plan_WithoutRatings_SkipsTheSampleRatings()
    {
        var plan = Plan(Source(hasRatings: false));

        Assert.DoesNotContain(plan.Commands, c => c.Procedure == "spiPublishedSampleRating");
    }

    [Fact]
    public void Plan_AttachmentRow_CarriesTheFile()
    {
        var plan = Plan();

        var attachment = Single(plan, "spiPublishedAttachment");

        Assert.Equal(plan.PublishedTabulationId, Value(attachment, "PublishedTabulationId"));
        Assert.Equal((Attachment1, "support.pdf", "application/pdf", new DateTime(2026, 6, 2)), (Value(attachment, "AttachmentId"), Value(attachment, "FileName"), Value(attachment, "ContentType"), Value(attachment, "UploadDate")));
        Assert.Equal(new byte[] { 1, 2, 3 }, Assert.IsType<byte[]>(Value(attachment, "FileData")));
    }

    [Fact]
    public void Plan_NothingToPublishBeyondTheTabulation_IsJustTheTabulationRow()
    {
        var source = Source() with { Samples = [], Tests = [], Participants = [], IntendedMethods = [], IntendedResults = [], Attachments = [] };

        var plan = Plan(source);

        Assert.Equal("spiPublishedTabulation", Assert.Single(plan.Commands).Procedure);
    }

    [Fact]
    public void Plan_RowReferringToADraftRowThatIsNotPartOfTheTabulation_Fails()
    {
        var source = Source() with { IntendedMethods = [new PublishIntendedMethod(Guid.NewGuid(), "Orphan")] };

        var exception = Assert.Throws<InvalidOperationException>(() => Plan(source));

        Assert.Contains("is not part of the tabulation being published", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_WithTheDefaultIdGenerator_IssuesDistinctIds()
    {
        var first = TabulationPublishPlanner.Plan(Source(), "x", Created);
        var second = TabulationPublishPlanner.Plan(Source(), "x", Created);

        Assert.NotEqual(first.PublishedTabulationId, second.PublishedTabulationId);
    }

    [Fact]
    public void CommandText_NamesEveryParameterOfTheProcedure()
    {
        var command = Single(Plan(), "spiPublishedSample", c => (Guid)Value(c, "SampleId")! == Sample1);

        Assert.Equal(
            "EXEC dbo.spiPublishedSample @PublishedSampleId = @PublishedSampleId, @SampleId = @SampleId, @SampleNumber = @SampleNumber, @TabulationDescription = @TabulationDescription",
            command.CommandText);
    }
}
