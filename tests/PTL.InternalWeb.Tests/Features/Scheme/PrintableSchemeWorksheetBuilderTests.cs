using PTL.Contracts.Scheme;
using PTL.InternalWeb.Features.Scheme;

namespace PTL.InternalWeb.Tests.Features.Scheme;

public class PrintableSchemeWorksheetBuilderTests
{
    private static SchemeResponse SampleScheme(
        int numberOfSamples,
        bool dateOfReceipt = false,
        bool conditionOnReceipt = false,
        bool storageConditions = false,
        IReadOnlyList<SchemeTestResponse>? tests = null) => new(
        Guid.NewGuid(), Guid.NewGuid(), 2027, "PT1234", "Salmonella Scheme", Guid.NewGuid(), Guid.NewGuid(), null,
        true, false, false, false, false, false, false, false, false, false, false, false, false,
        0, Guid.NewGuid(), numberOfSamples, 5, "UK", 10, string.Empty, false, null, null, string.Empty,
        false, false, false, false, false, false, false,
        "Biological samples", false, null, "<p>Pack carefully.</p>", dateOfReceipt, storageConditions, conditionOnReceipt,
        null, null, null, null, false, false, null, null, null, null, null,
        DateTime.UtcNow, false,
        Tests: tests ?? []);

    private static SchemeTestResponse Test(string testType, IReadOnlyList<SchemeTestItemResponse> resultItems, IReadOnlyList<SchemeTestItemResponse>? methodItems = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), testType, 1, resultItems, methodItems ?? [], []);

    [Fact]
    public void Build_PreservesSchemeHeadingAndInstructions()
    {
        var scheme = SampleScheme(5);

        var worksheet = PrintableSchemeWorksheetBuilder.Build(scheme);

        Assert.Equal("PT1234: Salmonella Scheme", worksheet.SchemeHeading);
        Assert.Equal("<p>Pack carefully.</p>", worksheet.Instructions);
        Assert.Equal("LAB. IDENTIFICATION NO.", worksheet.LabIdLabel);
    }

    [Theory]
    [InlineData(1, "00/0001")]
    [InlineData(9, "00/0009")]
    [InlineData(10, "00/0010")]
    [InlineData(100, "00/00100")]
    public void Build_GeneratesSampleNumbersInLegacyFormat(int sampleCount, string expectedLastNumber)
    {
        var scheme = SampleScheme(sampleCount);

        var worksheet = PrintableSchemeWorksheetBuilder.Build(scheme);

        Assert.Equal(sampleCount, worksheet.SampleNumbers.Count);
        Assert.Equal(expectedLastNumber, worksheet.SampleNumbers[^1]);
    }

    [Fact]
    public void Build_ZeroSamples_SuppressesResultsAndMethodTablesForEveryTest()
    {
        var test = Test("Serology", [new SchemeTestItemResponse(Guid.NewGuid(), Guid.NewGuid(), "Titre", 1, ExpectedLength: 3)]);
        var scheme = SampleScheme(0, tests: [test]);

        var worksheet = PrintableSchemeWorksheetBuilder.Build(scheme);

        Assert.Empty(worksheet.Tests);
    }

    [Fact]
    public void Build_ShowDistributionItems_OnlyWhenAnyFlagIsSet()
    {
        var withFlags = SampleScheme(1, dateOfReceipt: true);
        var withoutFlags = SampleScheme(1);

        Assert.True(PrintableSchemeWorksheetBuilder.Build(withFlags).ShowDistributionItems);
        Assert.False(PrintableSchemeWorksheetBuilder.Build(withoutFlags).ShowDistributionItems);
    }

    [Fact]
    public void Build_TestWithNoResultItems_RendersOneEmptyColumnTable()
    {
        var test = Test("Serology", []);
        var scheme = SampleScheme(1, tests: [test]);

        var worksheet = PrintableSchemeWorksheetBuilder.Build(scheme);

        var resultsTable = Assert.Single(Assert.Single(worksheet.Tests).ResultsTables);
        Assert.True(resultsTable.ShowTestName);
        Assert.Empty(resultsTable.Columns);
    }

    [Fact]
    public void Build_ColumnsFitWithinBudget_PackIntoASingleTable()
    {
        // ExpectedLength * 20 must stay within (600 - 100) * 1.05 = 525.
        var items = new[]
        {
            new SchemeTestItemResponse(Guid.NewGuid(), Guid.NewGuid(), "Result A", 1, ExpectedLength: 10),
            new SchemeTestItemResponse(Guid.NewGuid(), Guid.NewGuid(), "Result B", 2, ExpectedLength: 10),
        };
        var scheme = SampleScheme(1, tests: [Test("Serology", items)]);

        var worksheet = PrintableSchemeWorksheetBuilder.Build(scheme);

        var resultsTable = Assert.Single(Assert.Single(worksheet.Tests).ResultsTables);
        Assert.Equal(2, resultsTable.Columns.Count);
    }

    [Fact]
    public void Build_ColumnsExceedBudget_SplitAcrossMultipleTables()
    {
        // Each column is 400px (ExpectedLength 20 * 20); two together (800) exceed the 525 budget,
        // so the second column must start a new table - legacy ResultsTable.AddColumn.
        var items = new[]
        {
            new SchemeTestItemResponse(Guid.NewGuid(), Guid.NewGuid(), "Result A", 1, ExpectedLength: 20),
            new SchemeTestItemResponse(Guid.NewGuid(), Guid.NewGuid(), "Result B", 2, ExpectedLength: 20),
        };
        var scheme = SampleScheme(1, tests: [Test("Serology", items)]);

        var worksheet = PrintableSchemeWorksheetBuilder.Build(scheme);

        var resultsTables = Assert.Single(worksheet.Tests).ResultsTables;
        Assert.Equal(2, resultsTables.Count);
        Assert.True(resultsTables[0].ShowTestName);
        Assert.False(resultsTables[1].ShowTestName);
        Assert.Single(resultsTables[0].Columns);
        Assert.Single(resultsTables[1].Columns);
    }

    [Fact]
    public void Build_SingleOversizedColumn_StillGetsItsOwnTable()
    {
        // A column wider than the whole budget on its own must not be dropped - legacy only
        // refuses to add a column onto a table that already has one, never the first.
        var items = new[] { new SchemeTestItemResponse(Guid.NewGuid(), Guid.NewGuid(), "Huge", 1, ExpectedLength: 100) };
        var scheme = SampleScheme(1, tests: [Test("Serology", items)]);

        var worksheet = PrintableSchemeWorksheetBuilder.Build(scheme);

        var resultsTable = Assert.Single(Assert.Single(worksheet.Tests).ResultsTables);
        Assert.Single(resultsTable.Columns);
    }

    [Fact]
    public void Build_MethodItems_PreservedInOrder()
    {
        var methodItems = new[]
        {
            new SchemeTestItemResponse(Guid.NewGuid(), Guid.NewGuid(), "Method Two", 2),
            new SchemeTestItemResponse(Guid.NewGuid(), Guid.NewGuid(), "Method One", 1),
        };
        var scheme = SampleScheme(1, tests: [Test("Serology", [], methodItems)]);

        var worksheet = PrintableSchemeWorksheetBuilder.Build(scheme);

        Assert.Equal(["Method One", "Method Two"], Assert.Single(worksheet.Tests).MethodTable.MethodItemNames);
    }
}
