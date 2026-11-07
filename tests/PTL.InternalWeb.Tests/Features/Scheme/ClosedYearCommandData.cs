using PTL.InternalWeb.Features.Scheme;

namespace PTL.InternalWeb.Tests.Features.Scheme;

// Shared by the SchemeTestCommands and SchemeController Apply tests so both agree on which Tests and
// Results Tabulations commands a closed-year (Previous Year) scheme keeps.
public static class ClosedYearCommandData
{
    // What legacy leaves on a Previous Year scheme; every index is in range for Tree().
    public static TheoryData<string> Allowed => new()
    {
        "move-result-up:0:1",
        "move-result-down:0:0",
        "move-method-up:0:1",
        "move-method-down:0:0",
        "move-category-up:0:1",
        "move-category-down:0:0",
        "remove-category:0:0",
        "move-criterion-up:0:0:1",
        "move-criterion-down:0:0:0",
        "add-tabulation",
        "remove-tabulation:0",
    };

    // What legacy hides there. Each one changes Tree() for any other year, so a command that changes nothing is the closed-year guard.
    public static TheoryData<string> Blocked => new()
    {
        "add-test",
        "remove-test:0",
        "move-test-up:1",
        "move-test-down:0",
        "add-result:0",
        "remove-result:0:0",
        "add-method:0",
        "remove-method:0:0",
        "add-category:0",
        "add-criterion:0:0",
        "remove-criterion:0:0:0",
    };

    // Two Tests, each with two Result and Method items and two Categories of two Criteria, two Tabulations,
    // and a Tabulation name waiting to be added.
    public static SchemeFormViewModel Tree()
    {
        var model = new SchemeFormViewModel { NewTabulationName = "  Published  " };
        model.Tests.Add(Test("Serology"));
        model.Tests.Add(Test("Bacteriology"));
        model.Tabulations.Add(new SchemeTabulationViewModel { TabulationId = Guid.NewGuid(), Name = "Tab A" });
        model.Tabulations.Add(new SchemeTabulationViewModel { TabulationId = Guid.NewGuid(), Name = "Tab B" });
        return model;
    }

    // Names in order: enough to tell any add, remove or move apart, and the same on every run, unlike the generated ids.
    public static string Describe(SchemeFormViewModel model)
    {
        var tests = model.Tests.Select(test =>
            $"{test.TestType} results[{Names(test.ResultItems)}] methods[{Names(test.MethodItems)}] "
            + $"categories[{string.Join(',', test.Categories.Select(category => $"{category.Name}({Names(category.Criteria)})"))}]");

        return $"{string.Join(" | ", tests)} tabulations[{string.Join(',', model.Tabulations.Select(tabulation => tabulation.Name))}] new[{model.NewTabulationName}]";
    }

    private static string Names(IEnumerable<SchemeTestItemViewModel> items) => string.Join(',', items.Select(item => item.Name));

    private static SchemeTestViewModel Test(string name) => new()
    {
        TestId = Guid.NewGuid(),
        TestTypeId = Guid.NewGuid(),
        TestType = name,
        ResultItems = [Item($"{name} result 1"), Item($"{name} result 2")],
        MethodItems = [Item($"{name} method 1"), Item($"{name} method 2")],
        Categories = [Category($"{name} category 1"), Category($"{name} category 2")],
    };

    private static SchemeCategoryItemViewModel Category(string name) => new()
    {
        CategoryItemId = Guid.NewGuid(),
        CategoryItemTypeId = Guid.NewGuid(),
        Name = name,
        Criteria = [Item($"{name} criterion 1"), Item($"{name} criterion 2")],
    };

    private static SchemeTestItemViewModel Item(string name) => new() { ItemId = Guid.NewGuid(), ItemTypeId = Guid.NewGuid(), Name = name };
}
