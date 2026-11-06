using System.Reflection;
using System.Text.Json;
using PTL.Core.Scheme;
using static PTL.Api.Tests.Scheme.SchemeAuditFixture;
using CoreScheme = PTL.Core.Scheme.Scheme;

namespace PTL.Api.Tests.Scheme;

public class SchemePreviousYearEditTests
{
    private const string ResultKind = "result";
    private const string MethodKind = "method";

    // Scalar fields legacy still saves on a closed-year scheme; the collections have their own tests.
    private static readonly string[] PermittedFields =
    [
        nameof(CoreScheme.CustomsVolume),
        nameof(CoreScheme.CustomsDescription),
        nameof(CoreScheme.DataConsentDeclarationActive),
        nameof(CoreScheme.DataConsentDeclarationText),
        nameof(CoreScheme.Instructions),
        nameof(CoreScheme.DateOfReceipt),
        nameof(CoreScheme.StorageConditions),
        nameof(CoreScheme.ConditionOnReceipt),
        nameof(CoreScheme.StandardTabulationText),
        nameof(CoreScheme.TestConsultantTabulationId),
        nameof(CoreScheme.UseExternalReference),
    ];

    public static IEnumerable<object[]> PermittedFieldNames() =>
        PermittedFields.Select(name => new object[] { name });

    // Every other scalar property of Scheme, so a property added later is expected to stay locked.
    public static IEnumerable<object[]> LockedFieldNames() =>
        typeof(CoreScheme).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanWrite && !IsList(property.PropertyType) && !PermittedFields.Contains(property.Name))
            .Select(property => new object[] { property.Name });

    private static bool IsList(Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IList<>);

    // The fixture as a closed-year scheme, with a value in every child field so a copy that drops one shows up.
    private static CoreScheme Stored()
    {
        var scheme = Build();
        scheme.YearId = DateTime.UtcNow.Year - 1;
        scheme.IsReadOnly = true;
        scheme.StartDate = new DateTime(scheme.YearId, 4, 1);
        scheme.LastModified = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        scheme.StandardTabulationText = "Standard tabulation text";
        scheme.DataConsentDeclarationText = "Consent text";
        scheme.Prices[0].CurrencyName = "Sterling";
        scheme.Prices[0].CurrencySymbol = "GBP";

        foreach (var item in scheme.Tests.SelectMany(test => test.ResultItems))
        {
            item.TestResultItemType = $"Result {item.Order}";
            item.ExpectedLength = 10 + item.Order;
        }

        foreach (var item in scheme.Tests.SelectMany(test => test.MethodItems))
        {
            item.TestMethodItemType = $"Method {item.Order}";
            item.ExpectedLength = 20 + item.Order;
        }

        return scheme;
    }

    private static CoreScheme Apply(CoreScheme stored, CoreScheme posted) => SchemePreviousYearEdit.Apply(stored, posted);

    private static string Json(object value) => JsonSerializer.Serialize(value);

    private static object? Read(CoreScheme scheme, string name) => typeof(CoreScheme).GetProperty(name)!.GetValue(scheme);

    private static object ChangeValue(CoreScheme scheme, string name)
    {
        var property = typeof(CoreScheme).GetProperty(name)!;
        var current = property.GetValue(scheme);
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        object changed = Type.GetTypeCode(type) switch
        {
            TypeCode.Boolean => current is not true,
            TypeCode.Int32 => (current is int number ? number : 0) + 7,
            TypeCode.String => $"{current} (posted)",
            TypeCode.DateTime => (current is DateTime date ? date : new DateTime(2001, 2, 3)).AddDays(3),
            _ when type == typeof(Guid) => Guid.NewGuid(),
            _ => throw new NotSupportedException($"{name} is a {type.Name}; teach ChangeValue to change one."),
        };

        property.SetValue(scheme, changed);
        return changed;
    }

    private static Guid[] ItemIds(SchemeTest test, string kind) => kind == ResultKind
        ? test.ResultItems.Select(item => item.TestResultItemId).ToArray()
        : test.MethodItems.Select(item => item.TestMethodItemId).ToArray();

    // The test as the form would post it back, listing exactly these items in this order.
    private static void PostItems(SchemeTest test, string kind, params Guid[] ids)
    {
        if (kind == ResultKind)
        {
            test.ResultItems = [.. ids.Select(id => new SchemeTestResultItem { TestResultItemId = id, TestResultItemTypeId = TypeA, TestId = test.TestId })];
        }
        else
        {
            test.MethodItems = [.. ids.Select(id => new SchemeTestMethodItem { TestMethodItemId = id, TestMethodItemTypeId = TypeA, TestId = test.TestId })];
        }
    }

    private static void AddItem(SchemeTest test, string kind, Guid id)
    {
        if (kind == ResultKind)
        {
            test.ResultItems.Add(new SchemeTestResultItem { TestResultItemId = id, TestResultItemTypeId = TypeA, TestId = test.TestId, Order = 3 });
        }
        else
        {
            test.MethodItems.Add(new SchemeTestMethodItem { TestMethodItemId = id, TestMethodItemTypeId = TypeA, TestId = test.TestId, Order = 3 });
        }
    }

    private static Guid[] CategoryIds(SchemeTest test) => test.Categories.Select(category => category.CategoryItemId).ToArray();

    private static Guid[] CriterionIds(SchemeCategoryItem category) => category.Criteria.Select(criterion => criterion.CriterionItemId).ToArray();

    // Every reference-typed object in the scheme's tree, labelled by where it hangs.
    private static List<(string Path, object Instance)> Instances(CoreScheme scheme)
    {
        var found = new List<(string Path, object Instance)>
        {
            ("scheme", scheme),
            ("Prices", scheme.Prices),
            ("ViewerIds", scheme.ViewerIds),
            ("Tabulations", scheme.Tabulations),
            ("Tests", scheme.Tests),
        };

        found.AddRange(scheme.Prices.Select(price => ($"price {price.SchemeCurrencyId}", (object)price)));
        found.AddRange(scheme.Tabulations.Select(tabulation => ($"tabulation {tabulation.TabulationId}", (object)tabulation)));

        foreach (var test in scheme.Tests)
        {
            found.Add(($"test {test.TestId}", test));
            found.Add(($"test {test.TestId} ResultItems", test.ResultItems));
            found.Add(($"test {test.TestId} MethodItems", test.MethodItems));
            found.Add(($"test {test.TestId} Categories", test.Categories));
            found.AddRange(test.ResultItems.Select(item => ($"result item {item.TestResultItemId}", (object)item)));
            found.AddRange(test.MethodItems.Select(item => ($"method item {item.TestMethodItemId}", (object)item)));

            foreach (var category in test.Categories)
            {
                found.Add(($"category {category.CategoryItemId}", category));
                found.Add(($"category {category.CategoryItemId} Criteria", category.Criteria));
                found.AddRange(category.Criteria.Select(criterion => ($"criterion {criterion.CriterionItemId}", (object)criterion)));
            }
        }

        return found;
    }

    // Scalar fields

    [Theory]
    [MemberData(nameof(PermittedFieldNames))]
    public void Apply_PermittedField_IsTakenFromTheRequest(string property)
    {
        var stored = Stored();
        var posted = Stored();
        var requested = ChangeValue(posted, property);

        var result = Apply(stored, posted);

        Assert.NotEqual(Read(stored, property), requested);
        Assert.Equal(requested, Read(result, property));
    }

    [Theory]
    [MemberData(nameof(LockedFieldNames))]
    public void Apply_LockedField_KeepsTheStoredValueWhateverTheRequestSays(string property)
    {
        var stored = Stored();
        var posted = Stored();
        var requested = ChangeValue(posted, property);

        var result = Apply(stored, posted);

        Assert.NotEqual(Read(stored, property), requested);
        Assert.Equal(Read(stored, property), Read(result, property));
    }

    [Fact]
    public void Apply_RequestMatchingTheStoredScheme_ReturnsAnEqualScheme()
    {
        var stored = Stored();

        var result = Apply(stored, Stored());

        Assert.Equal(Json(stored), Json(result));
    }

    [Fact]
    public void Apply_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>("existing", () => Apply(null!, Stored()));
        Assert.Throws<ArgumentNullException>("posted", () => Apply(Stored(), null!));
    }

    // Collections taken from the request

    [Fact]
    public void Apply_Tabulations_AreReplacedByTheRequests()
    {
        var posted = Stored();
        posted.Tabulations.RemoveAt(1);
        posted.Tabulations[0].Name = "Renamed";
        posted.Tabulations[0].AvailableToViewers = false;
        var addedId = Guid.NewGuid();
        posted.Tabulations.Add(new SchemeTabulation { TabulationId = addedId, Name = "Extra", ShowRatings = true });

        var result = Apply(Stored(), posted);

        Assert.Equal(new[] { Tabulation1, addedId }, result.Tabulations.Select(t => t.TabulationId));
        Assert.Equal("Renamed", result.Tabulations[0].Name);
        Assert.False(result.Tabulations[0].AvailableToViewers);
        Assert.True(result.Tabulations[1].ShowRatings);
        Assert.NotSame(posted.Tabulations, result.Tabulations);
    }

    [Fact]
    public void Apply_ViewerIds_AreReplacedByTheRequests()
    {
        var viewers = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var posted = Stored();
        posted.ViewerIds = [.. viewers];

        var result = Apply(Stored(), posted);

        Assert.Equal(viewers, result.ViewerIds);
        Assert.NotSame(posted.ViewerIds, result.ViewerIds);
    }

    [Fact]
    public void Apply_RequestWithNoViewersOrTabulations_ClearsThemButKeepsPricesAndTests()
    {
        var stored = Stored();
        var posted = Stored();
        posted.ViewerIds.Clear();
        posted.Tabulations.Clear();
        posted.Prices.Clear();
        posted.Tests.Clear();

        var result = Apply(stored, posted);

        Assert.Empty(result.ViewerIds);
        Assert.Empty(result.Tabulations);
        Assert.Equal(Json(stored.Prices), Json(result.Prices));
        Assert.Equal(Json(stored.Tests), Json(result.Tests));
    }

    // Prices

    [Fact]
    public void Apply_Prices_ComeFromTheStoredSchemeWhateverTheRequestPosts()
    {
        var stored = Stored();
        var posted = Stored();
        var price = posted.Prices[0];
        price.SchemeCurrencyId = Guid.NewGuid();
        price.CurrencyId = Guid.NewGuid();
        price.Price = 99m;
        price.CurrencyName = "Changed";
        price.CurrencySymbol = "XXX";
        posted.Prices.Add(new SchemeCurrencyPrice { SchemeCurrencyId = Guid.NewGuid(), CurrencyId = Guid.NewGuid(), Price = 5m });

        var result = Apply(stored, posted);

        Assert.Equal(Json(stored.Prices), Json(result.Prices));
        Assert.NotSame(stored.Prices[0], result.Prices[0]);
    }

    // Tests

    [Fact]
    public void Apply_TestsKeepTheirStoredOrder_WhateverOrderTheRequestListsThem()
    {
        var posted = Stored();
        posted.Tests = [posted.Tests[1], posted.Tests[0]];

        var result = Apply(Stored(), posted);

        Assert.Equal(new[] { Test1, Test2 }, result.Tests.Select(t => t.TestId));
    }

    [Fact]
    public void Apply_TestTheRequestAdds_IsIgnored()
    {
        var posted = Stored();
        posted.Tests.Add(new SchemeTest { TestId = Guid.NewGuid(), TestTypeId = TypeA, MethodItems = [new SchemeTestMethodItem { TestMethodItemId = Guid.NewGuid() }] });

        var result = Apply(Stored(), posted);

        Assert.Equal(new[] { Test1, Test2 }, result.Tests.Select(t => t.TestId));
    }

    [Fact]
    public void Apply_TestTheRequestLeavesOut_IsLeftExactlyAsStored()
    {
        var stored = Stored();
        var posted = Stored();
        posted.Tests.RemoveAt(1);

        var result = Apply(stored, posted);

        Assert.Equal(new[] { Test1, Test2 }, result.Tests.Select(t => t.TestId));
        Assert.Equal(Json(stored.Tests[1]), Json(result.Tests[1]));
        Assert.NotEmpty(result.Tests[1].Categories);
        Assert.NotEmpty(result.Tests[1].Categories[0].Criteria);
    }

    [Fact]
    public void Apply_FieldsTheRequestCarriesOnTestsAndTheirItems_AreIgnored()
    {
        var stored = Stored();
        var posted = Stored();
        foreach (var test in posted.Tests)
        {
            test.TestTypeId = Guid.NewGuid();
            test.TestType = "Changed";
            test.SchemeId = Guid.NewGuid();
            test.Order = 50;

            foreach (var item in test.ResultItems)
            {
                item.TestResultItemTypeId = Guid.NewGuid();
                item.TestResultItemType = "Changed";
                item.TestId = Guid.NewGuid();
                item.Order = 50;
                item.ExpectedLength = 50;
            }

            foreach (var item in test.MethodItems)
            {
                item.TestMethodItemTypeId = Guid.NewGuid();
                item.TestMethodItemType = "Changed";
                item.TestId = Guid.NewGuid();
                item.Order = 50;
                item.ExpectedLength = 50;
            }

            foreach (var category in test.Categories)
            {
                category.CategoryItemTypeId = Guid.NewGuid();
                category.Name = "Changed";
                category.TestId = Guid.NewGuid();
                category.Order = 50;

                foreach (var criterion in category.Criteria)
                {
                    criterion.CriterionItemTypeId = Guid.NewGuid();
                    criterion.Name = "Changed";
                    criterion.CategoryItemId = Guid.NewGuid();
                    criterion.Order = 50;
                }
            }
        }

        var result = Apply(stored, posted);

        Assert.Equal(Json(stored.Tests), Json(result.Tests));
    }

    // Result and Method items: re-order only

    [Theory]
    [InlineData(ResultKind)]
    [InlineData(MethodKind)]
    public void Apply_ItemsListedInAnotherOrder_FollowTheRequestedOrder(string kind)
    {
        var stored = Stored();
        var posted = Stored();
        var ids = ItemIds(stored.Tests[0], kind);
        PostItems(posted.Tests[0], kind, ids[1], ids[0]);

        var result = Apply(stored, posted);

        Assert.Equal(new[] { ids[1], ids[0] }, ItemIds(result.Tests[0], kind));
        Assert.Equal(ItemIds(stored.Tests[1], kind), ItemIds(result.Tests[1], kind));
    }

    [Theory]
    [InlineData(ResultKind)]
    [InlineData(MethodKind)]
    public void Apply_ItemTheRequestAdds_IsIgnored(string kind)
    {
        var stored = Stored();
        var posted = Stored();
        var ids = ItemIds(stored.Tests[0], kind);
        PostItems(posted.Tests[0], kind, ids[1], Guid.NewGuid(), ids[0], Guid.Empty);

        var result = Apply(stored, posted);

        Assert.Equal(new[] { ids[1], ids[0] }, ItemIds(result.Tests[0], kind));
    }

    [Theory]
    [InlineData(ResultKind)]
    [InlineData(MethodKind)]
    public void Apply_ItemListedUnderAnotherTest_IsIgnored(string kind)
    {
        var stored = Stored();
        var posted = Stored();
        var ids = ItemIds(stored.Tests[0], kind);
        var otherTestsItem = ItemIds(stored.Tests[1], kind)[0];
        PostItems(posted.Tests[0], kind, otherTestsItem, ids[1], ids[0]);

        var result = Apply(stored, posted);

        Assert.Equal(new[] { ids[1], ids[0] }, ItemIds(result.Tests[0], kind));
        Assert.Equal(new[] { otherTestsItem }, ItemIds(result.Tests[1], kind));
    }

    [Theory]
    [InlineData(ResultKind)]
    [InlineData(MethodKind)]
    public void Apply_ItemTheRequestLeavesOut_IsKeptAfterTheListedOnesInStoredOrder(string kind)
    {
        var stored = Stored();
        AddItem(stored.Tests[0], kind, Guid.NewGuid());
        var ids = ItemIds(stored.Tests[0], kind);
        var posted = Stored();

        PostItems(posted.Tests[0], kind, ids[2]);
        Assert.Equal(new[] { ids[2], ids[0], ids[1] }, ItemIds(Apply(stored, posted).Tests[0], kind));

        PostItems(posted.Tests[0], kind, ids[1], ids[2]);
        Assert.Equal(new[] { ids[1], ids[2], ids[0] }, ItemIds(Apply(stored, posted).Tests[0], kind));
    }

    [Theory]
    [InlineData(ResultKind)]
    [InlineData(MethodKind)]
    public void Apply_TestPostedWithNoItems_KeepsEveryStoredItemInStoredOrder(string kind)
    {
        var stored = Stored();
        var posted = Stored();
        PostItems(posted.Tests[0], kind);

        var result = Apply(stored, posted);

        Assert.Equal(ItemIds(stored.Tests[0], kind), ItemIds(result.Tests[0], kind));
    }

    [Theory]
    [InlineData(ResultKind)]
    [InlineData(MethodKind)]
    public void Apply_ItemListedTwice_IsKeptOnce(string kind)
    {
        var stored = Stored();
        var posted = Stored();
        var ids = ItemIds(stored.Tests[0], kind);

        PostItems(posted.Tests[0], kind, ids[1], ids[1], ids[0]);
        Assert.Equal(new[] { ids[1], ids[0] }, ItemIds(Apply(stored, posted).Tests[0], kind));

        PostItems(posted.Tests[0], kind, ids[0], ids[0]);
        Assert.Equal(new[] { ids[0], ids[1] }, ItemIds(Apply(stored, posted).Tests[0], kind));
    }

    // Categories: re-order and remove, never add

    [Fact]
    public void Apply_CategoriesListedInAnotherOrder_FollowTheRequestedOrderWithTheirCriteria()
    {
        var posted = Stored();
        var categories = posted.Tests[0].Categories;
        (categories[0], categories[1]) = (categories[1], categories[0]);

        var result = Apply(Stored(), posted);

        var resultCategories = result.Tests[0].Categories;
        Assert.Equal(new[] { Category2, Category1 }, CategoryIds(result.Tests[0]));
        Assert.Equal(new[] { Criterion1, Criterion2 }, CriterionIds(resultCategories[1]));
    }

    [Fact]
    public void Apply_CategoryTheRequestOmits_IsRemoved()
    {
        var posted = Stored();
        posted.Tests[0].Categories.RemoveAt(1);

        var result = Apply(Stored(), posted);

        Assert.Equal(new[] { Category1 }, CategoryIds(result.Tests[0]));
        Assert.Equal(new[] { Category3 }, CategoryIds(result.Tests[1]));
    }

    [Fact]
    public void Apply_TestPostedWithoutCategories_LosesEveryCategoryButTheOtherTestKeepsItsOwn()
    {
        var posted = Stored();
        posted.Tests[0].Categories.Clear();

        var result = Apply(Stored(), posted);

        Assert.Empty(result.Tests[0].Categories);
        Assert.Equal(new[] { Category3 }, CategoryIds(result.Tests[1]));
    }

    [Fact]
    public void Apply_CategoryTheRequestAdds_IsIgnored()
    {
        var posted = Stored();
        var categories = posted.Tests[0].Categories;
        var added = new SchemeCategoryItem
        {
            CategoryItemId = Guid.NewGuid(),
            CategoryItemTypeId = TypeA,
            Name = "Added",
            Criteria = [new SchemeCriterionItem { CriterionItemId = Guid.NewGuid(), CriterionItemTypeId = TypeA }],
        };
        posted.Tests[0].Categories = [categories[0], added, categories[1]];

        var result = Apply(Stored(), posted);

        Assert.Equal(new[] { Category1, Category2 }, CategoryIds(result.Tests[0]));
    }

    [Fact]
    public void Apply_CategoryListedUnderAnotherTest_IsIgnored()
    {
        var posted = Stored();
        var otherTestsCategory = posted.Tests[1].Categories[0];
        posted.Tests[0].Categories = [otherTestsCategory, .. posted.Tests[0].Categories];

        var result = Apply(Stored(), posted);

        Assert.Equal(new[] { Category1, Category2 }, CategoryIds(result.Tests[0]));
        Assert.Equal(new[] { Category3 }, CategoryIds(result.Tests[1]));
    }

    [Fact]
    public void Apply_CategoryListedTwice_IsKeptOnce()
    {
        var posted = Stored();
        var categories = posted.Tests[0].Categories;
        posted.Tests[0].Categories = [categories[0], categories[0], categories[1]];

        var result = Apply(Stored(), posted);

        Assert.Equal(new[] { Category1, Category2 }, CategoryIds(result.Tests[0]));
    }

    // Criteria: re-order only

    [Fact]
    public void Apply_CriteriaListedInAnotherOrder_FollowTheRequestedOrder()
    {
        var posted = Stored();
        var criteria = posted.Tests[0].Categories[0].Criteria;
        (criteria[0], criteria[1]) = (criteria[1], criteria[0]);

        var result = Apply(Stored(), posted);

        Assert.Equal(new[] { Criterion2, Criterion1 }, CriterionIds(result.Tests[0].Categories[0]));
    }

    [Fact]
    public void Apply_CriterionTheRequestAdds_IsIgnored()
    {
        var posted = Stored();
        var category = posted.Tests[0].Categories[0];
        var anotherCategorysCriterion = posted.Tests[1].Categories[0].Criteria[0];
        category.Criteria =
        [
            category.Criteria[0],
            new SchemeCriterionItem { CriterionItemId = Guid.NewGuid(), CriterionItemTypeId = TypeA, Name = "Added" },
            anotherCategorysCriterion,
            category.Criteria[1],
        ];

        var result = Apply(Stored(), posted);

        Assert.Equal(new[] { Criterion1, Criterion2 }, CriterionIds(result.Tests[0].Categories[0]));
        Assert.Equal(new[] { Criterion3 }, CriterionIds(result.Tests[1].Categories[0]));
    }

    [Fact]
    public void Apply_CriterionTheRequestOmits_IsKeptAfterTheListedOnes()
    {
        var posted = Stored();
        var category = posted.Tests[0].Categories[0];
        category.Criteria = [category.Criteria[1]];

        var result = Apply(Stored(), posted);

        Assert.Equal(new[] { Criterion2, Criterion1 }, CriterionIds(result.Tests[0].Categories[0]));
    }

    [Fact]
    public void Apply_CategoryPostedWithNoCriteria_KeepsItsStoredCriteria()
    {
        var posted = Stored();
        posted.Tests[0].Categories[0].Criteria.Clear();

        var result = Apply(Stored(), posted);

        Assert.Equal(new[] { Criterion1, Criterion2 }, CriterionIds(result.Tests[0].Categories[0]));
    }

    [Fact]
    public void Apply_CriterionListedTwice_IsKeptOnce()
    {
        var posted = Stored();
        var category = posted.Tests[0].Categories[0];
        category.Criteria = [category.Criteria[1], category.Criteria[1], category.Criteria[0]];

        var result = Apply(Stored(), posted);

        Assert.Equal(new[] { Criterion2, Criterion1 }, CriterionIds(result.Tests[0].Categories[0]));
    }

    // The result is a new scheme

    [Fact]
    public void Apply_Result_SharesNoInstanceWithTheStoredScheme()
    {
        var stored = Stored();
        var result = Apply(stored, Stored());
        var storedInstances = new HashSet<object>(Instances(stored).Select(entry => entry.Instance), ReferenceEqualityComparer.Instance);

        var shared = Instances(result).Where(entry => storedInstances.Contains(entry.Instance)).Select(entry => entry.Path);

        Assert.Empty(shared);
    }

    [Fact]
    public void Apply_EditingTheResult_LeavesTheStoredSchemeUntouched()
    {
        var stored = Stored();
        var before = Json(stored);
        var result = Apply(stored, Stored());

        result.Name = "Changed";
        result.Prices[0].Price = 1m;
        result.Prices.Clear();
        result.ViewerIds.Add(Guid.NewGuid());
        result.Tabulations.Clear();
        var test = result.Tests[0];
        test.TestType = "Changed";
        test.ResultItems[0].Order = 99;
        test.ResultItems.Clear();
        test.MethodItems[0].ExpectedLength = 99;
        test.Categories[0].Name = "Changed";
        test.Categories[0].Criteria[0].Name = "Changed";
        test.Categories[0].Criteria.Clear();
        test.Categories.Clear();
        result.Tests.Clear();

        Assert.Equal(before, Json(stored));
    }

    [Fact]
    public void Apply_DoesNotModifyEitherInput()
    {
        var stored = Stored();
        var posted = Stored();
        posted.Name = "Changed";
        posted.CustomsDescription = "Changed";
        posted.Tests[0].ResultItems = [posted.Tests[0].ResultItems[1], posted.Tests[0].ResultItems[0]];
        posted.Tests[0].Categories.RemoveAt(1);
        var storedBefore = Json(stored);
        var postedBefore = Json(posted);

        Apply(stored, posted);

        Assert.Equal(storedBefore, Json(stored));
        Assert.Equal(postedBefore, Json(posted));
    }
}
