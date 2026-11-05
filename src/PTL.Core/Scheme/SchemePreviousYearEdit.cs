namespace PTL.Core.Scheme;

// A scheme from a closed year (YearId before the current year) is not frozen. Legacy Scheme.aspx.vb's
// SetReadonly() locks most of the Details tab, the Test Consultants and Assessors tabs and Score Samples,
// and hides the Tests tab's Add and Remove actions, but the Apply button still saves what is left:
// Sample Volume, Customs Description, the Data Consent Declaration, the Distribution Level Data,
// Results Tabulations and Viewers tabs, and re-ordering on the Tests tab.
//
// Apply returns the scheme a save should write: the stored scheme with only those fields taken from the
// request, so a field legacy locks cannot change however the request was built.
public static class SchemePreviousYearEdit
{
    public static Scheme Apply(Scheme existing, Scheme posted)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(posted);

        var effective = existing.ShallowCopy();

        // Details tab.
        effective.CustomsVolume = posted.CustomsVolume;
        effective.CustomsDescription = posted.CustomsDescription;
        effective.DataConsentDeclarationActive = posted.DataConsentDeclarationActive;
        effective.DataConsentDeclarationText = posted.DataConsentDeclarationText;

        // Distribution Level Data tab.
        effective.Instructions = posted.Instructions;
        effective.DateOfReceipt = posted.DateOfReceipt;
        effective.StorageConditions = posted.StorageConditions;
        effective.ConditionOnReceipt = posted.ConditionOnReceipt;

        // Results Tabulations tab. Score Samples is locked, so StoreRatings stays as stored.
        effective.StandardTabulationText = posted.StandardTabulationText;
        effective.TestConsultantTabulationId = posted.TestConsultantTabulationId;
        effective.UseExternalReference = posted.UseExternalReference;
        effective.Tabulations = [.. posted.Tabulations];

        // Viewers tab.
        effective.ViewerIds = [.. posted.ViewerIds];

        // Prices are locked; the Tests tab can only be re-ordered (see MergeTests).
        effective.Prices = [.. existing.Prices.Select(Copy)];
        effective.Tests = [.. MergeTests(existing.Tests, posted.Tests)];

        return effective;
    }

    // Legacy hides Add and Remove on the Tests themselves and on their Result and Method items, and
    // Move on the Tests, so those can only be re-ordered. A Category keeps Move and Remove (but not
    // Add) and its Criteria can only be re-ordered. Anything the request adds, or removes where legacy
    // offers no Remove, is ignored; a Test the request leaves out entirely is left as stored.
    private static IEnumerable<SchemeTest> MergeTests(IEnumerable<SchemeTest> stored, IEnumerable<SchemeTest> posted)
    {
        var postedById = ById(posted, test => test.TestId);

        foreach (var test in stored)
        {
            postedById.TryGetValue(test.TestId, out var edited);

            yield return new SchemeTest
            {
                TestId = test.TestId,
                TestTypeId = test.TestTypeId,
                TestType = test.TestType,
                SchemeId = test.SchemeId,
                Order = test.Order,
                ResultItems = [.. Reorder(test.ResultItems, edited?.ResultItems, item => item.TestResultItemId, Copy)],
                MethodItems = [.. Reorder(test.MethodItems, edited?.MethodItems, item => item.TestMethodItemId, Copy)],
                Categories = [.. MergeCategories(test.Categories, edited?.Categories)],
            };
        }
    }

    private static IEnumerable<SchemeCategoryItem> MergeCategories(IList<SchemeCategoryItem> stored, IList<SchemeCategoryItem>? posted)
    {
        // The request does not mention this Test's Categories, so there is nothing to remove or re-order.
        if (posted is null)
        {
            return stored.Select(category => Copy(category, category.Criteria.Select(Copy)));
        }

        var storedById = ById(stored, category => category.CategoryItemId);
        var listed = new HashSet<Guid>();
        var merged = new List<SchemeCategoryItem>();

        foreach (var edited in posted)
        {
            if (storedById.TryGetValue(edited.CategoryItemId, out var category) && listed.Add(edited.CategoryItemId))
            {
                merged.Add(Copy(category, Reorder(category.Criteria, edited.Criteria, criterion => criterion.CriterionItemId, Copy)));
            }
        }

        return merged;
    }

    // The stored items in the order the request lists them. An id the request does not know is ignored,
    // and a stored item it leaves out is kept, after the ones it lists.
    private static IEnumerable<T> Reorder<T>(IList<T> stored, IList<T>? posted, Func<T, Guid> id, Func<T, T> copy)
    {
        var storedById = ById(stored, id);
        var listed = new HashSet<Guid>();

        foreach (var item in posted ?? Array.Empty<T>())
        {
            if (storedById.TryGetValue(id(item), out var match) && listed.Add(id(item)))
            {
                yield return copy(match);
            }
        }

        foreach (var item in stored.Where(item => !listed.Contains(id(item))))
        {
            yield return copy(item);
        }
    }

    private static Dictionary<Guid, T> ById<T>(IEnumerable<T> items, Func<T, Guid> id)
    {
        var index = new Dictionary<Guid, T>();
        foreach (var item in items)
        {
            index.TryAdd(id(item), item);
        }

        return index;
    }

    private static SchemeCurrencyPrice Copy(SchemeCurrencyPrice price) => new()
    {
        SchemeCurrencyId = price.SchemeCurrencyId,
        CurrencyId = price.CurrencyId,
        Price = price.Price,
        CurrencyName = price.CurrencyName,
        CurrencySymbol = price.CurrencySymbol,
    };

    private static SchemeTestResultItem Copy(SchemeTestResultItem item) => new()
    {
        TestResultItemId = item.TestResultItemId,
        TestResultItemTypeId = item.TestResultItemTypeId,
        TestResultItemType = item.TestResultItemType,
        TestId = item.TestId,
        Order = item.Order,
        ExpectedLength = item.ExpectedLength,
    };

    private static SchemeTestMethodItem Copy(SchemeTestMethodItem item) => new()
    {
        TestMethodItemId = item.TestMethodItemId,
        TestMethodItemTypeId = item.TestMethodItemTypeId,
        TestMethodItemType = item.TestMethodItemType,
        TestId = item.TestId,
        Order = item.Order,
        ExpectedLength = item.ExpectedLength,
    };

    private static SchemeCriterionItem Copy(SchemeCriterionItem item) => new()
    {
        CriterionItemId = item.CriterionItemId,
        CriterionItemTypeId = item.CriterionItemTypeId,
        Name = item.Name,
        CategoryItemId = item.CategoryItemId,
        Order = item.Order,
    };

    private static SchemeCategoryItem Copy(SchemeCategoryItem category, IEnumerable<SchemeCriterionItem> criteria) => new()
    {
        CategoryItemId = category.CategoryItemId,
        CategoryItemTypeId = category.CategoryItemTypeId,
        Name = category.Name,
        TestId = category.TestId,
        Order = category.Order,
        Criteria = [.. criteria],
    };
}
