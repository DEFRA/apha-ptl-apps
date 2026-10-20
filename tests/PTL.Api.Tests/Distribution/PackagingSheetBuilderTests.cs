using PTL.Core.Distribution;
using static PTL.Api.Tests.Distribution.PackagingTestData;

namespace PTL.Api.Tests.Distribution;

// Pins the row-construction rules ported from legacy PackagingSheet.aspx.vb
// (BuildTableCombinedPackaging / BuildTablePartCombinedPackaging / BuildTableWeekly).
public class PackagingSheetBuilderTests
{
    private static readonly DateTime Today = new(2026, 5, 13, 15, 30, 0);

    // ---------- shared formatting helpers ----------

    [Fact]
    public void PrintKey_IsGuidWithoutHyphensAndSortableDate()
    {
        Assert.Equal($"{GroupA:N}_20260513", PackagingSheetBuilder.PrintKey(GroupA, Day1));
        Assert.DoesNotContain('-', PackagingSheetBuilder.PrintKey(GroupA, Day1));
        Assert.Equal($"{GroupA:N}_20260513", PackagingSheetBuilder.PrintKey(GroupA, new DateTime(2026, 5, 13, 23, 59, 0)));
    }

    [Fact]
    public void ShortDate_UsesUkFormatting() =>
        Assert.Equal("13/05/2026", PackagingSheetBuilder.ShortDate(Day1));

    // Legacy DateTime.ToLongDateString on the UK servers (Windows en-GB "dd MMMM yyyy") gave "13 May 2026".
    // DEFECT EVIDENCE: .NET 10/ICU en-GB "D" adds the weekday ("Wednesday, 13 May 2026").
    [Fact]
    public void LongDate_MatchesTheLegacyUkLongDateWithoutAWeekday() =>
        Assert.Equal("13 May 2026", PackagingSheetBuilder.LongDate(Day1));

    [Theory]
    [InlineData(1, "Set")]
    [InlineData(0, "Sets")]
    [InlineData(2, "Sets")]
    public void SchemeDescription_UsesSingularSetOnlyForOne(int sets, string expectedWord) =>
        Assert.Equal($"17152A/BA PT0060: Mastitis bovine - {sets} {expectedWord}",
            PackagingSheetBuilder.SchemeDescription("17152", "A", "BA", "PT0060", "Mastitis bovine", sets));

    [Theory]
    [InlineData(false, false, "text", "text")]
    [InlineData(true, false, "text", "Customs Certificate Required. text")]
    [InlineData(false, true, "text", "Import/Export Licence Required. text")]
    [InlineData(true, true, "text", "Import/Export Licence Required. Customs Certificate Required. text")]
    [InlineData(true, true, "", "Import/Export Licence Required. Customs Certificate Required. ")]
    public void PsLevelPackingInfo_PrefixesCustomsFirstThenImportExportInFront(bool customs, bool licence, string info, string expected) =>
        Assert.Equal(expected, PackagingSheetBuilder.PsLevelPackingInfo(info, licence, customs));

    [Theory]
    [InlineData(false, false, "N/A")]
    [InlineData(false, true, "N/A")]
    [InlineData(true, false, "No")]
    [InlineData(true, true, "Yes")]
    public void ImportPermitStatus_OnlyAppliesWhenLicenceRequired(bool licenceRequired, bool received, string expected) =>
        Assert.Equal(expected, PackagingSheetBuilder.ImportPermitStatus(licenceRequired, received));

    [Fact]
    public void ParticipantDescription_BaseTextOnly()
    {
        var p = ParticipantRow(labCode: "L001", labName: "Lab One", sets: 1);

        Assert.Equal("L001: Lab One - 1 Set", PackagingSheetBuilder.ParticipantDescription(p));
    }

    [Fact]
    public void ParticipantDescription_AppendsPackingInfoCustomerInfoAndPsLevelInfoInOrder()
    {
        var p = ParticipantRow(sets: 2);
        p.PackingInfo = "Keep cool";
        p.CustomerPackingInfo = "Call first";
        p.PSLevelPackingInfo = "Fragile";
        p.CustomsCertificateRequired = true;
        p.ImportExportLicenceRequired = true;

        Assert.Equal(
            "L001: Lab One - 2 Sets: Keep cool, Call first, Import/Export Licence Required. Customs Certificate Required. Fragile",
            PackagingSheetBuilder.ParticipantDescription(p));
    }

    [Fact]
    public void ParticipantDescription_OmitsEmptyParts()
    {
        var p = ParticipantRow(sets: 0);
        p.CustomerPackingInfo = "Call first";

        Assert.Equal("L001: Lab One - 0 Sets, Call first", PackagingSheetBuilder.ParticipantDescription(p));
    }

    // ---------- Group Address ----------

    [Fact]
    public void Build_GroupAddress_CarriesModeWeekYearAndMonth()
    {
        var sheet = PackagingSheetBuilder.Build(PackagingMode.GroupAddress, 3, Raw(), Today);

        Assert.Equal(PackagingMode.GroupAddress, sheet.Mode);
        Assert.Equal(3, sheet.Week);
        Assert.Equal(2026, sheet.YearId);
        Assert.Equal(5, sheet.MonthId);
        Assert.Empty(sheet.Blocks);
    }

    [Fact]
    public void Build_GroupAddress_OneBlockPerGroupAndDateInRawOrder()
    {
        var raw = Raw(
            groups:
            [
                Group(GroupB, date: Day2, identifier: "GB", packingInfo: "Pack B"),
                Group(GroupA, date: Day1, identifier: "GA", packingInfo: "Pack A"),
                Group(GroupB, date: Day1, identifier: "GB"),
            ],
            items:
            [
                Item(GroupB, Scheme1, Day2, sets: 2),
                Item(GroupA, Scheme1, Day1, sets: 1),
                Item(GroupB, Scheme2, Day1, sets: 4),
            ]);

        var sheet = PackagingSheetBuilder.Build(PackagingMode.GroupAddress, 1, raw, Today);

        Assert.Equal(
            [PackagingSheetBuilder.PrintKey(GroupB, Day2), PackagingSheetBuilder.PrintKey(GroupA, Day1), PackagingSheetBuilder.PrintKey(GroupB, Day1)],
            sheet.Blocks.Select(b => b.PrintKey));
        var first = sheet.Blocks[0];
        Assert.Equal(GroupB, first.KeyId);
        Assert.Equal(Day2, first.Date);
        Assert.Equal("GB", first.Heading);
        Assert.Equal("Pack B", first.PackingInfo);
        Assert.False(first.DueToBePosted);
        Assert.False(first.ShowAllPacked);
        Assert.False(first.ShowAllPosted);
        Assert.Equal("Pack A", sheet.Blocks[1].PackingInfo);
    }

    [Fact]
    public void Build_GroupAddress_DateWithZeroTotalSetsIsSkippedEntirely()
    {
        var raw = Raw(
            groups: [Group(GroupA, date: Day1), Group(GroupA, date: Day2)],
            items: [Item(GroupA, Scheme1, Day1, sets: 0), Item(GroupA, Scheme1, Day2, sets: 2)]);

        var sheet = PackagingSheetBuilder.Build(PackagingMode.GroupAddress, 1, raw, Today);

        var block = Assert.Single(sheet.Blocks);
        Assert.Equal(Day2, block.Date);
    }

    [Fact]
    public void Build_GroupAddress_DateWithNoItemsAtAllIsSkipped()
    {
        var sheet = PackagingSheetBuilder.Build(PackagingMode.GroupAddress, 1, Raw(groups: [Group(GroupA)]), Today);

        Assert.Empty(sheet.Blocks);
    }

    [Fact]
    public void Build_GroupAddress_ZeroSetItemsAreHiddenAndGroupWithoutSetsKeepsABlockWithNoSchemeRows()
    {
        var raw = Raw(
            groups: [Group(GroupA, identifier: "GA"), Group(GroupB, identifier: "GB")],
            items:
            [
                Item(GroupA, Scheme1, sets: 3),
                Item(GroupA, Scheme2, sets: 0),
                Item(GroupB, Scheme1, sets: 0),
            ]);

        var sheet = PackagingSheetBuilder.Build(PackagingMode.GroupAddress, 1, raw, Today);

        Assert.Equal(2, sheet.Blocks.Count);
        var withSets = sheet.Blocks.Single(b => b.KeyId == GroupA);
        Assert.Equal(Scheme1, Assert.Single(withSets.Schemes).MonthlyDistributionSchemeId);
        Assert.Empty(sheet.Blocks.Single(b => b.KeyId == GroupB).Schemes);
    }

    [Fact]
    public void Build_GroupAddress_SchemeRowCarriesDescriptionPreparationAndPackingInfo()
    {
        var raw = Raw(
            groups: [Group(GroupA)],
            items: [Item(GroupA, Scheme1, sets: 1, prepComplete: true, packingInfo: "Use foil")]);

        var scheme = Assert.Single(PackagingSheetBuilder.Build(PackagingMode.GroupAddress, 1, raw, Today).Blocks[0].Schemes);

        Assert.Equal("17152A/BA PT0060: Mastitis bovine - 1 Set", scheme.Description);
        Assert.True(scheme.PreparationComplete);
        Assert.Equal("Use foil", scheme.PackingInfo);
        Assert.Equal(string.Empty, scheme.CustomerPackingInfo);
        Assert.Equal(string.Empty, scheme.PSLevelPackingInfo);
        Assert.Equal(string.Empty, scheme.ImportPermitStatus);
        Assert.Null(scheme.DistributionParticipantId);
        Assert.Null(scheme.PackagedDate);
        Assert.Null(scheme.PostedDate);
    }

    [Fact]
    public void Build_GroupAddress_ParticipantsAreJoinedByGroupSchemeAndDate()
    {
        var wanted = Guid.NewGuid();
        var raw = Raw(
            groups: [Group(GroupA, date: Day1)],
            items: [Item(GroupA, Scheme1, Day1, sets: 3)],
            participants:
            [
                ParticipantRow(GroupA, Scheme1, Day1, wanted, packaged: Day1, posted: Day2, labCode: "L001", labName: "Lab One", sets: 3),
                ParticipantRow(GroupB, Scheme1, Day1),
                ParticipantRow(GroupA, Scheme2, Day1),
                ParticipantRow(GroupA, Scheme1, Day2),
            ]);

        var scheme = Assert.Single(PackagingSheetBuilder.Build(PackagingMode.GroupAddress, 1, raw, Today).Blocks[0].Schemes);

        var row = Assert.Single(scheme.Participants);
        Assert.Equal(wanted, row.DistributionParticipantId);
        Assert.Equal("L001: Lab One - 3 Sets", row.Description);
        Assert.Equal(Day1, row.PackagedDate);
        Assert.Equal(Day2, row.PostedDate);
        Assert.Equal("N/A", row.ImportPermitStatus);
    }

    [Fact]
    public void Build_GroupAddress_ParticipantRowShowsImportPermitStatus()
    {
        var licenced = ParticipantRow(GroupA, Scheme1, Day1);
        licenced.ImportExportLicenceRequired = true;
        licenced.ImportPermitReceived = true;
        var raw = Raw(groups: [Group(GroupA)], items: [Item(GroupA, Scheme1, sets: 2)], participants: [licenced]);

        var row = Assert.Single(PackagingSheetBuilder.Build(PackagingMode.GroupAddress, 1, raw, Today).Blocks[0].Schemes[0].Participants);

        Assert.Equal("Yes", row.ImportPermitStatus);
    }

    [Fact]
    public void Build_GroupAddress_TotalsAreCompleteWhenEveryParticipantHasTheDate()
    {
        var raw = Raw(
            groups: [Group(GroupA)],
            items: [Item(GroupA, Scheme1, sets: 2, prepComplete: false)],
            participants:
            [
                ParticipantRow(GroupA, Scheme1, packaged: Day1, posted: Day1),
                ParticipantRow(GroupA, Scheme1, packaged: Day1, posted: Day1),
            ]);

        var scheme = PackagingSheetBuilder.Build(PackagingMode.GroupAddress, 1, raw, Today).Blocks[0].Schemes[0];

        Assert.Equal(PackagingTotalState.Complete, scheme.PackedTotal);
        Assert.Equal(PackagingTotalState.Complete, scheme.PostedTotal);
    }

    [Fact]
    public void Build_GroupAddress_NoParticipantsIsVacuouslyComplete()
    {
        var raw = Raw(groups: [Group(GroupA)], items: [Item(GroupA, Scheme1, sets: 2, prepComplete: false)]);

        var scheme = PackagingSheetBuilder.Build(PackagingMode.GroupAddress, 1, raw, Today).Blocks[0].Schemes[0];

        Assert.Equal(PackagingTotalState.Complete, scheme.PackedTotal);
        Assert.Equal(PackagingTotalState.Complete, scheme.PostedTotal);
        Assert.Empty(scheme.Participants);
    }

    [Fact]
    public void Build_GroupAddress_IncompleteTotalsAreSelectableOnlyOncePreparationIsComplete()
    {
        var raw = Raw(
            groups: [Group(GroupA)],
            items:
            [
                Item(GroupA, Scheme1, sets: 2, prepComplete: true),
                Item(GroupA, Scheme2, sets: 2, prepComplete: false),
            ],
            participants:
            [
                ParticipantRow(GroupA, Scheme1, packaged: Day1, posted: null),
                ParticipantRow(GroupA, Scheme1, packaged: null, posted: null),
                ParticipantRow(GroupA, Scheme2, packaged: null, posted: null),
            ]);

        var schemes = PackagingSheetBuilder.Build(PackagingMode.GroupAddress, 1, raw, Today).Blocks[0].Schemes;

        Assert.Equal(PackagingTotalState.Selectable, schemes[0].PackedTotal);
        Assert.Equal(PackagingTotalState.Selectable, schemes[0].PostedTotal);
        Assert.Equal(PackagingTotalState.None, schemes[1].PackedTotal);
        Assert.Equal(PackagingTotalState.None, schemes[1].PostedTotal);
    }

    [Fact]
    public void Build_GroupAddress_PackedCompleteButPostedNotAreIndependent()
    {
        var raw = Raw(
            groups: [Group(GroupA)],
            items: [Item(GroupA, Scheme1, sets: 2)],
            participants: [ParticipantRow(GroupA, Scheme1, packaged: Day1, posted: null)]);

        var scheme = PackagingSheetBuilder.Build(PackagingMode.GroupAddress, 1, raw, Today).Blocks[0].Schemes[0];

        Assert.Equal(PackagingTotalState.Complete, scheme.PackedTotal);
        Assert.Equal(PackagingTotalState.Selectable, scheme.PostedTotal);
    }

    // ---------- Participant Combined ----------

    [Fact]
    public void Build_ParticipantCombined_OneBlockPerParticipantRowEvenWithNoSets()
    {
        var raw = Raw(
            groups:
            [
                Group(participantId: Part1, date: Day1, labCode: "L001", labName: "Lab One", packingInfo: "Pack 1"),
                Group(participantId: Part2, date: Day2, labCode: "L002", labName: "Lab Two"),
            ],
            schemeItems: [SchemeItem(Part1, Dp1, Scheme1, Day1, sets: 0)]);

        var sheet = PackagingSheetBuilder.Build(PackagingMode.ParticipantCombined, 2, raw, Today);

        Assert.Equal(2, sheet.Blocks.Count);
        var block = sheet.Blocks[0];
        Assert.Equal(PackagingSheetBuilder.PrintKey(Part1, Day1), block.PrintKey);
        Assert.Equal(Part1, block.KeyId);
        Assert.Equal("L001: Lab One", block.Heading);
        Assert.Equal("Pack 1", block.PackingInfo);
        Assert.False(block.DueToBePosted);
        Assert.Empty(sheet.Blocks[1].Schemes);
        Assert.Equal("L002: Lab Two", sheet.Blocks[1].Heading);
    }

    [Fact]
    public void Build_ParticipantCombined_SchemeRowsMatchParticipantAndDateAndCarryTheirOwnState()
    {
        var item = SchemeItem(Part1, Dp1, Scheme1, Day1, packaged: Day1, posted: Day2, sets: 2);
        item.CustomerPackingInfo = "Ring bell";
        item.PSLevelPackingInfo = "Fragile";
        item.CustomsCertificateRequired = true;
        item.ImportExportLicenceRequired = true;
        item.ImportPermitReceived = false;
        item.PackingInfo = "Scheme pack";
        var raw = Raw(
            groups: [Group(participantId: Part1, date: Day1)],
            schemeItems:
            [
                item,
                SchemeItem(Part2, Dp2, Scheme1, Day1),
                SchemeItem(Part1, Dp3, Scheme2, Day2),
            ]);

        var scheme = Assert.Single(PackagingSheetBuilder.Build(PackagingMode.ParticipantCombined, 1, raw, Today).Blocks[0].Schemes);

        Assert.Equal(Dp1, scheme.DistributionParticipantId);
        Assert.Equal(Scheme1, scheme.MonthlyDistributionSchemeId);
        Assert.Equal("17152A/BA PT0060: Mastitis bovine - 2 Sets", scheme.Description);
        Assert.Equal(Day1, scheme.PackagedDate);
        Assert.Equal(Day2, scheme.PostedDate);
        Assert.Equal("Ring bell", scheme.CustomerPackingInfo);
        Assert.Equal("Scheme pack", scheme.PackingInfo);
        Assert.Equal("Import/Export Licence Required. Customs Certificate Required. Fragile", scheme.PSLevelPackingInfo);
        Assert.Equal("No", scheme.ImportPermitStatus);
        Assert.Empty(scheme.Participants);
        Assert.Equal(PackagingTotalState.None, scheme.PackedTotal);
        Assert.Equal(PackagingTotalState.None, scheme.PostedTotal);
    }

    [Fact]
    public void Build_ParticipantCombined_ShowAllPackedWhenSomethingUnpackedAndEveryItemPrepared()
    {
        var raw = Raw(
            groups: [Group(participantId: Part1)],
            schemeItems:
            [
                SchemeItem(Part1, Dp1, Scheme1, packaged: Day1, posted: Day1),
                SchemeItem(Part1, Dp2, Scheme2, packaged: null, posted: Day1),
            ]);

        var block = PackagingSheetBuilder.Build(PackagingMode.ParticipantCombined, 1, raw, Today).Blocks[0];

        Assert.True(block.ShowAllPacked);
        Assert.False(block.ShowAllPosted);
    }

    [Fact]
    public void Build_ParticipantCombined_ShowAllPostedWhenSomethingUnpostedAndEveryItemPrepared()
    {
        var raw = Raw(
            groups: [Group(participantId: Part1)],
            schemeItems:
            [
                SchemeItem(Part1, Dp1, Scheme1, packaged: Day1, posted: null),
                SchemeItem(Part1, Dp2, Scheme2, packaged: Day1, posted: Day1),
            ]);

        var block = PackagingSheetBuilder.Build(PackagingMode.ParticipantCombined, 1, raw, Today).Blocks[0];

        Assert.False(block.ShowAllPacked);
        Assert.True(block.ShowAllPosted);
    }

    [Fact]
    public void Build_ParticipantCombined_NoAllCheckboxesWhileAnyItemIsUnprepared()
    {
        var raw = Raw(
            groups: [Group(participantId: Part1)],
            schemeItems:
            [
                SchemeItem(Part1, Dp1, Scheme1, prepComplete: true),
                SchemeItem(Part1, Dp2, Scheme2, prepComplete: false),
            ]);

        var block = PackagingSheetBuilder.Build(PackagingMode.ParticipantCombined, 1, raw, Today).Blocks[0];

        Assert.False(block.ShowAllPacked);
        Assert.False(block.ShowAllPosted);
    }

    [Fact]
    public void Build_ParticipantCombined_NoAllCheckboxesWhenEverythingIsPackedAndPosted()
    {
        var raw = Raw(
            groups: [Group(participantId: Part1)],
            schemeItems: [SchemeItem(Part1, Dp1, packaged: Day1, posted: Day1)]);

        var block = PackagingSheetBuilder.Build(PackagingMode.ParticipantCombined, 1, raw, Today).Blocks[0];

        Assert.False(block.ShowAllPacked);
        Assert.False(block.ShowAllPosted);
    }

    // ---------- Daily ----------

    [Fact]
    public void Build_Daily_OneBlockPerItemWithLongDateHeadingAndSchemeKey()
    {
        var raw = Raw(
            groups: [Group(date: Day1)],
            items: [Item(null, Scheme1, Day1, sets: 2), Item(null, Scheme2, Day1, sets: 5)],
            participants:
            [
                ParticipantRow(null, Scheme1, Day1, Dp1, Part1, labCode: "L001", labName: "Lab One"),
                ParticipantRow(null, Scheme2, Day1, Dp2, Part2, labCode: "L002", labName: "Lab Two"),
                ParticipantRow(null, Scheme1, Day2, Dp3, Part2),
            ]);

        var sheet = PackagingSheetBuilder.Build(PackagingMode.Daily, 0, raw, Today);

        Assert.Equal(2, sheet.Blocks.Count);
        var first = sheet.Blocks[0];
        Assert.Equal(PackagingSheetBuilder.LongDate(Day1), first.Heading);
        Assert.Equal(Scheme1, first.KeyId);
        Assert.Equal(Day1, first.Date);
        Assert.Equal(PackagingSheetBuilder.PrintKey(Scheme1, Day1), first.PrintKey);
        Assert.Equal(string.Empty, first.PackingInfo);
        Assert.False(first.ShowAllPacked);
        Assert.False(first.ShowAllPosted);
        var scheme = Assert.Single(first.Schemes);
        Assert.Equal("17152A/BA PT0060: Mastitis bovine - 2 Sets", scheme.Description);
        Assert.Equal(Dp1, Assert.Single(scheme.Participants).DistributionParticipantId);
        Assert.Equal(Scheme2, sheet.Blocks[1].KeyId);
    }

    [Fact]
    public void Build_Daily_DayWithZeroTotalSetsIsSkippedAndZeroSetItemsGetNoBlock()
    {
        var raw = Raw(
            groups: [Group(date: Day1), Group(date: Day2), Group(date: Day2)],
            items:
            [
                Item(null, Scheme1, Day1, sets: 0),
                Item(null, Scheme1, Day2, sets: 0),
                Item(null, Scheme2, Day2, sets: 2),
            ]);

        var sheet = PackagingSheetBuilder.Build(PackagingMode.Daily, 0, raw, Today);

        var block = Assert.Single(sheet.Blocks);
        Assert.Equal(Scheme2, block.KeyId);
        Assert.Equal(Day2, block.Date);
    }

    [Fact]
    public void Build_Daily_DayIsDueToBePostedWhenNotCompleteAndDayIsToday()
    {
        var raw = Raw(
            groups: [Group(date: Today.Date)],
            items: [Item(null, Scheme1, Today.Date, sets: 2), Item(null, Scheme2, Today.Date, sets: 2)],
            participants: [ParticipantRow(null, Scheme1, Today.Date, posted: Today.Date), ParticipantRow(null, Scheme2, Today.Date, posted: null)]);

        var sheet = PackagingSheetBuilder.Build(PackagingMode.Daily, 0, raw, Today);

        Assert.All(sheet.Blocks, b => Assert.True(b.DueToBePosted));
        Assert.Equal(2, sheet.Blocks.Count);
    }

    [Fact]
    public void Build_Daily_PastIncompleteDayIsDueButFutureDayIsNot()
    {
        var past = Today.Date.AddDays(-2);
        var future = Today.Date.AddDays(1);
        var raw = Raw(
            groups: [Group(date: past), Group(date: future)],
            items: [Item(null, Scheme1, past, sets: 1), Item(null, Scheme2, future, sets: 1)],
            participants: [ParticipantRow(null, Scheme1, past), ParticipantRow(null, Scheme2, future)]);

        var sheet = PackagingSheetBuilder.Build(PackagingMode.Daily, 0, raw, Today);

        Assert.True(sheet.Blocks.Single(b => b.Date == past).DueToBePosted);
        Assert.False(sheet.Blocks.Single(b => b.Date == future).DueToBePosted);
    }

    [Fact]
    public void Build_Daily_FullyPostedDayIsNotDueEvenWhenInThePast()
    {
        var past = Today.Date.AddDays(-1);
        var raw = Raw(
            groups: [Group(date: past)],
            items: [Item(null, Scheme1, past, sets: 1)],
            participants: [ParticipantRow(null, Scheme1, past, posted: past)]);

        var block = Assert.Single(PackagingSheetBuilder.Build(PackagingMode.Daily, 0, raw, Today).Blocks);

        Assert.False(block.DueToBePosted);
    }

    [Fact]
    public void Build_Daily_OnlyTheFirstParticipantOfEachItemDecidesWhetherTheDayIsComplete()
    {
        var raw = Raw(
            groups: [Group(date: Today.Date)],
            items: [Item(null, Scheme1, Today.Date, sets: 2)],
            participants:
            [
                ParticipantRow(null, Scheme1, Today.Date, posted: Today.Date),
                ParticipantRow(null, Scheme1, Today.Date, posted: null),
            ]);

        var block = Assert.Single(PackagingSheetBuilder.Build(PackagingMode.Daily, 0, raw, Today).Blocks);

        Assert.False(block.DueToBePosted);
    }

    [Fact]
    public void Build_Daily_ItemsWithoutParticipantsCountAsComplete()
    {
        var raw = Raw(
            groups: [Group(date: Today.Date)],
            items: [Item(null, Scheme1, Today.Date, sets: 2)]);

        var block = Assert.Single(PackagingSheetBuilder.Build(PackagingMode.Daily, 0, raw, Today).Blocks);

        Assert.False(block.DueToBePosted);
        Assert.Empty(block.Schemes[0].Participants);
    }

    [Fact]
    public void Build_Daily_ZeroSetItemsStillCountTowardsTheDayBeingComplete()
    {
        var raw = Raw(
            groups: [Group(date: Today.Date)],
            items: [Item(null, Scheme1, Today.Date, sets: 2), Item(null, Scheme2, Today.Date, sets: 0)],
            participants:
            [
                ParticipantRow(null, Scheme1, Today.Date, posted: Today.Date),
                ParticipantRow(null, Scheme2, Today.Date, posted: null),
            ]);

        var block = Assert.Single(PackagingSheetBuilder.Build(PackagingMode.Daily, 0, raw, Today).Blocks);

        Assert.True(block.DueToBePosted);
    }

    [Fact]
    public void Build_Daily_DueIsDecidedPerDay()
    {
        var raw = Raw(
            groups: [Group(date: Day1), Group(date: Day2)],
            items: [Item(null, Scheme1, Day1, sets: 1), Item(null, Scheme2, Day2, sets: 1)],
            participants:
            [
                ParticipantRow(null, Scheme1, Day1, posted: Day1),
                ParticipantRow(null, Scheme2, Day2, posted: null),
            ]);

        var sheet = PackagingSheetBuilder.Build(PackagingMode.Daily, 0, raw, new DateTime(2026, 5, 20));

        Assert.False(sheet.Blocks.Single(b => b.Date == Day1).DueToBePosted);
        Assert.True(sheet.Blocks.Single(b => b.Date == Day2).DueToBePosted);
    }
}
