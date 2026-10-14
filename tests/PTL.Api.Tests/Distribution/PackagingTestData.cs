using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

// Entity builders shared by the Sample Packaging tests. Defaults describe one scheme
// ("17152A/BA PT0060: Mastitis bovine") so a test only states what it is about.
internal static class PackagingTestData
{
    public static readonly Guid GroupA = Guid.Parse("a0000000-0000-0000-0000-00000000000a");
    public static readonly Guid GroupB = Guid.Parse("b0000000-0000-0000-0000-00000000000b");
    public static readonly Guid Scheme1 = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid Scheme2 = Guid.Parse("20000000-0000-0000-0000-000000000002");
    public static readonly Guid Part1 = Guid.Parse("c0000000-0000-0000-0000-0000000000c1");
    public static readonly Guid Part2 = Guid.Parse("c0000000-0000-0000-0000-0000000000c2");
    public static readonly Guid Dp1 = Guid.Parse("d0000000-0000-0000-0000-0000000000d1");
    public static readonly Guid Dp2 = Guid.Parse("d0000000-0000-0000-0000-0000000000d2");
    public static readonly Guid Dp3 = Guid.Parse("d0000000-0000-0000-0000-0000000000d3");
    public static readonly Guid MonthlyDistributionId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static readonly DateTime Day1 = new(2026, 5, 13);
    public static readonly DateTime Day2 = new(2026, 5, 14);

    public static PackagingGroupEntity Group(
        Guid? groupAddressId = null, Guid? participantId = null, DateTime? date = null, string identifier = "G1", string packingInfo = "",
        string labCode = "L001", string labName = "Lab One") =>
        new()
        {
            GroupAddressId = groupAddressId ?? Guid.Empty,
            ParticipantId = participantId ?? Guid.Empty,
            DistributionDate = date ?? Day1,
            Identifier = identifier,
            PackingInfo = packingInfo,
            LabCode = labCode,
            LabName = labName,
        };

    public static PackagingItemEntity Item(
        Guid? groupAddressId = null, Guid? schemeId = null, DateTime? date = null, int sets = 3, bool prepComplete = true,
        string identifier = "PT0060", string name = "Mastitis bovine", string packingInfo = "") =>
        new()
        {
            GroupAddressId = groupAddressId ?? Guid.Empty,
            MonthlyDistributionSchemeId = schemeId ?? Scheme1,
            DistributionDate = date ?? Day1,
            NumberOfSetsRequired = sets,
            PreparationComplete = prepComplete,
            SchemeIdentifier = identifier,
            SchemeName = name,
            DistributionReference = "17152",
            DistributionReferenceSuffix = "A",
            ScheduleCode = "BA",
            PackingInfo = packingInfo,
            CustomsDocumentDescription = "Blood samples",
            CustomsDocumentVolume = "5ml",
            NumberOfSamples = 4,
        };

    public static PackagingParticipantEntity ParticipantRow(
        Guid? groupAddressId = null, Guid? schemeId = null, DateTime? date = null, Guid? distributionParticipantId = null,
        Guid? participantId = null, DateTime? packaged = null, DateTime? posted = null, int sets = 1,
        string labCode = "L001", string labName = "Lab One") =>
        new()
        {
            GroupAddressId = groupAddressId ?? Guid.Empty,
            MonthlyDistributionSchemeId = schemeId ?? Scheme1,
            DistributionDate = date ?? Day1,
            DistributionParticipantId = distributionParticipantId ?? Guid.NewGuid(),
            ParticipantId = participantId ?? Part1,
            PackagedDate = packaged,
            PostedDate = posted,
            NumberOfSetsRequired = sets,
            LabCode = labCode,
            LabName = labName,
            ContactName = "Contact " + labCode,
            Organisation = "Org " + labCode,
            Address1 = "1 High St",
            Address2 = "Town",
            Address3 = "County",
            Address4 = "A4",
            Address5 = "A5",
            Telephone = "0123",
            Country = "UK",
        };

    public static PackagingSchemeItemEntity SchemeItem(
        Guid? participantId = null, Guid? distributionParticipantId = null, Guid? schemeId = null, DateTime? date = null,
        bool prepComplete = true, DateTime? packaged = null, DateTime? posted = null, int sets = 2) =>
        new()
        {
            ParticipantId = participantId ?? Part1,
            DistributionParticipantId = distributionParticipantId ?? Guid.NewGuid(),
            MonthlyDistributionSchemeId = schemeId ?? Scheme1,
            DistributionDate = date ?? Day1,
            PreparationComplete = prepComplete,
            PackagedDate = packaged,
            PostedDate = posted,
            NumberOfSetsRequired = sets,
            SchemeIdentifier = "PT0060",
            SchemeName = "Mastitis bovine",
            DistributionReference = "17152",
            DistributionReferenceSuffix = "A",
            ScheduleCode = "BA",
            CustomsDocumentDescription = "Blood samples",
            CustomsDocumentVolume = "5ml",
            NumberOfSamples = 4,
        };

    public static PackagingRawData Raw(
        IEnumerable<PackagingGroupEntity>? groups = null,
        IEnumerable<PackagingItemEntity>? items = null,
        IEnumerable<PackagingParticipantEntity>? participants = null,
        IEnumerable<PackagingSchemeItemEntity>? schemeItems = null) =>
        new(2026, 5, [.. groups ?? []], [.. items ?? []], [.. participants ?? []], [.. schemeItems ?? []]);
}
