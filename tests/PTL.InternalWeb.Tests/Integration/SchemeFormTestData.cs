using PTL.Contracts.Lookup;
using PTL.Contracts.Participant;
using PTL.Contracts.Scheme;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// A scheme with something on every tab, and the lookups it points at, so the Scheme form renders every field and every
// Previous Year branch. Ids are fixed so a rendered value can be compared with the one it came from.
internal static class SchemeFormTestData
{
    public const int ClosedYearId = 2025;
    public const int OpenYearId = 2026;

    public const string Identifier = "PT1234";
    public const string Name = "Avian Influenza Serology";

    // Markup characters, so a locked value is only ever seen encoded.
    public const string Subcontractor = "Smith & Sons <Labs>";
    public const string PackingInstructions = "Keep <b>upright</b> & on ice.";

    public static readonly Guid SchemeId = Id(1);
    public static readonly Guid SharedId = Id(2);
    public static readonly Guid ScheduleId = Id(10);
    public static readonly Guid OtherScheduleId = Id(11);
    public static readonly Guid ScheduleCodeId = Id(12);
    public static readonly Guid DayId = Id(13);
    public static readonly Guid PostageId = Id(14);
    public static readonly Guid GbpId = Id(15);
    public static readonly Guid EurId = Id(16);

    public static readonly Guid TestConsultant1 = Id(600);
    public static readonly Guid TestConsultant2 = Id(601);
    public static readonly Guid TestConsultant3 = Id(602);
    public static readonly Guid[] Assessors = [Id(610), Id(611), Id(612), Id(613)];

    public static readonly Guid AssignedViewerId = Id(500);
    public static readonly Guid OtherViewerId = Id(501);

    public static readonly Guid TabulationA = Id(800);
    public static readonly Guid TabulationB = Id(801);

    // The Result Items and Method Items of the first Test, which the tabulations include.
    public static readonly Guid[] SerologyResultItems = [Id(210), Id(212), Id(214)];
    public static readonly Guid[] SerologyMethodItems = [Id(220), Id(222)];

    public static Guid Id(int number) => new($"5c4e0000-0000-0000-0000-{number:x12}");

    // The first Test has three Result Items, two Method Items and (for an assessment scheme) two Categories of three and one
    // Criteria; the second has one Result Item, no Method Items and one Category with no Criteria - so every first, middle,
    // last, only and empty case of the Up and Down rules is on the page.
    public static SchemeResponse Scheme(bool requiresAssessment, bool isReadOnly = true) => new(
        SchemeId: SchemeId,
        SharedId: SharedId,
        YearId: isReadOnly ? ClosedYearId : OpenYearId,
        Identifier: Identifier,
        Name: Name,
        ScheduleId: ScheduleId,
        ScheduleCodeId: ScheduleCodeId,
        StartDate: new DateTime(2025, 4, 14),
        DistributionMonthApr: !requiresAssessment,
        DistributionMonthMay: false,
        DistributionMonthJun: false,
        DistributionMonthJul: !requiresAssessment,
        DistributionMonthAug: false,
        DistributionMonthSep: false,
        DistributionAsAvailable: requiresAssessment,
        DistributionMonthOct: !requiresAssessment,
        DistributionMonthNov: false,
        DistributionMonthDec: false,
        DistributionMonthJan: false,
        DistributionMonthFeb: false,
        DistributionMonthMar: false,
        WeekNumber: 3,
        DayOfWeekId: DayId,
        NumberOfSamples: 5,
        SampleNoSequence: 17,
        SampleOrigin: "UK",
        Deadline: 21,
        Subcontractor: Subcontractor,
        CombinedPackaging: true,
        Postage: PostageId,
        CustomsVolume: "20 ml",
        SamplePackingInstructions: PackingInstructions,
        RequiresAssessment: requiresAssessment,
        CommentsRequired: true,
        Pilot: false,
        LimitedSampleAvailability: true,
        Accredited: false,
        NoVLALabs: true,
        ComerciallyAvailable: false,
        CustomsDescription: "Biological samples",
        DataConsentDeclarationActive: true,
        DataConsentDeclarationText: "I consent to my data being shared.",
        Instructions: "<p>Follow the <strong>sampling</strong> instructions.</p>",
        DateOfReceipt: true,
        StorageConditions: false,
        ConditionOnReceipt: true,
        TestConsultant1: requiresAssessment ? null : TestConsultant1,
        TestConsultant2: requiresAssessment ? null : TestConsultant2,
        TestConsultant3: requiresAssessment ? null : TestConsultant3,
        TestConsultantTabulationId: TabulationA,
        UseExternalReference: true,
        StoreRatings: true,
        Assessor1: requiresAssessment ? Assessors[0] : null,
        Assessor2: requiresAssessment ? Assessors[1] : null,
        Assessor3: requiresAssessment ? Assessors[2] : null,
        Assessor4: requiresAssessment ? Assessors[3] : null,
        StandardTabulationText: "Results are anonymised.",
        LastModified: new DateTime(2025, 12, 1, 9, 30, 0),
        IsReadOnly: isReadOnly,
        Prices:
        [
            new SchemeCurrencyPriceResponse(Id(700), GbpId, 40m, "British Pound", "£"),
            new SchemeCurrencyPriceResponse(Id(701), EurId, 55m, "Euro", "€"),
        ],
        ViewerIds: [AssignedViewerId],
        Tests: [Serology(), Bacteriology()],
        Tabulations:
        [
            new SchemeTabulationResponse(TabulationA, "Participant report", true, false, true, true, true, [SerologyResultItems[0]], [SerologyMethodItems[0]]),
            new SchemeTabulationResponse(TabulationB, "Viewer report", false, true, false, false, true, [SerologyResultItems[0], SerologyResultItems[1]], []),
        ]);

    public static FakeSchemeApiClient Schemes(SchemeResponse? scheme) => new() { SchemeResponse = scheme };

    public static FakeLookupApiClient Lookups() => new()
    {
        AllYears = [new YearResponse(ClosedYearId, "2025/26"), new YearResponse(OpenYearId, "2026/27")],
        Years = [new YearResponse(OpenYearId, "2026/27"), new YearResponse(OpenYearId + 1, "2027/28")],
        Schedules = [new ScheduleResponse(ScheduleId, "Quarterly Schedule"), new ScheduleResponse(OtherScheduleId, "Monthly Schedule")],
        ScheduleCodes = [new ScheduleCodeResponse(ScheduleCodeId, "SC-01")],
        Days = [new DayResponse(DayId, "Wednesday")],
        PostagePricingPlans = [new PostagePricingPlanResponse(PostageId, "Standard Postage", 1.5m, 2.5m, 3.5m, ClosedYearId)],
        Currencies =
        [
            new CurrencyResponse(GbpId, "British Pound", "£", "£ - British Pound"),
            new CurrencyResponse(EurId, "Euro", "€", "€ - Euro"),
        ],
        TestConsultants =
        [
            new SchemeUserResponse(TestConsultant1, "Ada Internal", false),
            new SchemeUserResponse(TestConsultant2, "Ben Deputy", false),
            new SchemeUserResponse(TestConsultant3, "Cleo External", true),
        ],
        Assessors =
        [
            new SchemeUserResponse(Assessors[0], "Alice Assessor", false),
            new SchemeUserResponse(Assessors[1], "Bruno Assessor", false),
            new SchemeUserResponse(Assessors[2], "Chloe Assessor", false),
            new SchemeUserResponse(Assessors[3], "Dmitri Assessor", false),
        ],
        Viewers = [new ViewerResponse(AssignedViewerId, "Alpha Viewer", string.Empty), new ViewerResponse(OtherViewerId, "Beta Viewer", string.Empty)],
        SchemeItemTypes = [new SchemeItemTypeResponse(Id(900), "Option Alpha", false)],
    };

    // The month and As Available flags by property name, in the order the Details tab lists them.
    public static IReadOnlyList<(string Field, bool Value)> Months(SchemeResponse scheme) =>
    [
        ("DistributionMonthApr", scheme.DistributionMonthApr),
        ("DistributionMonthMay", scheme.DistributionMonthMay),
        ("DistributionMonthJun", scheme.DistributionMonthJun),
        ("DistributionMonthJul", scheme.DistributionMonthJul),
        ("DistributionMonthAug", scheme.DistributionMonthAug),
        ("DistributionMonthSep", scheme.DistributionMonthSep),
        ("DistributionMonthOct", scheme.DistributionMonthOct),
        ("DistributionMonthNov", scheme.DistributionMonthNov),
        ("DistributionMonthDec", scheme.DistributionMonthDec),
        ("DistributionMonthJan", scheme.DistributionMonthJan),
        ("DistributionMonthFeb", scheme.DistributionMonthFeb),
        ("DistributionMonthMar", scheme.DistributionMonthMar),
        ("DistributionAsAvailable", scheme.DistributionAsAvailable),
    ];

    // The Details tab checkboxes Previous Year locks (Requires Assessment is disabled on any saved scheme, so it is not here).
    public static IReadOnlyList<(string Field, bool Value)> LockedFlags(SchemeResponse scheme) =>
    [
        ("CombinedPackaging", scheme.CombinedPackaging),
        ("CommentsRequired", scheme.CommentsRequired),
        ("Pilot", scheme.Pilot),
        ("LimitedSampleAvailability", scheme.LimitedSampleAvailability),
        ("Accredited", scheme.Accredited),
        ("NoVLALabs", scheme.NoVLALabs),
        ("ComerciallyAvailable", scheme.ComerciallyAvailable),
    ];

    private static SchemeTestResponse Serology() => new(
        Id(200), Id(201), "Serology", 1,
        ResultItems: [Item(210, "Titre", 1), Item(212, "Seroconversion", 2), Item(214, "Antibody status", 3)],
        MethodItems: [Item(220, "ELISA", 1), Item(222, "Virus neutralisation", 2)],
        Categories:
        [
            new SchemeCategoryItemResponse(Id(230), Id(231), "Appearance", 1, [Item(240, "Clear", 1), Item(242, "Cloudy", 2), Item(244, "Turbid", 3)]),
            new SchemeCategoryItemResponse(Id(232), Id(233), "Odour", 2, [Item(246, "None", 1)]),
        ]);

    private static SchemeTestResponse Bacteriology() => new(
        Id(300), Id(301), "Bacteriology", 2,
        ResultItems: [Item(310, "Colony count", 1)],
        MethodItems: [],
        Categories: [new SchemeCategoryItemResponse(Id(330), Id(331), "Growth", 1, [])]);

    // The item type id sits one above the item id, so neither matches an entry of the lookups' item type lists.
    private static SchemeTestItemResponse Item(int number, string name, int order) => new(Id(number), Id(number + 1), name, order);
}
