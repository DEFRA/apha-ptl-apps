using PTL.Contracts.Distribution;
using PTL.InternalWeb.Features.Distribution;

namespace PTL.InternalWeb.Tests.Features.Distribution;

public class PrintableResultsWorksheetBuilderTests
{
    private static readonly Guid SchemeId = Guid.NewGuid();

    private static IntendedResultsResultItemResponse ResultItem(int expectedLength, bool isRange = false, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), "Column " + expectedLength, "N0", "Decimal", isRange, expectedLength);

    private static IntendedResultsSampleRowResponse Sample(
        string sampleNumber, bool isApplicable = true, string externalDescription = "", IReadOnlyList<IntendedResultsSampleResultValueResponse>? values = null) =>
        new(Guid.NewGuid(), sampleNumber, "Internal " + sampleNumber, isApplicable, values ?? [], externalDescription);

    private static IntendedResultsTestResponse Test(
        IReadOnlyList<IntendedResultsResultItemResponse> resultItems,
        IReadOnlyList<IntendedResultsSampleRowResponse> samples,
        IReadOnlyList<IntendedResultsMethodItemResponse>? methodItems = null,
        string testName = "Test A") =>
        new(Guid.NewGuid(), testName, resultItems, methodItems ?? [], samples);

    private static IntendedResultsResponse Page(
        IReadOnlyList<IntendedResultsTestResponse> tests,
        bool hasIntendedResults = true,
        DateTime schemeVersionDate = default,
        bool dateOfReceipt = false,
        bool conditionOnReceipt = false,
        bool storageConditions = false) =>
        new(SchemeId, "D26-01A", "PT0060", "Mastitis bovine", false, tests,
            schemeVersionDate, hasIntendedResults, dateOfReceipt, storageConditions, conditionOnReceipt);

    [Fact]
    public void Build_NullPage_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => PrintableResultsWorksheetBuilder.Build(2026, 4, null!, showValues: false));
    }

    [Fact]
    public void Build_ColumnsExceedingBudget_StartNewTableChunk()
    {
        var col1 = ResultItem(10);
        var col2 = ResultItem(10);
        var col3 = ResultItem(10);
        var page = Page([Test([col1, col2, col3], [Sample("S1")])]);

        var worksheet = PrintableResultsWorksheetBuilder.Build(2026, 4, page, showValues: false);

        var tables = worksheet.Tests.Single().ResultsTables;
        Assert.Equal(2, tables.Count);

        Assert.Equal([col1.TestResultItemId, col2.TestResultItemId], tables[0].Columns.Select(c => c.TestResultItemId));
        Assert.True(tables[0].ShowTestName);
        Assert.Equal("Test A", tables[0].TestName);

        Assert.Equal([col3.TestResultItemId], tables[1].Columns.Select(c => c.TestResultItemId));
        Assert.False(tables[1].ShowTestName);
        Assert.Equal(string.Empty, tables[1].TestName);
    }

    [Fact]
    public void Build_OversizedSingleColumn_IsNeverRejected_StillGetsOwnTable()
    {
        var hugeColumn = ResultItem(100);
        var page = Page([Test([hugeColumn], [Sample("S1")])]);

        var worksheet = PrintableResultsWorksheetBuilder.Build(2026, 4, page, showValues: false);

        var table = Assert.Single(worksheet.Tests.Single().ResultsTables);
        Assert.Equal(hugeColumn.TestResultItemId, Assert.Single(table.Columns).TestResultItemId);
    }

    [Fact]
    public void Build_StretchedColumnWidths_FillAvailableWidth_LastColumnAbsorbsRemainder()
    {
        var col1 = ResultItem(10);
        var col2 = ResultItem(10);
        var col3 = ResultItem(10);
        var page = Page([Test([col1, col2, col3], [Sample("S1")])]);

        var worksheet = PrintableResultsWorksheetBuilder.Build(2026, 4, page, showValues: false);

        var tables = worksheet.Tests.Single().ResultsTables;
        // Chunk 1: two 200px columns stretched into the 500px budget - floor(200*1.25)=250 each,
        // minus 1px border apiece.
        Assert.Equal(249, tables[0].Columns[0].Width);
        Assert.Equal(249, tables[0].Columns[1].Width);
        // Chunk 2: a lone column always absorbs the entire available width, minus its border.
        Assert.Equal(499, tables[1].Columns[0].Width);
    }

    [Fact]
    public void Build_StretchedColumnWidths_RoundingRemainder_GoesToLastColumnNotComputedFromStretchFactor()
    {
        var col1 = ResultItem(1);
        var col2 = ResultItem(1);
        var col3 = ResultItem(1);
        var page = Page([Test([col1, col2, col3], [Sample("S1")])]);

        var worksheet = PrintableResultsWorksheetBuilder.Build(2026, 4, page, showValues: false);

        var table = Assert.Single(worksheet.Tests.Single().ResultsTables);
        // Raw widths 20/20/20, stretch factor 500/60 = 8.3333 -> floor(166.66)=166 for the first
        // two; the third is 500-332=168, not floor(166.66)=166 - proving it is not stretch-computed.
        Assert.Equal(165, table.Columns[0].Width);
        Assert.Equal(165, table.Columns[1].Width);
        Assert.Equal(167, table.Columns[2].Width);
    }

    [Fact]
    public void Build_StretchedColumnWidths_ZeroExpectedLength_AvoidsDivideByZero_TreatsStretchFactorAsZero()
    {
        var zeroWidthColumn = ResultItem(0);
        var page = Page([Test([zeroWidthColumn], [Sample("S1")])]);

        var worksheet = PrintableResultsWorksheetBuilder.Build(2026, 4, page, showValues: false);

        // A lone column still absorbs the entire available width regardless of its raw width.
        var table = Assert.Single(worksheet.Tests.Single().ResultsTables);
        Assert.Equal(499, Assert.Single(table.Columns).Width);
    }

    [Fact]
    public void Build_ZeroResultItems_ProducesSinglePlaceholderTable_WithOneNullValuePerSampleRow()
    {
        var page = Page([Test([], [Sample("S1"), Sample("S2")])]);

        var worksheet = PrintableResultsWorksheetBuilder.Build(2026, 4, page, showValues: true);

        var table = Assert.Single(worksheet.Tests.Single().ResultsTables);
        Assert.Empty(table.Columns);
        Assert.Equal(2, table.Rows.Count);
        Assert.All(table.Rows, row => Assert.Equal([null], row.Values));
    }

    [Fact]
    public void Build_ZeroApplicableSamples_ProducesEmptyResultsTablesAndMethodRows_RegardlessOfItemCounts()
    {
        var page = Page(
        [
            Test(
                resultItems: [ResultItem(5), ResultItem(5)],
                methodItems: [new IntendedResultsMethodItemResponse(Guid.NewGuid(), "Method A", "N0", "Decimal", "XYZ")],
                samples: [Sample("S1", isApplicable: false), Sample("S2", isApplicable: false)]),
        ]);

        var worksheet = PrintableResultsWorksheetBuilder.Build(2026, 4, page, showValues: true);

        var testWorksheet = worksheet.Tests.Single();
        Assert.Empty(testWorksheet.ResultsTables);
        Assert.Empty(testWorksheet.MethodRows);
    }

    [Fact]
    public void Build_SampleRowValues_ShowValuesFalse_EveryCellIsNull()
    {
        var column = ResultItem(5, isRange: true);
        var values = new[] { new IntendedResultsSampleResultValueResponse(Guid.NewGuid(), column.TestResultItemId, "12", "15") };
        var page = Page([Test([column], [Sample("S1", values: values)])]);

        var worksheet = PrintableResultsWorksheetBuilder.Build(2026, 4, page, showValues: false);

        var row = Assert.Single(worksheet.Tests.Single().ResultsTables.Single().Rows);
        Assert.Equal([null], row.Values);
    }

    [Fact]
    public void Build_SampleRowValues_ShowValuesTrue_NoMatchingValue_IsEmptyStringNotNull()
    {
        var column = ResultItem(5);
        var page = Page([Test([column], [Sample("S1", values: [])])]);

        var worksheet = PrintableResultsWorksheetBuilder.Build(2026, 4, page, showValues: true);

        var row = Assert.Single(worksheet.Tests.Single().ResultsTables.Single().Rows);
        Assert.Equal([string.Empty], row.Values);
    }

    [Fact]
    public void Build_SampleRowValues_ShowValuesTrue_NonRangeMatch_ReturnsIntendedResultOnly()
    {
        var column = ResultItem(5, isRange: false);
        var values = new[] { new IntendedResultsSampleResultValueResponse(Guid.NewGuid(), column.TestResultItemId, "ABC", "") };
        var page = Page([Test([column], [Sample("S1", values: values)])]);

        var worksheet = PrintableResultsWorksheetBuilder.Build(2026, 4, page, showValues: true);

        var row = Assert.Single(worksheet.Tests.Single().ResultsTables.Single().Rows);
        Assert.Equal(["ABC"], row.Values);
    }

    [Fact]
    public void Build_SampleRowValues_ShowValuesTrue_RangeMatch_AlwaysAppendsToEvenWhenIntendedResult2Blank()
    {
        var column = ResultItem(5, isRange: true);
        var values = new[] { new IntendedResultsSampleResultValueResponse(Guid.NewGuid(), column.TestResultItemId, "12", "") };
        var page = Page([Test([column], [Sample("S1", values: values)])]);

        var worksheet = PrintableResultsWorksheetBuilder.Build(2026, 4, page, showValues: true);

        var row = Assert.Single(worksheet.Tests.Single().ResultsTables.Single().Rows);
        Assert.Equal(["12 to "], row.Values);
    }

    [Fact]
    public void Build_MethodRows_ShowValuesFalse_ValueIsNull_NameAlwaysPopulated()
    {
        var page = Page(
        [
            Test(
                resultItems: [],
                methodItems: [new IntendedResultsMethodItemResponse(Guid.NewGuid(), "Method A", "N0", "Decimal", "XYZ")],
                samples: [Sample("S1")]),
        ]);

        var worksheet = PrintableResultsWorksheetBuilder.Build(2026, 4, page, showValues: false);

        var methodRow = Assert.Single(worksheet.Tests.Single().MethodRows);
        Assert.Equal("Method A", methodRow.Name);
        Assert.Null(methodRow.Value);
    }

    [Fact]
    public void Build_MethodRows_ShowValuesTrue_ValuePopulatedEvenWhenEmpty()
    {
        var page = Page(
        [
            Test(
                resultItems: [],
                methodItems:
                [
                    new IntendedResultsMethodItemResponse(Guid.NewGuid(), "Method A", "N0", "Decimal", "XYZ"),
                    new IntendedResultsMethodItemResponse(Guid.NewGuid(), "Method B", "N0", "Decimal", ""),
                ],
                samples: [Sample("S1")]),
        ]);

        var worksheet = PrintableResultsWorksheetBuilder.Build(2026, 4, page, showValues: true);

        var methodRows = worksheet.Tests.Single().MethodRows;
        Assert.Equal("XYZ", methodRows[0].Value);
        Assert.Equal(string.Empty, methodRows[1].Value);
    }

    [Fact]
    public void Build_MapsTopLevelFieldsFromPage()
    {
        var page = Page(
            [],
            hasIntendedResults: true,
            schemeVersionDate: new DateTime(2026, 4, 1),
            dateOfReceipt: true,
            conditionOnReceipt: true,
            storageConditions: true) with
        { Instructions = "Keep refrigerated." };

        var worksheet = PrintableResultsWorksheetBuilder.Build(2026, 4, page, showValues: false);

        Assert.Equal("LAB. IDENTIFICATION NO.", worksheet.LabIdLabel);
        Assert.Equal("DISTRIBUTION D26-01A", worksheet.DistributionHeading);
        Assert.Equal("PT0060: Mastitis bovine", worksheet.SchemeHeading);
        Assert.Equal("Version: 01/04/2026", worksheet.VersionLabel);
        Assert.Equal("Keep refrigerated.", worksheet.Instructions);
        Assert.True(worksheet.ShowDistributionItems);
        Assert.True(worksheet.DateOfReceipt);
        Assert.True(worksheet.ConditionOnReceipt);
        Assert.True(worksheet.StorageConditions);
        Assert.True(worksheet.HasIntendedResults);
    }

    [Fact]
    public void Build_ShowDistributionItems_FalseWhenAllThreeFlagsAreFalse()
    {
        var page = Page([], dateOfReceipt: false, conditionOnReceipt: false, storageConditions: false);

        var worksheet = PrintableResultsWorksheetBuilder.Build(2026, 4, page, showValues: false);

        Assert.False(worksheet.ShowDistributionItems);
    }

    [Fact]
    public void Build_SampleInstructions_FromFirstTestOnly_UnfilteredByApplicability_PreservingOrder()
    {
        var firstTestSamples = new[]
        {
            Sample("S1", isApplicable: true, externalDescription: "First external"),
            Sample("S2", isApplicable: false, externalDescription: "Second external"),
        };
        var secondTestSamples = new[] { Sample("S9", isApplicable: true, externalDescription: "Should not appear") };
        var page = Page(
        [
            Test([], firstTestSamples, testName: "First"),
            Test([], secondTestSamples, testName: "Second"),
        ]);

        var worksheet = PrintableResultsWorksheetBuilder.Build(2026, 4, page, showValues: false);

        Assert.Equal(2, worksheet.SampleInstructions.Count);
        Assert.Equal("S1", worksheet.SampleInstructions[0].SampleNumber);
        Assert.Equal("First external", worksheet.SampleInstructions[0].ExternalDescription);
        Assert.Equal("S2", worksheet.SampleInstructions[1].SampleNumber);
        Assert.Equal("Second external", worksheet.SampleInstructions[1].ExternalDescription);
    }

    [Fact]
    public void Build_SampleInstructions_NoTests_IsEmpty()
    {
        var page = Page([]);

        var worksheet = PrintableResultsWorksheetBuilder.Build(2026, 4, page, showValues: false);

        Assert.Empty(worksheet.SampleInstructions);
    }
}
