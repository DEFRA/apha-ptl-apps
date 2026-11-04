namespace PTL.Core.Scheme;

// Decides whether saving an existing Scheme needs a reason, and which audit rows go with it.
// Mirrors legacy AuditableBusinessBase.AuditableCollection over the Scheme's Tests and Tabulations:
// only the audited properties of the audited child classes count, an item added or removed counts
// in its own right, and the children of a removed Test, Category or Tabulation are not audited
// individually (their rows are removed along with the parent). Scheme-level fields, prices,
// viewers, staffing and the Tabulation flags are never audited.
public static class SchemeAuditDetector
{
    private sealed record Stored(Guid TypeId, int Order, Guid ParentId);

    public static SchemeAuditPlan Detect(Scheme existing, Scheme updated)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(updated);

        var tests = new List<SchemeItemAudit>();
        var methodItems = new List<SchemeItemAudit>();
        var resultItems = new List<SchemeItemAudit>();
        var categories = new List<SchemeItemAudit>();
        var criteria = new List<SchemeItemAudit>();

        var storedTests = Index(existing.Tests, t => t.TestId, t => t.TestTypeId, t => t.Order, _ => existing.SchemeId);
        var storedMethodItems = Index(existing.Tests.SelectMany(t => t.MethodItems), i => i.TestMethodItemId, i => i.TestMethodItemTypeId, i => i.Order, i => i.TestId);
        var storedResultItems = Index(existing.Tests.SelectMany(t => t.ResultItems), i => i.TestResultItemId, i => i.TestResultItemTypeId, i => i.Order, i => i.TestId);
        var storedCategories = Index(existing.Tests.SelectMany(t => t.Categories), c => c.CategoryItemId, c => c.CategoryItemTypeId, c => c.Order, c => c.TestId);
        var storedCriteria = Index(existing.Tests.SelectMany(t => t.Categories).SelectMany(c => c.Criteria), c => c.CriterionItemId, c => c.CriterionItemTypeId, c => c.Order, c => c.CategoryItemId);

        Compare(updated.Tests, t => t.TestId, t => t.TestTypeId, storedTests, typeIsAudited: true, tests);

        foreach (var test in updated.Tests)
        {
            Compare(test.MethodItems, i => i.TestMethodItemId, i => i.TestMethodItemTypeId, storedMethodItems, typeIsAudited: true, methodItems);
            Compare(test.ResultItems, i => i.TestResultItemId, i => i.TestResultItemTypeId, storedResultItems, typeIsAudited: true, resultItems);
            Compare(test.Categories, c => c.CategoryItemId, c => c.CategoryItemTypeId, storedCategories, typeIsAudited: false, categories);

            foreach (var category in test.Categories)
            {
                Compare(category.Criteria, c => c.CriterionItemId, c => c.CriterionItemTypeId, storedCriteria, typeIsAudited: false, criteria);
            }
        }

        var keptTestIds = updated.Tests.Select(t => t.TestId).ToHashSet();
        var keptCategoryIds = updated.Tests.SelectMany(t => t.Categories).Select(c => c.CategoryItemId).ToHashSet();

        CollectRemoved(storedTests, updated.Tests.Select(t => t.TestId), [existing.SchemeId], tests);
        CollectRemoved(storedMethodItems, updated.Tests.SelectMany(t => t.MethodItems).Select(i => i.TestMethodItemId), keptTestIds, methodItems);
        CollectRemoved(storedResultItems, updated.Tests.SelectMany(t => t.ResultItems).Select(i => i.TestResultItemId), keptTestIds, resultItems);
        CollectRemoved(storedCategories, keptCategoryIds, keptTestIds, categories);
        CollectRemoved(storedCriteria, updated.Tests.SelectMany(t => t.Categories).SelectMany(c => c.Criteria).Select(c => c.CriterionItemId), keptCategoryIds, criteria);

        var tabulations = new List<SchemeTabulationAudit>();
        var methodLinks = new List<SchemeTabulationLinkAudit>();
        var resultLinks = new List<SchemeTabulationLinkAudit>();
        CompareTabulations(existing, updated, tabulations, methodLinks, resultLinks);

        return new SchemeAuditPlan(tests, methodItems, resultItems, categories, criteria, tabulations, methodLinks, resultLinks);
    }

    private static Dictionary<Guid, Stored> Index<T>(IEnumerable<T> items, Func<T, Guid> id, Func<T, Guid> typeId, Func<T, int> order, Func<T, Guid> parentId)
    {
        var index = new Dictionary<Guid, Stored>();
        foreach (var item in items)
        {
            index.TryAdd(id(item), new Stored(typeId(item), order(item), parentId(item)));
        }

        return index;
    }

    // The save renumbers every list 1..n, so the posted position is the new Order.
    private static void Compare<T>(
        IList<T> posted, Func<T, Guid> id, Func<T, Guid> typeId, Dictionary<Guid, Stored> stored, bool typeIsAudited, List<SchemeItemAudit> sink)
    {
        for (var index = 0; index < posted.Count; index++)
        {
            var itemId = id(posted[index]);
            var newType = typeId(posted[index]);
            var newOrder = index + 1;

            if (!stored.TryGetValue(itemId, out var old))
            {
                sink.Add(new SchemeItemAudit(itemId, SchemeAuditOperation.Insert, newType, false, newOrder, false));
                continue;
            }

            var typeChanged = typeIsAudited && old.TypeId != newType;
            var orderChanged = old.Order != newOrder;
            if (typeChanged || orderChanged)
            {
                sink.Add(new SchemeItemAudit(itemId, SchemeAuditOperation.Update, newType, typeChanged, newOrder, orderChanged));
            }
        }
    }

    private static void CollectRemoved(Dictionary<Guid, Stored> stored, IEnumerable<Guid> keptIds, HashSet<Guid> survivingParents, List<SchemeItemAudit> sink)
    {
        var kept = keptIds as ISet<Guid> ?? keptIds.ToHashSet();
        foreach (var (itemId, old) in stored)
        {
            if (!kept.Contains(itemId) && survivingParents.Contains(old.ParentId))
            {
                sink.Add(new SchemeItemAudit(itemId, SchemeAuditOperation.Delete, old.TypeId, false, old.Order, false));
            }
        }
    }

    private static void CompareTabulations(
        Scheme existing,
        Scheme updated,
        List<SchemeTabulationAudit> tabulations,
        List<SchemeTabulationLinkAudit> methodLinks,
        List<SchemeTabulationLinkAudit> resultLinks)
    {
        var storedTabulations = new Dictionary<Guid, SchemeTabulation>();
        foreach (var tabulation in existing.Tabulations)
        {
            storedTabulations.TryAdd(tabulation.TabulationId, tabulation);
        }

        foreach (var tabulation in updated.Tabulations)
        {
            if (!storedTabulations.TryGetValue(tabulation.TabulationId, out var old))
            {
                tabulations.Add(new SchemeTabulationAudit(tabulation.TabulationId, SchemeAuditOperation.Insert, tabulation.Name, false, tabulation.IntendedResultsOnly, false));
                CompareLinks(tabulation.TabulationId, tabulation.MethodItemIds, [], methodLinks);
                CompareLinks(tabulation.TabulationId, tabulation.ResultItemIds, [], resultLinks);
                continue;
            }

            var nameChanged = !string.Equals(old.Name, tabulation.Name, StringComparison.Ordinal);
            var intendedResultsOnlyChanged = old.IntendedResultsOnly != tabulation.IntendedResultsOnly;
            if (nameChanged || intendedResultsOnlyChanged)
            {
                tabulations.Add(new SchemeTabulationAudit(
                    tabulation.TabulationId, SchemeAuditOperation.Update, tabulation.Name, nameChanged, tabulation.IntendedResultsOnly, intendedResultsOnlyChanged));
            }

            CompareLinks(tabulation.TabulationId, tabulation.MethodItemIds, old.MethodItemLinks, methodLinks);
            CompareLinks(tabulation.TabulationId, tabulation.ResultItemIds, old.ResultItemLinks, resultLinks);
        }

        var keptTabulations = updated.Tabulations.Select(t => t.TabulationId).ToHashSet();
        tabulations.AddRange(storedTabulations.Values
            .Where(t => !keptTabulations.Contains(t.TabulationId))
            .Select(removed => new SchemeTabulationAudit(removed.TabulationId, SchemeAuditOperation.Delete, removed.Name, false, removed.IntendedResultsOnly, false)));
    }

    private static void CompareLinks(Guid tabulationId, IList<Guid> postedItemIds, IList<SchemeTabulationItemLink> storedLinks, List<SchemeTabulationLinkAudit> sink)
    {
        var posted = postedItemIds.ToHashSet();
        var alreadyLinked = storedLinks.Select(l => l.ItemId).ToHashSet();

        sink.AddRange(storedLinks
            .Where(l => !posted.Contains(l.ItemId))
            .Select(link => new SchemeTabulationLinkAudit(link.LinkId, SchemeAuditOperation.Delete, tabulationId, link.ItemId)));
        sink.AddRange(posted
            .Where(id => !alreadyLinked.Contains(id))
            .Select(itemId => new SchemeTabulationLinkAudit(Guid.NewGuid(), SchemeAuditOperation.Insert, tabulationId, itemId)));
    }
}
