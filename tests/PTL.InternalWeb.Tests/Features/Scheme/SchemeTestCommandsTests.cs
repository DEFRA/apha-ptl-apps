using PTL.Contracts.Lookup;
using PTL.InternalWeb.Features.Scheme;

namespace PTL.InternalWeb.Tests.Features.Scheme;

public class SchemeTestCommandsTests
{
    private static SchemeFormViewModel ModelWithOneTest()
    {
        var model = new SchemeFormViewModel();
        model.Tests.Add(new SchemeTestViewModel { TestTypeId = Guid.NewGuid(), TestType = "Serology" });
        return model;
    }

    [Fact]
    public void TryApply_NoCommand_DoesNothing()
    {
        var model = ModelWithOneTest();

        Assert.False(SchemeTestCommands.TryApply(model, null, Guid.NewGuid()));
        Assert.Single(model.Tests);
    }

    [Fact]
    public void TryApply_AddTest_AppendsStagedTest()
    {
        var model = new SchemeFormViewModel();
        var testTypeId = Guid.NewGuid();

        Assert.True(SchemeTestCommands.TryApply(model, "add-test", testTypeId));

        var test = Assert.Single(model.Tests);
        Assert.Equal(testTypeId, test.TestTypeId);

        // Ids are generated client-side so the tabulation matrix can reference the test before it
        // is saved.
        Assert.NotEqual(Guid.Empty, test.TestId);
    }

    [Fact]
    public void TryApply_AddTestWithoutASelection_IsIgnored()
    {
        var model = new SchemeFormViewModel();

        Assert.False(SchemeTestCommands.TryApply(model, "add-test", Guid.Empty));
        Assert.Empty(model.Tests);
    }

    [Fact]
    public void TryApply_RemoveTest_DropsIt()
    {
        var model = ModelWithOneTest();

        Assert.True(SchemeTestCommands.TryApply(model, "remove-test:0", Guid.Empty));
        Assert.Empty(model.Tests);
    }

    [Fact]
    public void TryApply_MoveTestDown_SwapsWithTheNextTest()
    {
        var model = ModelWithOneTest();
        model.Tests.Add(new SchemeTestViewModel { TestType = "Bacteriology" });

        Assert.True(SchemeTestCommands.TryApply(model, "move-test-down:0", Guid.Empty));

        Assert.Equal("Bacteriology", model.Tests[0].TestType);
        Assert.Equal("Serology", model.Tests[1].TestType);
    }

    [Fact]
    public void TryApply_MoveFirstTestUp_IsIgnored()
    {
        var model = ModelWithOneTest();

        Assert.False(SchemeTestCommands.TryApply(model, "move-test-up:0", Guid.Empty));
        Assert.Equal("Serology", model.Tests[0].TestType);
    }

    [Theory]
    [InlineData("add-result")]
    [InlineData("add-method")]
    public void TryApply_AddItem_AppendsToTheRightList(string action)
    {
        var model = ModelWithOneTest();
        var itemTypeId = Guid.NewGuid();

        Assert.True(SchemeTestCommands.TryApply(model, $"{action}:0", itemTypeId));

        var items = action == "add-result" ? model.Tests[0].ResultItems : model.Tests[0].MethodItems;
        Assert.Equal(itemTypeId, Assert.Single(items).ItemTypeId);
    }

    [Fact]
    public void TryApply_MoveResultItemDown_ReordersWithinTheTest()
    {
        var model = ModelWithOneTest();
        model.Tests[0].ResultItems.Add(new SchemeTestItemViewModel { Name = "First" });
        model.Tests[0].ResultItems.Add(new SchemeTestItemViewModel { Name = "Second" });

        Assert.True(SchemeTestCommands.TryApply(model, "move-result-down:0:0", Guid.Empty));

        Assert.Equal("Second", model.Tests[0].ResultItems[0].Name);
    }

    [Fact]
    public void TryApply_AddCriterion_AppendsUnderTheNamedCategory()
    {
        var model = ModelWithOneTest();
        model.Tests[0].Categories.Add(new SchemeCategoryItemViewModel { Name = "Accuracy" });
        var criterionTypeId = Guid.NewGuid();

        Assert.True(SchemeTestCommands.TryApply(model, "add-criterion:0:0", criterionTypeId));

        Assert.Equal(criterionTypeId, Assert.Single(model.Tests[0].Categories[0].Criteria).ItemTypeId);
    }

    [Fact]
    public void TryApply_IndexOutOfRange_IsIgnored()
    {
        var model = ModelWithOneTest();

        Assert.False(SchemeTestCommands.TryApply(model, "remove-test:5", Guid.Empty));
        Assert.False(SchemeTestCommands.TryApply(model, "remove-result:0:5", Guid.Empty));
        Assert.False(SchemeTestCommands.TryApply(model, "add-criterion:0:5", Guid.NewGuid()));
        Assert.Single(model.Tests);
    }

    [Fact]
    public void TryApply_UnknownCommand_IsIgnored()
    {
        var model = ModelWithOneTest();

        Assert.False(SchemeTestCommands.TryApply(model, "detonate:0", Guid.Empty));
    }

    [Fact]
    public void ResolveNames_FillsDisplayNamesForEveryLevel()
    {
        var testTypeId = Guid.NewGuid();
        var resultTypeId = Guid.NewGuid();
        var methodTypeId = Guid.NewGuid();
        var categoryTypeId = Guid.NewGuid();
        var criterionTypeId = Guid.NewGuid();

        var model = new SchemeFormViewModel();
        model.Tests.Add(new SchemeTestViewModel
        {
            TestTypeId = testTypeId,
            ResultItems = [new SchemeTestItemViewModel { ItemTypeId = resultTypeId }],
            MethodItems = [new SchemeTestItemViewModel { ItemTypeId = methodTypeId }],
            Categories =
            [
                new SchemeCategoryItemViewModel
                {
                    CategoryItemTypeId = categoryTypeId,
                    Criteria = [new SchemeTestItemViewModel { ItemTypeId = criterionTypeId }],
                }
            ],
        });

        SchemeTestCommands.ResolveNames(model, new Dictionary<SchemeItemTypeKind, IReadOnlyDictionary<Guid, string>>
        {
            [SchemeItemTypeKind.TestType] = new Dictionary<Guid, string> { [testTypeId] = "Serology" },
            [SchemeItemTypeKind.TestResultItemType] = new Dictionary<Guid, string> { [resultTypeId] = "Titre" },
            [SchemeItemTypeKind.TestMethodItemType] = new Dictionary<Guid, string> { [methodTypeId] = "ELISA" },
            [SchemeItemTypeKind.CategoryItemType] = new Dictionary<Guid, string> { [categoryTypeId] = "Accuracy" },
            [SchemeItemTypeKind.CriterionItemType] = new Dictionary<Guid, string> { [criterionTypeId] = "Within range" },
        });

        var test = model.Tests[0];
        Assert.Equal("Serology", test.TestType);
        Assert.Equal("Titre", test.ResultItems[0].Name);
        Assert.Equal("ELISA", test.MethodItems[0].Name);
        Assert.Equal("Accuracy", test.Categories[0].Name);
        Assert.Equal("Within range", test.Categories[0].Criteria[0].Name);
    }

    [Fact]
    public void ResolveNames_UnknownItemType_KeepsTheExistingName()
    {
        var model = ModelWithOneTest();

        SchemeTestCommands.ResolveNames(model, new Dictionary<SchemeItemTypeKind, IReadOnlyDictionary<Guid, string>>());

        Assert.Equal("Serology", model.Tests[0].TestType);
    }
}
