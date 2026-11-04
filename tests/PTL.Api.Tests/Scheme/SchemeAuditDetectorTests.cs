using PTL.Core.Scheme;
using static PTL.Api.Tests.Scheme.SchemeAuditFixture;
using CoreScheme = PTL.Core.Scheme.Scheme;

namespace PTL.Api.Tests.Scheme;

public class SchemeAuditDetectorTests
{
    private static SchemeAuditPlan Detect(Action<CoreScheme> edit)
    {
        var updated = Build();
        edit(updated);
        return SchemeAuditDetector.Detect(Build(), updated);
    }

    private static SchemeTest Test(int index, CoreScheme scheme) => scheme.Tests[index];

    private static Guid NewId() => Guid.NewGuid();

    private static void AssertOnlyTests(SchemeAuditPlan plan)
    {
        Assert.Empty(plan.TestMethodItems);
        Assert.Empty(plan.TestResultItems);
        Assert.Empty(plan.CategoryItems);
        Assert.Empty(plan.CriterionItems);
        Assert.Empty(plan.Tabulations);
        Assert.Empty(plan.MethodLinks);
        Assert.Empty(plan.ResultLinks);
    }

    [Fact]
    public void Detect_NothingChanged_PlanIsEmpty()
    {
        var plan = Detect(_ => { });

        Assert.True(plan.IsEmpty);
    }

    [Fact]
    public void Detect_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => SchemeAuditDetector.Detect(null!, Build()));
        Assert.Throws<ArgumentNullException>(() => SchemeAuditDetector.Detect(Build(), null!));
    }

    // Tests

    [Fact]
    public void Detect_TestAdded_IsAnInsertAtTheNextPosition()
    {
        var newId = NewId();

        var plan = Detect(s => s.Tests.Add(new SchemeTest { TestId = newId, TestTypeId = TypeB }));

        var audit = Assert.Single(plan.Tests);
        Assert.Equal(new SchemeItemAudit(newId, SchemeAuditOperation.Insert, TypeB, false, 3, false), audit);
        Assert.False(plan.IsEmpty);
        Assert.Empty(plan.TestMethodItems);
        Assert.Empty(plan.Tabulations);
    }

    [Fact]
    public void Detect_TestRemoved_IsADeleteCarryingTheStoredTypeAndOrder()
    {
        var plan = Detect(s => s.Tests.RemoveAt(1));

        var audit = Assert.Single(plan.Tests);
        Assert.Equal(new SchemeItemAudit(Test2, SchemeAuditOperation.Delete, TypeB, false, 2, false), audit);
    }

    [Fact]
    public void Detect_TestRemoved_ItsChildrenAreNotAuditedIndividually()
    {
        var plan = Detect(s => s.Tests.RemoveAt(0));

        Assert.Equal(Test1, Assert.Single(plan.Tests, t => t.Operation == SchemeAuditOperation.Delete).Id);
        Assert.Empty(plan.TestMethodItems);
        Assert.Empty(plan.TestResultItems);
        Assert.Empty(plan.CategoryItems);
        Assert.Empty(plan.CriterionItems);
    }

    [Fact]
    public void Detect_EarlierTestRemoved_LaterTestsBecomeOrderUpdates()
    {
        var plan = Detect(s => s.Tests.RemoveAt(0));

        Assert.Equal(2, plan.Tests.Count);
        Assert.Contains(plan.Tests, t => t == new SchemeItemAudit(Test1, SchemeAuditOperation.Delete, TypeA, false, 1, false));
        Assert.Contains(plan.Tests, t => t == new SchemeItemAudit(Test2, SchemeAuditOperation.Update, TypeB, false, 1, true));
    }

    [Fact]
    public void Detect_TestsReordered_BothAreOrderUpdates()
    {
        var plan = Detect(s => (s.Tests[0], s.Tests[1]) = (s.Tests[1], s.Tests[0]));

        Assert.Equal(2, plan.Tests.Count);
        Assert.Contains(plan.Tests, t => t == new SchemeItemAudit(Test2, SchemeAuditOperation.Update, TypeB, false, 1, true));
        Assert.Contains(plan.Tests, t => t == new SchemeItemAudit(Test1, SchemeAuditOperation.Update, TypeA, false, 2, true));
        AssertOnlyTests(plan);
    }

    [Fact]
    public void Detect_TestTypeChanged_IsATypeUpdateOnly()
    {
        var plan = Detect(s => Test(0, s).TestTypeId = TypeB);

        var audit = Assert.Single(plan.Tests);
        Assert.Equal(new SchemeItemAudit(Test1, SchemeAuditOperation.Update, TypeB, true, 1, false), audit);
        AssertOnlyTests(plan);
    }

    [Fact]
    public void Detect_TestTypeAndOrderChanged_BothFlagsAreSet()
    {
        var plan = Detect(s =>
        {
            (s.Tests[0], s.Tests[1]) = (s.Tests[1], s.Tests[0]);
            s.Tests[1].TestTypeId = TypeB;
        });

        Assert.Contains(plan.Tests, t => t == new SchemeItemAudit(Test1, SchemeAuditOperation.Update, TypeB, true, 2, true));
    }

    [Fact]
    public void Detect_TestAdded_ItsChildrenAreInserts()
    {
        var testId = NewId();
        var methodId = NewId();
        var resultId = NewId();
        var categoryId = NewId();
        var criterionId = NewId();

        var plan = Detect(s => s.Tests.Add(new SchemeTest
        {
            TestId = testId,
            TestTypeId = TypeA,
            MethodItems = [new SchemeTestMethodItem { TestMethodItemId = methodId, TestMethodItemTypeId = TypeB }],
            ResultItems = [new SchemeTestResultItem { TestResultItemId = resultId, TestResultItemTypeId = TypeB }],
            Categories =
            [
                new SchemeCategoryItem
                {
                    CategoryItemId = categoryId,
                    CategoryItemTypeId = TypeA,
                    Criteria = [new SchemeCriterionItem { CriterionItemId = criterionId, CriterionItemTypeId = TypeB }],
                },
            ],
        }));

        Assert.Equal(new SchemeItemAudit(testId, SchemeAuditOperation.Insert, TypeA, false, 3, false), Assert.Single(plan.Tests));
        Assert.Equal(new SchemeItemAudit(methodId, SchemeAuditOperation.Insert, TypeB, false, 1, false), Assert.Single(plan.TestMethodItems));
        Assert.Equal(new SchemeItemAudit(resultId, SchemeAuditOperation.Insert, TypeB, false, 1, false), Assert.Single(plan.TestResultItems));
        Assert.Equal(new SchemeItemAudit(categoryId, SchemeAuditOperation.Insert, TypeA, false, 1, false), Assert.Single(plan.CategoryItems));
        Assert.Equal(new SchemeItemAudit(criterionId, SchemeAuditOperation.Insert, TypeB, false, 1, false), Assert.Single(plan.CriterionItems));
    }

    // Test Method Items

    [Fact]
    public void Detect_MethodItemAdded_IsAnInsert()
    {
        var id = NewId();

        var plan = Detect(s => Test(0, s).MethodItems.Add(new SchemeTestMethodItem { TestMethodItemId = id, TestMethodItemTypeId = TypeB }));

        Assert.Equal(new SchemeItemAudit(id, SchemeAuditOperation.Insert, TypeB, false, 3, false), Assert.Single(plan.TestMethodItems));
        Assert.Empty(plan.Tests);
    }

    [Fact]
    public void Detect_MethodItemRemoved_IsADeleteCarryingTheStoredTypeAndOrder()
    {
        var plan = Detect(s => Test(0, s).MethodItems.RemoveAt(1));

        Assert.Equal(new SchemeItemAudit(Method2, SchemeAuditOperation.Delete, TypeA, false, 2, false), Assert.Single(plan.TestMethodItems));
        Assert.Empty(plan.Tests);
    }

    [Fact]
    public void Detect_MethodItemsReordered_BothAreOrderUpdates()
    {
        var plan = Detect(s => (Test(0, s).MethodItems[0], Test(0, s).MethodItems[1]) = (Test(0, s).MethodItems[1], Test(0, s).MethodItems[0]));

        Assert.Equal(2, plan.TestMethodItems.Count);
        Assert.Contains(plan.TestMethodItems, i => i == new SchemeItemAudit(Method2, SchemeAuditOperation.Update, TypeA, false, 1, true));
        Assert.Contains(plan.TestMethodItems, i => i == new SchemeItemAudit(Method1, SchemeAuditOperation.Update, TypeA, false, 2, true));
    }

    [Fact]
    public void Detect_MethodItemTypeChanged_IsATypeUpdate()
    {
        var plan = Detect(s => Test(0, s).MethodItems[0].TestMethodItemTypeId = TypeB);

        Assert.Equal(new SchemeItemAudit(Method1, SchemeAuditOperation.Update, TypeB, true, 1, false), Assert.Single(plan.TestMethodItems));
    }

    // Test Result Items

    [Fact]
    public void Detect_ResultItemAdded_IsAnInsert()
    {
        var id = NewId();

        var plan = Detect(s => Test(1, s).ResultItems.Add(new SchemeTestResultItem { TestResultItemId = id, TestResultItemTypeId = TypeA }));

        Assert.Equal(new SchemeItemAudit(id, SchemeAuditOperation.Insert, TypeA, false, 2, false), Assert.Single(plan.TestResultItems));
        Assert.Empty(plan.Tests);
    }

    [Fact]
    public void Detect_ResultItemRemoved_IsADeleteCarryingTheStoredTypeAndOrder()
    {
        var plan = Detect(s => Test(0, s).ResultItems.RemoveAt(0));

        Assert.Contains(plan.TestResultItems, i => i == new SchemeItemAudit(Result1, SchemeAuditOperation.Delete, TypeA, false, 1, false));
        Assert.Contains(plan.TestResultItems, i => i == new SchemeItemAudit(Result2, SchemeAuditOperation.Update, TypeA, false, 1, true));
        Assert.Equal(2, plan.TestResultItems.Count);
    }

    [Fact]
    public void Detect_ResultItemsReordered_BothAreOrderUpdates()
    {
        var plan = Detect(s => (Test(0, s).ResultItems[0], Test(0, s).ResultItems[1]) = (Test(0, s).ResultItems[1], Test(0, s).ResultItems[0]));

        Assert.Contains(plan.TestResultItems, i => i == new SchemeItemAudit(Result2, SchemeAuditOperation.Update, TypeA, false, 1, true));
        Assert.Contains(plan.TestResultItems, i => i == new SchemeItemAudit(Result1, SchemeAuditOperation.Update, TypeA, false, 2, true));
    }

    [Fact]
    public void Detect_ResultItemTypeChanged_IsATypeUpdate()
    {
        var plan = Detect(s => Test(1, s).ResultItems[0].TestResultItemTypeId = TypeA);

        Assert.Equal(new SchemeItemAudit(Result3, SchemeAuditOperation.Update, TypeA, true, 1, false), Assert.Single(plan.TestResultItems));
    }

    // Category Items

    [Fact]
    public void Detect_CategoryAdded_IsAnInsert()
    {
        var id = NewId();

        var plan = Detect(s => Test(0, s).Categories.Add(new SchemeCategoryItem { CategoryItemId = id, CategoryItemTypeId = TypeB }));

        Assert.Equal(new SchemeItemAudit(id, SchemeAuditOperation.Insert, TypeB, false, 3, false), Assert.Single(plan.CategoryItems));
        Assert.Empty(plan.CriterionItems);
    }

    [Fact]
    public void Detect_CategoryRemoved_IsADeleteAndItsCriteriaAreNotAuditedIndividually()
    {
        var plan = Detect(s => Test(0, s).Categories.RemoveAt(0));

        Assert.Contains(plan.CategoryItems, i => i == new SchemeItemAudit(Category1, SchemeAuditOperation.Delete, TypeA, false, 1, false));
        Assert.Contains(plan.CategoryItems, i => i == new SchemeItemAudit(Category2, SchemeAuditOperation.Update, TypeA, false, 1, true));
        Assert.Empty(plan.CriterionItems);
    }

    [Fact]
    public void Detect_CategoriesReordered_BothAreOrderUpdates()
    {
        var plan = Detect(s => (Test(0, s).Categories[0], Test(0, s).Categories[1]) = (Test(0, s).Categories[1], Test(0, s).Categories[0]));

        Assert.Contains(plan.CategoryItems, i => i == new SchemeItemAudit(Category2, SchemeAuditOperation.Update, TypeA, false, 1, true));
        Assert.Contains(plan.CategoryItems, i => i == new SchemeItemAudit(Category1, SchemeAuditOperation.Update, TypeA, false, 2, true));
    }

    [Fact]
    public void Detect_CategoryTypeChanged_IsNotAudited()
    {
        var plan = Detect(s => Test(0, s).Categories[0].CategoryItemTypeId = TypeB);

        Assert.True(plan.IsEmpty);
    }

    // Criterion Items

    [Fact]
    public void Detect_CriterionAdded_IsAnInsert()
    {
        var id = NewId();

        var plan = Detect(s => Test(0, s).Categories[1].Criteria.Add(new SchemeCriterionItem { CriterionItemId = id, CriterionItemTypeId = TypeB }));

        Assert.Equal(new SchemeItemAudit(id, SchemeAuditOperation.Insert, TypeB, false, 1, false), Assert.Single(plan.CriterionItems));
        Assert.Empty(plan.CategoryItems);
    }

    [Fact]
    public void Detect_CriterionRemoved_IsADeleteCarryingTheStoredTypeAndOrder()
    {
        var plan = Detect(s => Test(0, s).Categories[0].Criteria.RemoveAt(1));

        Assert.Equal(new SchemeItemAudit(Criterion2, SchemeAuditOperation.Delete, TypeA, false, 2, false), Assert.Single(plan.CriterionItems));
    }

    [Fact]
    public void Detect_CriteriaReordered_BothAreOrderUpdates()
    {
        var plan = Detect(s => (Test(0, s).Categories[0].Criteria[0], Test(0, s).Categories[0].Criteria[1]) = (Test(0, s).Categories[0].Criteria[1], Test(0, s).Categories[0].Criteria[0]));

        Assert.Contains(plan.CriterionItems, i => i == new SchemeItemAudit(Criterion2, SchemeAuditOperation.Update, TypeA, false, 1, true));
        Assert.Contains(plan.CriterionItems, i => i == new SchemeItemAudit(Criterion1, SchemeAuditOperation.Update, TypeA, false, 2, true));
    }

    [Fact]
    public void Detect_CriterionTypeChanged_IsNotAudited()
    {
        var plan = Detect(s => Test(0, s).Categories[0].Criteria[0].CriterionItemTypeId = TypeB);

        Assert.True(plan.IsEmpty);
    }

    // Tabulations

    [Fact]
    public void Detect_TabulationNameChanged_IsANameUpdate()
    {
        var plan = Detect(s => s.Tabulations[0].Name = "Renamed");

        var audit = Assert.Single(plan.Tabulations);
        Assert.Equal(new SchemeTabulationAudit(Tabulation1, SchemeAuditOperation.Update, "Renamed", true, false, false), audit);
    }

    [Fact]
    public void Detect_TabulationNameChangedOnlyInCase_IsStillAChange()
    {
        var plan = Detect(s => s.Tabulations[0].Name = "published");

        Assert.True(Assert.Single(plan.Tabulations).NameChanged);
    }

    [Fact]
    public void Detect_IntendedResultsOnlyChanged_IsAnUpdate()
    {
        var plan = Detect(s => s.Tabulations[1].IntendedResultsOnly = true);

        var audit = Assert.Single(plan.Tabulations);
        Assert.Equal(new SchemeTabulationAudit(Tabulation2, SchemeAuditOperation.Update, "Test Consultant", false, true, true), audit);
    }

    [Fact]
    public void Detect_NameAndIntendedResultsOnlyChanged_BothFlagsAreSet()
    {
        var plan = Detect(s =>
        {
            s.Tabulations[1].Name = "Renamed";
            s.Tabulations[1].IntendedResultsOnly = true;
        });

        var audit = Assert.Single(plan.Tabulations);
        Assert.True(audit.NameChanged);
        Assert.True(audit.IntendedResultsOnlyChanged);
    }

    [Fact]
    public void Detect_TabulationAdded_IsAnInsertAndItsLinksAreInserts()
    {
        var id = NewId();

        var plan = Detect(s => s.Tabulations.Add(new SchemeTabulation
        {
            TabulationId = id,
            Name = "Extra",
            IntendedResultsOnly = true,
            MethodItemIds = [Method1, Method2],
            ResultItemIds = [Result2],
        }));

        Assert.Equal(new SchemeTabulationAudit(id, SchemeAuditOperation.Insert, "Extra", false, true, false), Assert.Single(plan.Tabulations));
        Assert.Equal(2, plan.MethodLinks.Count);
        Assert.All(plan.MethodLinks, l => Assert.Equal((SchemeAuditOperation.Insert, id), (l.Operation, l.TabulationId)));
        Assert.Equal([Method1, Method2], plan.MethodLinks.Select(l => l.ItemId));
        var resultLink = Assert.Single(plan.ResultLinks);
        Assert.Equal((SchemeAuditOperation.Insert, id, Result2), (resultLink.Operation, resultLink.TabulationId, resultLink.ItemId));
    }

    [Fact]
    public void Detect_TabulationRemoved_IsADeleteAndItsLinksAreNotAuditedIndividually()
    {
        var plan = Detect(s => s.Tabulations.RemoveAt(0));

        var audit = Assert.Single(plan.Tabulations);
        Assert.Equal(new SchemeTabulationAudit(Tabulation1, SchemeAuditOperation.Delete, "Published", false, false, false), audit);
        Assert.Empty(plan.MethodLinks);
        Assert.Empty(plan.ResultLinks);
    }

    [Fact]
    public void Detect_MethodLinkAdded_IsAnInsertWithAFreshId()
    {
        var plan = Detect(s => s.Tabulations[0].MethodItemIds.Add(Method2));

        var link = Assert.Single(plan.MethodLinks);
        Assert.Equal(SchemeAuditOperation.Insert, link.Operation);
        Assert.Equal(Tabulation1, link.TabulationId);
        Assert.Equal(Method2, link.ItemId);
        Assert.NotEqual(Guid.Empty, link.LinkId);
        Assert.Empty(plan.Tabulations);
        Assert.Empty(plan.ResultLinks);
    }

    [Fact]
    public void Detect_MethodLinkRemoved_IsADeleteCarryingTheStoredLinkId()
    {
        var plan = Detect(s => s.Tabulations[0].MethodItemIds.Clear());

        Assert.Equal(new SchemeTabulationLinkAudit(MethodLink1, SchemeAuditOperation.Delete, Tabulation1, Method1), Assert.Single(plan.MethodLinks));
        Assert.Empty(plan.ResultLinks);
    }

    [Fact]
    public void Detect_ResultLinkAdded_IsAnInsertWithAFreshId()
    {
        var plan = Detect(s => s.Tabulations[1].ResultItemIds.Add(Result3));

        var link = Assert.Single(plan.ResultLinks);
        Assert.Equal(SchemeAuditOperation.Insert, link.Operation);
        Assert.Equal(Tabulation2, link.TabulationId);
        Assert.Equal(Result3, link.ItemId);
        Assert.NotEqual(Guid.Empty, link.LinkId);
        Assert.Empty(plan.MethodLinks);
    }

    [Fact]
    public void Detect_ResultLinkRemoved_IsADeleteCarryingTheStoredLinkId()
    {
        var plan = Detect(s => s.Tabulations[0].ResultItemIds.Clear());

        Assert.Equal(new SchemeTabulationLinkAudit(ResultLink1, SchemeAuditOperation.Delete, Tabulation1, Result1), Assert.Single(plan.ResultLinks));
        Assert.Empty(plan.MethodLinks);
    }

    [Fact]
    public void Detect_SeveralLinksInserted_EveryIdIsNonEmptyAndUnique()
    {
        var plan = Detect(s =>
        {
            s.Tabulations[1].MethodItemIds = [Method1, Method2, Method3];
            s.Tabulations[1].ResultItemIds = [Result1, Result2];
            s.Tabulations[0].MethodItemIds.Add(Method3);
        });

        var ids = plan.MethodLinks.Concat(plan.ResultLinks).Select(l => l.LinkId).ToList();
        Assert.Equal(6, ids.Count);
        Assert.DoesNotContain(Guid.Empty, ids);
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Detect_ExistingTabulationWithLinksKeptRemovedAndAdded_AuditsOnlyTheDifference()
    {
        var existing = Build();
        var stored = existing.Tabulations[0];
        var methodLink2 = NewId();
        var resultLink2 = NewId();
        stored.MethodItemLinks.Add(new SchemeTabulationItemLink { LinkId = methodLink2, TabulationId = Tabulation1, ItemId = Method2 });
        stored.ResultItemLinks.Add(new SchemeTabulationItemLink { LinkId = resultLink2, TabulationId = Tabulation1, ItemId = Result2 });
        var updated = Build();
        updated.Tabulations[0].MethodItemIds = [Method2, Method3];
        updated.Tabulations[0].ResultItemIds = [Result2, Result3];

        var plan = SchemeAuditDetector.Detect(existing, updated);

        Assert.Equal(
            [
                (SchemeAuditOperation.Delete, MethodLink1, Tabulation1, Method1),
                (SchemeAuditOperation.Insert, Guid.Empty, Tabulation1, Method3),
            ],
            plan.MethodLinks.Select(Describe));
        Assert.Equal(
            [
                (SchemeAuditOperation.Delete, ResultLink1, Tabulation1, Result1),
                (SchemeAuditOperation.Insert, Guid.Empty, Tabulation1, Result3),
            ],
            plan.ResultLinks.Select(Describe));
        Assert.NotEqual(Guid.Empty, plan.MethodLinks[1].LinkId);
        Assert.NotEqual(Guid.Empty, plan.ResultLinks[1].LinkId);
        Assert.Empty(plan.Tabulations);
    }

    [Fact]
    public void Detect_PostedLinkListWithADuplicateItemId_InsertsItOnce()
    {
        var plan = Detect(s => s.Tabulations[0].MethodItemIds = [Method1, Method2, Method2]);

        var link = Assert.Single(plan.MethodLinks);
        Assert.Equal((SchemeAuditOperation.Insert, Tabulation1, Method2), (link.Operation, link.TabulationId, link.ItemId));
        Assert.NotEqual(Guid.Empty, link.LinkId);
        Assert.Empty(plan.ResultLinks);
    }

    [Fact]
    public void Detect_NewTabulationWithNoLinks_IsAnInsertWithoutLinkAudits()
    {
        var id = NewId();

        var plan = Detect(s => s.Tabulations.Add(new SchemeTabulation { TabulationId = id, Name = "Empty" }));

        Assert.Equal(new SchemeTabulationAudit(id, SchemeAuditOperation.Insert, "Empty", false, false, false), Assert.Single(plan.Tabulations));
        Assert.Empty(plan.MethodLinks);
        Assert.Empty(plan.ResultLinks);
    }

    private static (SchemeAuditOperation Operation, Guid LinkId, Guid TabulationId, Guid ItemId) Describe(SchemeTabulationLinkAudit link) =>
        (link.Operation, link.Operation == SchemeAuditOperation.Insert ? Guid.Empty : link.LinkId, link.TabulationId, link.ItemId);

    // Changes that are never audited

    public static TheoryData<string, Action<CoreScheme>> NonAuditedChanges => new()
    {
        { "Name", s => s.Name = "Renamed" },
        { "Identifier", s => s.Identifier = "PT9999" },
        { "Instructions", s => s.Instructions = "Different" },
        { "Distribution month", s => s.DistributionMonthJan = true },
        { "Price changed", s => s.Prices[0].Price = 99m },
        { "Price added", s => s.Prices.Add(new SchemeCurrencyPrice { CurrencyId = Guid.NewGuid(), Price = 1m }) },
        { "Viewer added", s => s.ViewerIds.Add(Guid.NewGuid()) },
        { "Viewers removed", s => s.ViewerIds.Clear() },
        { "Test consultant 1", s => s.TestConsultant1 = Guid.NewGuid() },
        { "Test consultant 2", s => s.TestConsultant2 = Guid.NewGuid() },
        { "Test consultant 3", s => s.TestConsultant3 = Guid.NewGuid() },
        { "Test consultant tabulation", s => s.TestConsultantTabulationId = Tabulation2 },
        { "Assessor 1", s => s.Assessor1 = Guid.NewGuid() },
        { "Assessor 2", s => s.Assessor2 = Guid.NewGuid() },
        { "Assessor 3", s => s.Assessor3 = Guid.NewGuid() },
        { "Assessor 4", s => s.Assessor4 = Guid.NewGuid() },
        { "Single participant tabulation", s => s.Tabulations[0].SingleParticipantTabulation = true },
        { "Show ratings", s => s.Tabulations[0].ShowRatings = true },
        { "Available to participants", s => s.Tabulations[0].AvailableToParticipants = false },
        { "Available to viewers", s => s.Tabulations[0].AvailableToViewers = false },
        { "Test display name", s => s.Tests[0].TestType = "Renamed" },
        { "Category name", s => s.Tests[0].Categories[0].Name = "Renamed" },
        { "Criterion name", s => s.Tests[0].Categories[0].Criteria[0].Name = "Renamed" },
        { "Link list replaced with the same items", s => s.Tabulations[0].MethodItemIds = [Method1] },
    };

    [Theory]
    [MemberData(nameof(NonAuditedChanges))]
    public void Detect_NonAuditedChange_PlanIsEmpty(string change, Action<CoreScheme> edit)
    {
        var plan = Detect(edit);

        Assert.True(plan.IsEmpty, change);
    }
}
