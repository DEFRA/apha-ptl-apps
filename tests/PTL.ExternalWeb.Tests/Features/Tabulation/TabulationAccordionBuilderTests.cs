using PTL.Contracts.Distribution;
using PTL.ExternalWeb.Features.Tabulation;

namespace PTL.ExternalWeb.Tests.Features.Tabulation;

public class TabulationAccordionBuilderTests
{
    private static readonly TabulationPrintParticipantResponse[] TwoParticipants =
    [
        new("2567", true, string.Empty),
        new("2568", false, "Not tested"),
    ];

    private static TabulationReportResponse Report(TabulationPrintResponse print, params TabulationTableResponse[] tables) => new(
        Guid.NewGuid(), true, Guid.NewGuid(), "Title", "Name", "Distribution Date: 01/05/2026", "", "", "", "Comments :",
        false, null, [], Guid.Empty, [], Guid.Empty, tables, null, null, print);

    private static TabulationPrintResponse Print(IReadOnlyList<TabulationPrintParticipantResponse> participants, params TabulationPrintTestResponse[] tests) =>
        new(participants, tests, []);

    private static TabulationTableResponse Table(Guid? assessmentTestId = null) => new("Culture", [], assessmentTestId);

    private static TabulationPrintResultColumnResponse Column(string header, string intended, params (string Text, int Span)[] results) =>
        new(header, intended, [.. results.Select(r => new TabulationPrintResultResponse(r.Text, r.Span))]);

    private static string Row(TabulationGridRowResponse row) => string.Join("|", row.Cells.Select(c => c.Text));

    private static TabulationPrintTestResponse Test(
        IReadOnlyList<TabulationPrintMethodColumnResponse> methods, params TabulationPrintSampleColumnResponse[] samples) =>
        new("Culture", methods, samples);

    private static TabulationPrintSampleColumnResponse Sample(string number, params TabulationPrintResultColumnResponse[] columns) =>
        new(number, "Sample description", columns);

    [Fact]
    public void Build_WhenThePrintDataHasNoTests_ReturnsNullSoThePageKeepsTheTableLayout()
    {
        var report = Report(Print(TwoParticipants), Table());

        Assert.Null(TabulationAccordionBuilder.Build(report));
    }

    [Fact]
    public void Build_WhenTheTestCountDiffersFromTheTableCount_ReturnsNull()
    {
        var test = Test([], Sample("26/1028", Column("Pathogen", "Salmonella", ("Salmonella", 1), ("", 1))));

        Assert.Null(TabulationAccordionBuilder.Build(Report(Print(TwoParticipants, test), Table(), Table())));
    }

    [Fact]
    public void Build_WhenNoSampleHasResultColumnsAndThereAreNoMethodItems_ReturnsNull()
    {
        var test = Test([], Sample("26/1028"));

        Assert.Null(TabulationAccordionBuilder.Build(Report(Print(TwoParticipants, test), Table())));
    }

    [Fact]
    public void Build_OneSectionPerSample_TitledWithTheSampleNumber()
    {
        var test = Test(
            [],
            Sample("26/1028", Column("Pathogen", "Salmonella", ("Salmonella", 1), ("", 1))),
            Sample("26/1029", Column("Pathogen", "None", ("None", 1), ("", 1))));

        var tests = Assert.IsAssignableFrom<IReadOnlyList<TabulationTestSections>>(TabulationAccordionBuilder.Build(Report(Print(TwoParticipants, test), Table())));

        var sections = Assert.Single(tests).Sections;
        Assert.Equal(["26/1028", "26/1029"], sections.Select(s => s.Title));
    }

    [Fact]
    public void Build_SampleTable_HasHeaderIntendedRowsAndOneRowPerLab()
    {
        var test = Test(
            [],
            Sample("26/1028", Column("Pathogen", "Salmonella", ("Salmonella", 1), ("", 1)), Column("Ref lab", "No", ("Yes", 1), ("", 1))));

        var table = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<TabulationTestSections>>(TabulationAccordionBuilder.Build(Report(Print(TwoParticipants, test), Table()))))
            .Sections[0].Table;

        Assert.Equal("Sample 26/1028 results for Culture", table.Caption);
        Assert.Equal(["Culture|Pathogen|Ref lab", "Intended|Sample description", "Salmonella|No", "2567|Salmonella|Yes", "2568|Not tested"], table.Rows.Select(Row));
        Assert.True(table.Rows[0].IsHeader);
        Assert.Equal(2, table.Rows[1].Cells[0].RowSpan);
        Assert.Equal(2, table.Rows[1].Cells[1].ColSpan);
    }

    [Fact]
    public void Build_LabWithoutResults_GetsOneGreyNilReturnCellSpanningTheColumns()
    {
        var test = Test([], Sample("26/1028", Column("Pathogen", "Salmonella", ("Salmonella", 1), ("", 1)), Column("Ref lab", "No", ("Yes", 1), ("", 1))));

        var nilRow = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<TabulationTestSections>>(TabulationAccordionBuilder.Build(Report(Print(TwoParticipants, test), Table()))))
            .Sections[0].Table.Rows[^1];

        var nil = nilRow.Cells[1];
        Assert.Equal(2, nilRow.Cells.Count);
        Assert.Equal(2, nil.ColSpan);
        Assert.True(nil.IsNilReturn);
    }

    [Fact]
    public void Build_EmptyResultFromALabWithResults_IsNilReturnStyled()
    {
        var test = Test([], Sample("26/1028", Column("Pathogen", "Salmonella", ("", 1), ("", 1))));

        var row = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<TabulationTestSections>>(TabulationAccordionBuilder.Build(Report(Print(TwoParticipants, test), Table()))))
            .Sections[0].Table.Rows[3];

        Assert.True(row.Cells[1].IsNilReturn);
        Assert.False(row.Cells[0].IsNilReturn);
    }

    [Fact]
    public void Build_TestWithMethodItems_StartsWithAMethodItemsSection()
    {
        var methods = new TabulationPrintMethodColumnResponse[] { new("Method used", "Culture", ["Culture", ""]), new("Date", "01/05/2026", ["02/05/2026", ""]) };
        var test = Test(methods, Sample("26/1028", Column("Pathogen", "Salmonella", ("Salmonella", 1), ("", 1))));

        var sections = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<TabulationTestSections>>(TabulationAccordionBuilder.Build(Report(Print(TwoParticipants, test), Table()))))
            .Sections;

        Assert.Equal(["Method items", "26/1028"], sections.Select(s => s.Title));
        var table = sections[0].Table;
        Assert.Equal("Method items for Culture", table.Caption);
        Assert.Equal(["Culture|Method used|Date", "Intended|Culture|01/05/2026", "2567|Culture|02/05/2026", "2568|Not tested"], table.Rows.Select(Row));
        Assert.Equal(2, table.Rows[3].Cells[1].ColSpan);
    }

    [Fact]
    public void Build_TestWithMethodItemsOnly_HasJustTheMethodItemsSection()
    {
        var methods = new TabulationPrintMethodColumnResponse[] { new("Method used", "Culture", ["Culture", ""]) };
        var test = Test(methods, Sample("26/1028"));

        var sections = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<TabulationTestSections>>(TabulationAccordionBuilder.Build(Report(Print(TwoParticipants, test), Table()))))
            .Sections;

        Assert.Equal("Method items", Assert.Single(sections).Title);
    }

    [Fact]
    public void Build_AssessmentScheme_UnscoredLabSpansTheCategoryAndAssessmentColumns()
    {
        var participants = new TabulationPrintParticipantResponse[] { new("2567", true, string.Empty), new("2568", true, string.Empty) };
        var test = Test(
            [],
            Sample(
                "26/1028",
                Column("Result", "Salmonella", ("Salmonella", 1), ("Salmonella", 1)),
                Column("Category", string.Empty, ("Good [4/5]", 1), ("Not Submitted", 3)),
                Column("Assessment Comment", string.Empty, ("Fine", 1), ("", 0)),
                Column("Overall score", string.Empty, ("Good [4/5]", 1), ("", 0))));

        var table = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<TabulationTestSections>>(TabulationAccordionBuilder.Build(Report(Print(participants, test), Table()))))
            .Sections[0].Table;

        Assert.Equal("2567|Salmonella|Good [4/5]|Fine|Good [4/5]", Row(table.Rows[3]));
        Assert.Equal("2568|Salmonella|Not Submitted", Row(table.Rows[4]));
        Assert.Equal(3, table.Rows[4].Cells[2].ColSpan);
        Assert.False(table.Rows[4].Cells[2].IsNilReturn);
        Assert.Equal("Culture|Result|Category|Assessment Comment|Overall score", Row(table.Rows[0]));
    }

    [Fact]
    public void Build_CarriesTheAssessmentTestIdFromTheMatchingTable()
    {
        var assessmentTestId = Guid.NewGuid();
        var test = Test([], Sample("26/1028", Column("Pathogen", "Salmonella", ("Salmonella", 1), ("", 1))));

        var built = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<TabulationTestSections>>(TabulationAccordionBuilder.Build(Report(Print(TwoParticipants, test), Table(assessmentTestId)))));

        Assert.Equal(assessmentTestId, built.AssessmentTestId);
        Assert.Equal("Culture", built.TestName);
    }

    [Fact]
    public void Build_LabWithNoValueForAColumn_GetsABlankNilReturnCell()
    {
        var participants = new TabulationPrintParticipantResponse[] { new("2567", true, string.Empty), new("2568", true, string.Empty) };
        var test = Test([], Sample("26/1028", Column("Pathogen", "Salmonella", ("Salmonella", 1))));

        var row = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<TabulationTestSections>>(TabulationAccordionBuilder.Build(Report(Print(participants, test), Table()))))
            .Sections[0].Table.Rows[4];

        Assert.Equal("2568|", Row(row));
        Assert.True(row.Cells[1].IsNilReturn);
    }

    [Fact]
    public void Build_NullReport_Throws() =>
        Assert.Throws<ArgumentNullException>(() => TabulationAccordionBuilder.Build(null!));
}
