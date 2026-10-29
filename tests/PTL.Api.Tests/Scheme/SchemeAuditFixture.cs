using PTL.Core.Scheme;
using CoreScheme = PTL.Core.Scheme.Scheme;

namespace PTL.Api.Tests.Scheme;

// A stored scheme with every audited child present. Build() returns a fresh, equal copy each call, so a
// test edits one copy and compares it with another.
internal static class SchemeAuditFixture
{
    private static Guid Id(int n) => Guid.Parse($"{n:x8}-0000-0000-0000-000000000000");

    public static readonly Guid SchemeId = Id(1);
    public static readonly Guid TypeA = Id(2);
    public static readonly Guid TypeB = Id(3);
    public static readonly Guid Test1 = Id(10);
    public static readonly Guid Test2 = Id(11);
    public static readonly Guid Method1 = Id(20);
    public static readonly Guid Method2 = Id(21);
    public static readonly Guid Method3 = Id(22);
    public static readonly Guid Result1 = Id(30);
    public static readonly Guid Result2 = Id(31);
    public static readonly Guid Result3 = Id(32);
    public static readonly Guid Category1 = Id(40);
    public static readonly Guid Category2 = Id(41);
    public static readonly Guid Category3 = Id(42);
    public static readonly Guid Criterion1 = Id(50);
    public static readonly Guid Criterion2 = Id(51);
    public static readonly Guid Criterion3 = Id(52);
    public static readonly Guid Tabulation1 = Id(60);
    public static readonly Guid Tabulation2 = Id(61);
    public static readonly Guid MethodLink1 = Id(70);
    public static readonly Guid ResultLink1 = Id(71);

    public static CoreScheme Build() => new()
    {
        SchemeId = SchemeId,
        SharedId = Id(2000),
        YearId = DateTime.UtcNow.Year + 1,
        Identifier = "PT1234",
        Name = "Test Scheme",
        Instructions = "Follow the packing instructions.",
        CustomsDescription = "Biological samples",
        CustomsVolume = "1kg",
        SampleOrigin = "UK",
        NumberOfSamples = 5,
        Deadline = 10,
        DistributionMonthApr = true,
        RequiresAssessment = true,
        Assessor1 = Id(3001),
        Assessor2 = Id(3002),
        ViewerIds = [Id(4001)],
        Prices = [new SchemeCurrencyPrice { SchemeCurrencyId = Id(5001), CurrencyId = Id(5002), Price = 10m }],
        Tests =
        [
            new SchemeTest
            {
                TestId = Test1,
                TestTypeId = TypeA,
                TestType = "Serology",
                SchemeId = SchemeId,
                Order = 1,
                MethodItems =
                [
                    new SchemeTestMethodItem { TestMethodItemId = Method1, TestMethodItemTypeId = TypeA, TestId = Test1, Order = 1 },
                    new SchemeTestMethodItem { TestMethodItemId = Method2, TestMethodItemTypeId = TypeA, TestId = Test1, Order = 2 },
                ],
                ResultItems =
                [
                    new SchemeTestResultItem { TestResultItemId = Result1, TestResultItemTypeId = TypeA, TestId = Test1, Order = 1 },
                    new SchemeTestResultItem { TestResultItemId = Result2, TestResultItemTypeId = TypeA, TestId = Test1, Order = 2 },
                ],
                Categories =
                [
                    new SchemeCategoryItem
                    {
                        CategoryItemId = Category1,
                        CategoryItemTypeId = TypeA,
                        Name = "Accuracy",
                        TestId = Test1,
                        Order = 1,
                        Criteria =
                        [
                            new SchemeCriterionItem { CriterionItemId = Criterion1, CriterionItemTypeId = TypeA, Name = "Within range", CategoryItemId = Category1, Order = 1 },
                            new SchemeCriterionItem { CriterionItemId = Criterion2, CriterionItemTypeId = TypeA, Name = "Reported", CategoryItemId = Category1, Order = 2 },
                        ],
                    },
                    new SchemeCategoryItem { CategoryItemId = Category2, CategoryItemTypeId = TypeA, Name = "Timeliness", TestId = Test1, Order = 2 },
                ],
            },
            new SchemeTest
            {
                TestId = Test2,
                TestTypeId = TypeB,
                TestType = "Bacteriology",
                SchemeId = SchemeId,
                Order = 2,
                MethodItems = [new SchemeTestMethodItem { TestMethodItemId = Method3, TestMethodItemTypeId = TypeB, TestId = Test2, Order = 1 }],
                ResultItems = [new SchemeTestResultItem { TestResultItemId = Result3, TestResultItemTypeId = TypeB, TestId = Test2, Order = 1 }],
                Categories =
                [
                    new SchemeCategoryItem
                    {
                        CategoryItemId = Category3,
                        CategoryItemTypeId = TypeB,
                        Name = "Accuracy",
                        TestId = Test2,
                        Order = 1,
                        Criteria = [new SchemeCriterionItem { CriterionItemId = Criterion3, CriterionItemTypeId = TypeB, Name = "Within range", CategoryItemId = Category3, Order = 1 }],
                    },
                ],
            },
        ],
        Tabulations =
        [
            new SchemeTabulation
            {
                TabulationId = Tabulation1,
                SchemeId = SchemeId,
                Name = "Published",
                AvailableToParticipants = true,
                AvailableToViewers = true,
                MethodItemIds = [Method1],
                ResultItemIds = [Result1],
                MethodItemLinks = [new SchemeTabulationItemLink { LinkId = MethodLink1, TabulationId = Tabulation1, ItemId = Method1 }],
                ResultItemLinks = [new SchemeTabulationItemLink { LinkId = ResultLink1, TabulationId = Tabulation1, ItemId = Result1 }],
            },
            new SchemeTabulation { TabulationId = Tabulation2, SchemeId = SchemeId, Name = "Test Consultant" },
        ],
    };
}
