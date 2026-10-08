using PTL.Contracts.Lookup;

namespace PTL.InternalWeb.Features.Scheme;

// Applies the Tests and Results Tabulations tabs' Add / Remove / Up / Down commands to the staged
// model. Legacy performs the equivalent mutations against its ViewState-held collections; nothing
// here touches the database - everything is only persisted when the user presses Save.
public static class SchemeTestCommands
{
    // Posted as "<action>:<testIndex>[:<childIndex>[:<grandchildIndex>]]" by the tabs' buttons.
    public static bool TryApply(SchemeFormViewModel model, string? command, Guid selectedTypeId)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        var parts = command.Split(':');
        var action = parts[0];
        var indexes = parts.Skip(1).Select(part => int.TryParse(part, out var value) ? value : -1).ToArray();

        return action switch
        {
            "add-test" => AddTest(model, selectedTypeId),
            "remove-test" => RemoveAt(model.Tests, Index(indexes, 0)),
            "move-test-up" => Move(model.Tests, Index(indexes, 0), -1),
            "move-test-down" => Move(model.Tests, Index(indexes, 0), 1),
            "add-tabulation" => AddTabulation(model),
            "remove-tabulation" => RemoveAt(model.Tabulations, Index(indexes, 0)),
            _ => ApplyChildCommand(model, action, indexes, selectedTypeId),
        };
    }

    // Legacy requires a name before the tabulation is created.
    private static bool AddTabulation(SchemeFormViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.NewTabulationName))
        {
            return false;
        }

        model.Tabulations.Add(new SchemeTabulationViewModel { TabulationId = Guid.NewGuid(), Name = model.NewTabulationName.Trim() });
        model.NewTabulationName = null;
        return true;
    }

    private static bool ApplyChildCommand(SchemeFormViewModel model, string action, int[] indexes, Guid selectedTypeId)
    {
        if (Test(model, Index(indexes, 0)) is not { } test)
        {
            return false;
        }

        return action switch
        {
            "add-result" => Add(test.ResultItems, selectedTypeId),
            "remove-result" => RemoveAt(test.ResultItems, Index(indexes, 1)),
            "move-result-up" => Move(test.ResultItems, Index(indexes, 1), -1),
            "move-result-down" => Move(test.ResultItems, Index(indexes, 1), 1),
            "add-method" => Add(test.MethodItems, selectedTypeId),
            "remove-method" => RemoveAt(test.MethodItems, Index(indexes, 1)),
            "move-method-up" => Move(test.MethodItems, Index(indexes, 1), -1),
            "move-method-down" => Move(test.MethodItems, Index(indexes, 1), 1),
            "add-category" => AddCategory(test, selectedTypeId),
            "remove-category" => RemoveAt(test.Categories, Index(indexes, 1)),
            "move-category-up" => Move(test.Categories, Index(indexes, 1), -1),
            "move-category-down" => Move(test.Categories, Index(indexes, 1), 1),
            _ => ApplyCriterionCommand(test, action, indexes, selectedTypeId),
        };
    }

    private static bool ApplyCriterionCommand(SchemeTestViewModel test, string action, int[] indexes, Guid selectedTypeId)
    {
        var categoryIndex = Index(indexes, 1);
        if (categoryIndex < 0 || categoryIndex >= test.Categories.Count)
        {
            return false;
        }

        var criteria = test.Categories[categoryIndex].Criteria;
        return action switch
        {
            "add-criterion" => Add(criteria, selectedTypeId),
            "remove-criterion" => RemoveAt(criteria, Index(indexes, 2)),
            "move-criterion-up" => Move(criteria, Index(indexes, 2), -1),
            "move-criterion-down" => Move(criteria, Index(indexes, 2), 1),
            _ => false,
        };
    }

    private static SchemeTestViewModel? Test(SchemeFormViewModel model, int index) =>
        index >= 0 && index < model.Tests.Count ? model.Tests[index] : null;

    private static int Index(int[] indexes, int position) => position < indexes.Length ? indexes[position] : -1;

    // Ids are generated client-side when an item is staged, exactly as legacy's business objects
    // do, so the Results Tabulations matrix can reference an item before it has been saved.
    private static bool AddTest(SchemeFormViewModel model, Guid testTypeId)
    {
        if (testTypeId == Guid.Empty)
        {
            return false;
        }

        model.Tests.Add(new SchemeTestViewModel { TestId = Guid.NewGuid(), TestTypeId = testTypeId });
        return true;
    }

    private static bool AddCategory(SchemeTestViewModel test, Guid categoryItemTypeId)
    {
        if (categoryItemTypeId == Guid.Empty)
        {
            return false;
        }

        test.Categories.Add(new SchemeCategoryItemViewModel { CategoryItemId = Guid.NewGuid(), CategoryItemTypeId = categoryItemTypeId });
        return true;
    }

    private static bool Add(List<SchemeTestItemViewModel> items, Guid itemTypeId)
    {
        if (itemTypeId == Guid.Empty)
        {
            return false;
        }

        items.Add(new SchemeTestItemViewModel { ItemId = Guid.NewGuid(), ItemTypeId = itemTypeId });
        return true;
    }

    private static bool RemoveAt<T>(List<T> items, int index)
    {
        if (index < 0 || index >= items.Count)
        {
            return false;
        }

        items.RemoveAt(index);
        return true;
    }

    private static bool Move<T>(List<T> items, int index, int offset)
    {
        var target = index + offset;
        if (index < 0 || index >= items.Count || target < 0 || target >= items.Count)
        {
            return false;
        }

        (items[index], items[target]) = (items[target], items[index]);
        return true;
    }

    // The display name is only known from the lookup list, so it is resolved after a command so
    // the re-rendered tree shows the item's name rather than a blank label.
    public static void ResolveNames(
        SchemeFormViewModel model,
        IReadOnlyDictionary<SchemeItemTypeKind, IReadOnlyDictionary<Guid, string>> namesByKind)
    {
        foreach (var test in model.Tests)
        {
            test.TestType = Name(namesByKind, SchemeItemTypeKind.TestType, test.TestTypeId, test.TestType);

            foreach (var item in test.ResultItems)
            {
                item.Name = Name(namesByKind, SchemeItemTypeKind.TestResultItemType, item.ItemTypeId, item.Name);
            }

            foreach (var item in test.MethodItems)
            {
                item.Name = Name(namesByKind, SchemeItemTypeKind.TestMethodItemType, item.ItemTypeId, item.Name);
            }

            foreach (var category in test.Categories)
            {
                category.Name = Name(namesByKind, SchemeItemTypeKind.CategoryItemType, category.CategoryItemTypeId, category.Name);

                foreach (var criterion in category.Criteria)
                {
                    criterion.Name = Name(namesByKind, SchemeItemTypeKind.CriterionItemType, criterion.ItemTypeId, criterion.Name);
                }
            }
        }
    }

    private static string Name(
        IReadOnlyDictionary<SchemeItemTypeKind, IReadOnlyDictionary<Guid, string>> namesByKind,
        SchemeItemTypeKind kind,
        Guid itemTypeId,
        string current) =>
        namesByKind.TryGetValue(kind, out var names) && names.TryGetValue(itemTypeId, out var name) ? name : current;
}
