using PTL.Contracts.Distribution;
using PTL.Core.Distribution;
using static PTL.Api.Tests.Distribution.TestConsultantTestData;

namespace PTL.Api.Tests.Distribution;

// Legacy Test Consultant/Tabulation.aspx.vb: the sample filter (HiddenColumnBlocks), the participant information
// table, the sample information tab and the comments editor state. The grids themselves come from
// TabulationReportBuilder (see TabulationReportBuilderTests).
public class TestConsultantTabulationBuilderTests
{
    private static TestConsultantTabulationResponse Build(
        TestConsultantTabulationEntity? results = null, TestConsultantTabulationEntity? full = null, TestConsultantTabulationFilter? filter = null) =>
        TestConsultantTabulationBuilder.Build(results ?? Entity(), full ?? Entity(), filter ?? TestConsultantTabulationFilter.None);

    private static List<string> Texts(TabulationGridRowResponse row) => [.. row.Cells.Select(c => c.Text)];

    [Fact]
    public void Build_NullArguments_Throw()
    {
        var entity = Entity();

        Assert.Throws<ArgumentNullException>(() => TestConsultantTabulationBuilder.Build(null!, entity, TestConsultantTabulationFilter.None));
        Assert.Throws<ArgumentNullException>(() => TestConsultantTabulationBuilder.Build(entity, null!, TestConsultantTabulationFilter.None));
        Assert.Throws<ArgumentNullException>(() => TestConsultantTabulationBuilder.Build(entity, entity, null!));
    }

    [Fact]
    public void Build_SetsTheTitleDateAndTexts()
    {
        var response = Build();

        Assert.Equal(SchemeId, response.MonthlyDistributionSchemeId);
        Assert.Equal("FINAL REPORT for D1a/S1 BT: Brucella", response.Title);
        Assert.Equal("Date Of Distribution: 03/05/2026", response.DistributionDateText);
        Assert.Equal("<p>Standard text</p>", response.StandardTabulationTextHtml);
        Assert.Equal("<p>Scheme comments</p>", response.CommentsHtml);
    }

    [Fact]
    public void Build_DrawsTheResultsAndFullTabulationsWithTheIntendedRowAndParticipants()
    {
        var response = Build();

        var table = Assert.Single(response.ResultsTables);
        Assert.Equal("PCR", table.Caption);
        Assert.Equal(["PCR", "Method Details", "26/1", "26/2"], Texts(table.Rows[0]));
        Assert.Equal(["Intended", string.Empty, "Positive", "Negative"], Texts(table.Rows[2]));
        Assert.Equal(["PCR", "Positive", string.Empty], Texts(table.Rows[3]));
        Assert.Equal(["L001", "In-house PCR", "Positive", "Negative"], Texts(table.Rows[4]));
        Assert.Equal("L002", table.Rows[5].Cells[0].Text);
        Assert.True(table.Rows[5].Cells[1].IsNilReturn);
        Assert.Equal("Late", table.Rows[5].Cells[1].Text);
        Assert.Single(response.FullTables);
        Assert.Null(response.ResultsRatings);
        Assert.Null(response.FullRatings);
    }

    [Fact]
    public void Build_WithRatings_AddsTheRatingsTables()
    {
        var response = Build(Entity(hasRatings: true), Entity(hasRatings: true));

        Assert.Equal("QAU Ratings", response.ResultsRatings!.Rows[0].Cells[0].Text);
        Assert.NotNull(response.FullRatings);
    }

    [Fact]
    public void Build_ListsEverySampleNumberInOrderAndWhetherThereAreMethodItems()
    {
        var response = Build();

        Assert.Equal(["26/1", "26/2"], response.SampleNumbers);
        Assert.True(response.HasMethodItems);
    }

    [Fact]
    public void Build_WithoutMethodItems_ReportsNone()
    {
        var full = Entity();
        full.Source.Tests[0].MethodItems.Clear();

        Assert.False(Build(full: full).HasMethodItems);
    }

    [Fact]
    public void Build_HiddenSample_LeavesItsColumnOutOfBothTabs()
    {
        var response = Build(filter: new TestConsultantTabulationFilter(["26/2"], false));

        Assert.Equal(["PCR", "Method Details", "26/1"], Texts(response.ResultsTables[0].Rows[0]));
        Assert.Equal(["PCR", "Method Details", "26/1"], Texts(response.FullTables[0].Rows[0]));
        Assert.Equal(["L001", "In-house PCR", "Positive"], Texts(response.ResultsTables[0].Rows[4]));
        Assert.Equal(["26/1", "26/2"], response.SampleNumbers);
    }

    [Fact]
    public void Build_HideMethodItems_LeavesTheMethodColumnsOut()
    {
        var response = Build(filter: new TestConsultantTabulationFilter([], true));

        Assert.Equal(["PCR", "26/1", "26/2"], Texts(response.ResultsTables[0].Rows[0]));
        Assert.Equal(["L001", "Positive", "Negative"], Texts(response.ResultsTables[0].Rows[4]));
        Assert.True(response.HasMethodItems);
    }

    [Fact]
    public void Build_HidingEverySample_DropsTheTestTable()
    {
        var response = Build(filter: new TestConsultantTabulationFilter(["26/1", "26/2"], false));

        Assert.Empty(response.ResultsTables);
    }

    [Fact]
    public void Build_Filter_KeepsTheRatingsOfTheShownSamplesOnly()
    {
        var response = Build(Entity(hasRatings: true), Entity(hasRatings: true), new TestConsultantTabulationFilter(["26/2"], false));

        Assert.Equal(["QAU Ratings", "26/1"], Texts(response.ResultsRatings!.Rows[0]));
    }

    [Fact]
    public void Build_Filter_DoesNotChangeTheSourceEntity()
    {
        var results = Entity();

        _ = Build(results, filter: new TestConsultantTabulationFilter(["26/2"], true));

        Assert.Equal(2, results.Source.Tests[0].Samples.Count);
        Assert.Single(results.Source.Tests[0].MethodItems);
    }

    [Fact]
    public void Build_ParticipantInformation_HasAHeaderAndOneRowPerParticipant()
    {
        var table = Build().ParticipantInformation!;

        Assert.True(table.Rows[0].IsHeader);
        Assert.Equal(["Participant", "Extra Documents Returned", "Deadline Extension Reason"], Texts(table.Rows[0]));
        Assert.Equal(["L001\u00a0", "Yes", "\u00a0"], Texts(table.Rows[1]));
        Assert.Equal(["L002\u00a0", "No", "\u00a0Postal delay"], Texts(table.Rows[2]));
    }

    [Fact]
    public void Build_NoParticipants_HasNoParticipantInformation() =>
        Assert.Null(Build(full: Entity(withParticipantInformation: false)).ParticipantInformation);

    [Fact]
    public void Build_SampleInformation_ComesFromTheFullTabulation()
    {
        var information = Assert.Single(Build().SampleInformation);

        Assert.Equal(("26/1", "Positive", "Details of sample one"), (information.SampleNumber, information.TabulationDescription, information.TestConsultantsDescription));
    }

    [Fact]
    public void Build_NotSubmitted_ShowsTheDraftInTheEditor()
    {
        var comments = Build(Entity(comments: "<p>Draft</p>", author: "A Author")).Comments;

        Assert.Equal((false, "Add your comments here:", "<p>Draft</p>", true), (comments.IsSubmitted, comments.Heading, comments.CommentsHtml, comments.HasComments));
    }

    [Fact]
    public void Build_NotSubmittedWithoutComments_HasNoComments()
    {
        var comments = Build(Entity(comments: "<p> </p>")).Comments;

        Assert.False(comments.HasComments);
        Assert.Equal(string.Empty, comments.CommentsHtml);
    }

    [Fact]
    public void Build_SubmittedWithAuthor_NamesTheAuthor()
    {
        var comments = Build(Entity(submitted: true, comments: "<p>Final</p>", author: " A Author ")).Comments;

        Assert.Equal((true, "Comments provided by A Author:", "<p>Final</p>"), (comments.IsSubmitted, comments.Heading, comments.CommentsHtml));
    }

    [Fact]
    public void Build_SubmittedWithoutAuthor_UsesTheGenericHeading() =>
        Assert.Equal("Comments:", Build(Entity(submitted: true, comments: "<p>Final</p>")).Comments.Heading);

    [Fact]
    public void Build_SubmittedWithoutComments_SaysNoneWereSubmitted()
    {
        var comments = Build(Entity(submitted: true)).Comments;

        Assert.Equal("No comments were submitted before the deadline", comments.Heading);
        Assert.False(comments.HasComments);
    }

    [Fact]
    public void Build_SanitisesTheComments() =>
        Assert.Equal("<p>Hi</p>", Build(Entity(comments: "<p onclick=\"x()\">Hi</p><iframe src=\"x\"></iframe>")).Comments.CommentsHtml);
}
