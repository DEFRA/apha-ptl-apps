using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

// Legacy Assessor AssessmentEntry.DataPortal_Update and CategoryItemResultCollection.Update: what is written, in which order and with
// which parameters.
public class AssessorEntryPlannerTests
{
    private static readonly Guid Receipt = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid SampleTest = Guid.Parse("a0000000-0000-0000-0000-000000000002");
    private static readonly Guid Participant = Guid.Parse("a0000000-0000-0000-0000-000000000003");
    private static readonly Guid User = Guid.Parse("a0000000-0000-0000-0000-000000000004");
    private static readonly Guid Morphology = Guid.Parse("b0000000-0000-0000-0000-000000000001");
    private static readonly Guid Staining = Guid.Parse("b0000000-0000-0000-0000-000000000002");
    private static readonly Guid Shape = Guid.Parse("c0000000-0000-0000-0000-000000000001");
    private static readonly Guid Colour = Guid.Parse("c0000000-0000-0000-0000-000000000002");
    private static readonly Guid Size = Guid.Parse("c0000000-0000-0000-0000-000000000003");
    private static readonly Guid Intensity = Guid.Parse("c0000000-0000-0000-0000-000000000004");

    private static readonly DateTime When = new(2026, 10, 27, 15, 0, 0);

    // Morphology (Shape, Colour, Size) and Staining (Intensity). Saved: Morphology scored 2 with Shape ticked; Staining not saved.
    private static AssessorEntryEntity Current(
        string comment = "", int score = 0, Guid? assessorId = null, string assessorName = "", bool savedMorphology = true, bool savedStaining = false)
    {
        var entry = new AssessorEntryEntity { SampleReceiptId = Receipt, Comment = comment, Score = score, AssessorId = assessorId ?? Guid.Empty, AssessorName = assessorName };
        var morphology = new AssessorCategoryEntity { CategoryItemId = Morphology, Name = "Morphology", ActualScore = savedMorphology ? 2 : null };
        morphology.Criteria.AddRange([new(Shape, "Shape", savedMorphology), new(Colour, "Colour", false), new(Size, "Size", false)]);
        var staining = new AssessorCategoryEntity { CategoryItemId = Staining, Name = "Staining", ActualScore = savedStaining ? 4 : null };
        staining.Criteria.Add(new(Intensity, "Intensity", savedStaining));
        entry.Categories.AddRange([morphology, staining]);
        return entry;
    }

    private static AssessorEntrySave Save(
        string comment = "", int overall = 0, Guid[]? morphology = null, int morphologyScore = 2, Guid[]? staining = null, int stainingScore = 4, Guid? user = null) =>
        new(
            SampleTest,
            Participant,
            user ?? User,
            comment,
            overall,
            [new(Morphology, morphologyScore, morphology ?? [Shape]), new(Staining, stainingScore, staining ?? [])],
            When);

    private static object? Value(AssessorCommand command, string name) => command.Parameters.Single(p => p.Key == name).Value;

    [Fact]
    public void Plan_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => AssessorEntryPlanner.Plan(null!, Save(), "x"));
        Assert.Throws<ArgumentNullException>(() => AssessorEntryPlanner.Plan(Current(), null!, "x"));
        Assert.Throws<ArgumentNullException>(() => AssessorEntryPlanner.Plan(Current(), Save(), null!));
    }

    [Fact]
    public void Plan_NothingChanged_WritesNothing()
    {
        var current = Current(comment: "Well done", score: 3, assessorId: User, assessorName: "jsmith", savedStaining: true);

        var plan = AssessorEntryPlanner.Plan(current, Save(comment: "Well done", overall: 3, staining: [Intensity]), "jsmith");

        Assert.False(plan.HasChanges);
        Assert.Empty(plan.Commands);
        Assert.Null(plan.CategoryChanges);
    }

    [Fact]
    public void Plan_FirstSave_AuditsThenStampsTheEntryThenInsertsTheUnsavedCategoriesWithTheirCriteria()
    {
        var current = Current(savedMorphology: false);

        var plan = AssessorEntryPlanner.Plan(current, Save(comment: "Good", overall: 3, morphology: [Shape, Size], morphologyScore: 3, staining: [Intensity], stainingScore: 5), "jsmith");

        Assert.Equal(
            [
                "spiAuditAssessmentItem",
                "spuAssessmentEntry",
                "spiActualCategoryItemResult", "spiActualCriterionItemResult", "spiActualCriterionItemResult",
                "spiActualCategoryItemResult", "spiActualCriterionItemResult",
            ],
            plan.Commands.Select(c => c.Procedure));
        Assert.Equal([3, 5], plan.Commands.Where(c => c.Procedure == "spiActualCategoryItemResult").Select(c => Value(c, "CategoryScore")));
        Assert.Equal([Shape, Size, Intensity], plan.Commands.Where(c => c.Procedure == "spiActualCriterionItemResult").Select(c => Value(c, "CriterionItemId")));
    }

    [Fact]
    public void Plan_TheAssessmentEntryRow_CarriesTheScoreAssessorDateAndComment()
    {
        var plan = AssessorEntryPlanner.Plan(Current(savedMorphology: false), Save(comment: "Good", overall: 4), "jsmith");

        var entry = plan.Commands.Single(c => c.Procedure == "spuAssessmentEntry");

        Assert.Equal(
            "EXEC dbo.spuAssessmentEntry @ParticipantId = @ParticipantId, @SampleTestId = @SampleTestId, @Score = @Score, @AssessorId = @AssessorId, @AssessorName = @AssessorName, @AssessmentDate = @AssessmentDate, @AssessmentComment = @AssessmentComment",
            entry.CommandText);
        Assert.Equal((Participant, SampleTest, 4, User, "jsmith", When, "Good"),
            (Value(entry, "ParticipantId"), Value(entry, "SampleTestId"), Value(entry, "Score"), Value(entry, "AssessorId"), Value(entry, "AssessorName"), Value(entry, "AssessmentDate"), Value(entry, "AssessmentComment")));
    }

    [Fact]
    public void Plan_TheAuditRow_CarriesTheNewValuesTheAssessorNameAndTheLegacyReason()
    {
        var plan = AssessorEntryPlanner.Plan(Current(comment: "Old", score: 2), Save(comment: "New", overall: 4), "jsmith");

        var audit = plan.Commands[0];

        Assert.Equal("spiAuditAssessmentItem", audit.Procedure);
        Assert.Equal((Receipt, "jsmith", "Assessment Entry", "New", true, 4, true),
            (Value(audit, "SampleReceiptId"), Value(audit, "AuditWho"), Value(audit, "AuditWhy"), Value(audit, "AssessmentComment"), Value(audit, "AssessmentCommentUpdate"), Value(audit, "Score"), Value(audit, "ScoreUpdate")));
    }

    [Theory]
    [InlineData("New", 2, true, false)]
    [InlineData("Old", 4, false, true)]
    public void Plan_TheAuditFlagsSayWhichOfTheCommentAndTheScoreChanged(string comment, int overall, bool commentChanged, bool scoreChanged)
    {
        var plan = AssessorEntryPlanner.Plan(Current(comment: "Old", score: 2), Save(comment: comment, overall: overall), "jsmith");

        var audit = plan.Commands[0];
        Assert.Equal((commentChanged, scoreChanged), (Value(audit, "AssessmentCommentUpdate"), Value(audit, "ScoreUpdate")));
    }

    [Fact]
    public void Plan_OnlyACategoryChanged_IsNotAuditedInTheAssessmentItemTable()
    {
        var current = Current(comment: "Old", score: 2, assessorId: User, assessorName: "jsmith");

        var plan = AssessorEntryPlanner.Plan(current, Save(comment: "Old", overall: 2, morphologyScore: 4), "jsmith");

        Assert.DoesNotContain(plan.Commands, c => c.Procedure == "spiAuditAssessmentItem");
        Assert.Equal(["spuAssessmentEntry", "spuActualCategoryItemResult", "spiActualCategoryItemResult"], plan.Commands.Select(c => c.Procedure));
    }

    [Fact]
    public void Plan_AnotherAssessorOpeningASavedEntry_RestampsTheEntryOnly()
    {
        var current = Current(comment: "Old", score: 2, assessorId: User, assessorName: "jsmith", savedStaining: true);

        var plan = AssessorEntryPlanner.Plan(current, Save(comment: "Old", overall: 2, staining: [Intensity], user: Guid.NewGuid()), "another");

        Assert.Equal("spuAssessmentEntry", Assert.Single(plan.Commands).Procedure);
        Assert.Null(plan.CategoryChanges);
    }

    [Fact]
    public void Plan_TheSameAssessorUnderAnotherName_RestampsTheEntry()
    {
        var current = Current(comment: "Old", score: 2, assessorId: User, assessorName: "jsmith", savedStaining: true);

        var plan = AssessorEntryPlanner.Plan(current, Save(comment: "Old", overall: 2, staining: [Intensity]), "j.smith");

        Assert.Equal("spuAssessmentEntry", Assert.Single(plan.Commands).Procedure);
    }

    [Fact]
    public void Plan_ASavedCategoryWhoseScoreChanged_IsUpdatedNotInserted()
    {
        var plan = AssessorEntryPlanner.Plan(Current(), Save(morphologyScore: 5), "jsmith");

        var update = plan.Commands.Single(c => c.Procedure == "spuActualCategoryItemResult");
        Assert.Equal(
            "EXEC dbo.spuActualCategoryItemResult @ActualResultsId = @ActualResultsId, @CategoryItemId = @CategoryItemId, @SampleTestId = @SampleTestId, @CategoryScore = @CategoryScore",
            update.CommandText);
        Assert.Equal((Participant, Morphology, SampleTest, 5), (Value(update, "ActualResultsId"), Value(update, "CategoryItemId"), Value(update, "SampleTestId"), Value(update, "CategoryScore")));
    }

    [Fact]
    public void Plan_ASavedCategoryThatDidNotChange_IsLeftAlone()
    {
        var plan = AssessorEntryPlanner.Plan(Current(savedStaining: true), Save(comment: "Hi", staining: [Intensity]), "jsmith");

        Assert.DoesNotContain(plan.Commands, c => c.Procedure.Contains("ActualCategoryItemResult", StringComparison.Ordinal));
        Assert.DoesNotContain(plan.Commands, c => c.Procedure.Contains("ActualCriterionItemResult", StringComparison.Ordinal));
    }

    [Fact]
    public void Plan_CriteriaTickedAndUntickedOnASavedCategory_AreInsertedThenDeletedAfterTheCategory()
    {
        var plan = AssessorEntryPlanner.Plan(Current(), Save(morphology: [Colour, Size]), "jsmith");

        var morphology = plan.Commands.Where(c => c.Procedure is not ("spuAssessmentEntry" or "spiAuditAssessmentItem")).Take(4).ToList();
        Assert.Equal(["spuActualCategoryItemResult", "spiActualCriterionItemResult", "spiActualCriterionItemResult", "spdActualCriterionItemResult"], morphology.Select(c => c.Procedure));
        Assert.Equal([Colour, Size, Shape], morphology.Skip(1).Select(c => Value(c, "CriterionItemId")));
        Assert.Equal(
            "EXEC dbo.spdActualCriterionItemResult @ActualResultsId = @ActualResultsId, @CriterionItemId = @CriterionItemId, @SampleTestId = @SampleTestId",
            morphology[3].CommandText);
        Assert.All(morphology.Skip(1), c => Assert.Equal((Participant, SampleTest), (Value(c, "ActualResultsId"), Value(c, "SampleTestId"))));
    }

    [Fact]
    public void Plan_ACategoryTheRequestLeavesOut_IsKeptAsItWas()
    {
        var request = Save(comment: "Hi") with { Categories = [new(Morphology, 2, [Shape])] };

        var plan = AssessorEntryPlanner.Plan(Current(savedStaining: true), request, "jsmith");

        Assert.Equal(["spiAuditAssessmentItem", "spuAssessmentEntry"], plan.Commands.Select(c => c.Procedure));
    }

    [Fact]
    public void Plan_ACriterionOfAnotherCategory_Fails()
    {
        var request = Save(morphology: [Intensity]);

        var exception = Assert.Throws<InvalidOperationException>(() => AssessorEntryPlanner.Plan(Current(), request, "jsmith"));

        Assert.Contains("is not part of the category", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_DescribesTheCategoryChangesForTheLog()
    {
        var plan = AssessorEntryPlanner.Plan(Current(), Save(morphology: [Colour], morphologyScore: 3, staining: [Intensity], stainingScore: 5), "jsmith");

        Assert.Equal("Morphology: score 2->3, criteria added Colour, criteria removed Shape; Staining: score 5, criteria added Intensity", plan.CategoryChanges);
    }

    [Fact]
    public void Plan_ANameLongerThanTheColumns_IsCutToFiftyCharacters()
    {
        var longName = new string('x', 80);

        var plan = AssessorEntryPlanner.Plan(Current(savedMorphology: false), Save(comment: "Hi"), longName);

        var cut = new string('x', 50);
        Assert.Equal(cut, Value(plan.Commands[0], "AuditWho"));
        Assert.Equal(cut, Value(plan.Commands.Single(c => c.Procedure == "spuAssessmentEntry"), "AssessorName"));
    }

    [Fact]
    public void Plan_ANullComment_IsSavedAsEmptyText()
    {
        var request = Save(overall: 3) with { Comment = null! };

        var plan = AssessorEntryPlanner.Plan(Current(), request, "jsmith");

        Assert.Equal(string.Empty, Value(plan.Commands.Single(c => c.Procedure == "spuAssessmentEntry"), "AssessmentComment"));
    }
}
