using Microsoft.Extensions.Options;
using PTL.Api.Tests.Contract;
using PTL.Core.Contract.Export.Templates;
using PTL.Core.Distribution;
using static PTL.Api.Tests.Distribution.PackagingTestData;

namespace PTL.Api.Tests.Distribution;

// Pins legacy ButtonPrintLabels_Click / ButtonPrintCustomsDocument_Click: which rows print, what
// each label contains, and how the Customs Pro-Forma merge documents are built.
public class PackagingOutputServiceTests
{
    private const string DefaultTemplateKey = "templates/customs/Customs Pro-Forma Template v20.10.2020.docx";

    private sealed class Harness
    {
        public FakeDistributionRepository Repository { get; } = new();
        public FakeTemplateStorageService Storage { get; } = new();
        public FakeTemplateMergeService Merge { get; } = new();
        public TemplateStorageOptions StorageOptions { get; } = new() { Prefix = "templates" };
        public PackagingCustomsOptions CustomsOptions { get; } = new();
        public PackagingOutputService Service { get; }

        public Harness(bool initialised = true)
        {
            if (initialised)
            {
                Repository.Schedules[(2026, 5)] = (MonthlyDistributionId, []);
            }

            Service = new PackagingOutputService(Repository, Storage, Merge, Options.Create(StorageOptions), Options.Create(CustomsOptions));
        }

        public void Sheet(PackagingMode mode, int week, PackagingRawData raw) =>
            Repository.PackagingSheets[(MonthlyDistributionId, mode, week)] = raw;

        public void WithTemplate(byte[]? content = null) => Storage.Files[DefaultTemplateKey] = content ?? [9, 9];
    }

    private static PackagingLabelAddressEntity Address(string tag, bool withContact = true) =>
        new()
        {
            ContactName = withContact ? $"{tag} contact" : string.Empty,
            Organisation = withContact ? $"{tag} org" : string.Empty,
            Address1 = $"{tag} a1",
            Address2 = $"{tag} a2",
            Address3 = string.Empty,
            Address4 = $"{tag} a4",
            Address5 = " ",
            Country = $"{tag} country",
            Telephone = $"{tag} tel",
        };

    private static PackagingRawData GroupAddressRaw() =>
        Raw(
            groups: [Group(GroupA, date: Day1), Group(GroupB, date: Day1)],
            items:
            [
                Item(GroupA, Scheme1, Day1, sets: 3),
                Item(GroupA, Scheme2, Day1, sets: 0),
                Item(GroupB, Scheme1, Day1, sets: 1),
            ],
            participants:
            [
                ParticipantRow(GroupA, Scheme1, Day1, participantId: Part1),
                ParticipantRow(GroupA, Scheme2, Day1, participantId: Part2),
                ParticipantRow(GroupA, Scheme2, Day2, participantId: Guid.NewGuid()),
                ParticipantRow(GroupB, Scheme1, Day1, participantId: Guid.NewGuid()),
            ]);

    // ---------- Labels ----------

    [Fact]
    public async Task GetLabelsAsync_MonthNotInitialised_ReturnsNull()
    {
        var h = new Harness(initialised: false);

        Assert.Null(await h.Service.GetLabelsAsync(2026, 5, PackagingMode.GroupAddress, 1, ["x"]));
    }

    [Fact]
    public async Task GetLabelsAsync_NoPackagingHeader_ReturnsNull()
    {
        var h = new Harness();

        Assert.Null(await h.Service.GetLabelsAsync(2026, 5, PackagingMode.GroupAddress, 1, ["x"]));
    }

    [Fact]
    public async Task GetLabelsAsync_EmptySelection_ReturnsNoLabelsAndLooksUpNothing()
    {
        var h = new Harness();
        h.Sheet(PackagingMode.GroupAddress, 1, GroupAddressRaw());

        var labels = await h.Service.GetLabelsAsync(2026, 5, PackagingMode.GroupAddress, 1, []);

        Assert.Empty(labels!);
        Assert.Empty(h.Repository.GroupLabelRequests);
        Assert.Empty(h.Repository.ParticipantLabelRequests);
    }

    [Fact]
    public async Task GetLabelsAsync_GroupAddress_GroupLabelThenEveryParticipantOfEveryItemIncludingZeroSetOnes()
    {
        var h = new Harness();
        h.Sheet(PackagingMode.GroupAddress, 1, GroupAddressRaw());
        h.Repository.GroupLabelAddresses[GroupA] = Address("group");
        h.Repository.ParticipantLabelAddresses[(Part1, Scheme1)] = Address("p1");
        h.Repository.ParticipantLabelAddresses[(Part2, Scheme2)] = Address("p2");
        var key = PackagingSheetBuilder.PrintKey(GroupA, Day1).ToUpperInvariant();

        var labels = await h.Service.GetLabelsAsync(2026, 5, PackagingMode.GroupAddress, 1, [key]);

        Assert.Equal(3, labels!.Count);
        // Group label: address lines only, blanks dropped, no contact/organisation.
        Assert.Equal(["group a1", "group a2", "group a4", "group country", "group tel"], labels[0].Lines);
        Assert.Equal(["p1 contact", "p1 org", "p1 a1", "p1 a2", "p1 a4", "p1 country", "p1 tel"], labels[1].Lines);
        Assert.Equal("p2 contact", labels[2].Lines[0]);
        Assert.Equal([GroupA], h.Repository.GroupLabelRequests);
        Assert.Equal([(Part1, Scheme1), (Part2, Scheme2)], h.Repository.ParticipantLabelRequests);
    }

    [Fact]
    public async Task GetLabelsAsync_NullAddressFromTheRepositoryAddsNoLabel()
    {
        var h = new Harness();
        h.Sheet(PackagingMode.GroupAddress, 1, GroupAddressRaw());
        h.Repository.ParticipantLabelAddresses[(Part1, Scheme1)] = Address("p1", withContact: false);

        var labels = await h.Service.GetLabelsAsync(2026, 5, PackagingMode.GroupAddress, 1, [PackagingSheetBuilder.PrintKey(GroupA, Day1)]);

        var label = Assert.Single(labels!);
        Assert.Equal("p1 a1", label.Lines[0]);
    }

    [Fact]
    public async Task GetLabelsAsync_ParticipantCombined_OwnLabelFirstThenOnePerSchemeRow()
    {
        var h = new Harness();
        h.Sheet(PackagingMode.ParticipantCombined, 2, Raw(
            groups: [Group(participantId: Part1, date: Day1), Group(participantId: Part2, date: Day1)],
            schemeItems:
            [
                SchemeItem(Part1, Dp1, Scheme1, Day1),
                SchemeItem(Part1, Dp2, Scheme2, Day1),
                SchemeItem(Part2, Dp3, Scheme1, Day1),
            ]));
        h.Repository.ParticipantLabelAddresses[(Part1, Guid.Empty)] = Address("own");
        h.Repository.ParticipantLabelAddresses[(Part1, Scheme1)] = Address("s1");
        h.Repository.ParticipantLabelAddresses[(Part1, Scheme2)] = Address("s2");

        var labels = await h.Service.GetLabelsAsync(2026, 5, PackagingMode.ParticipantCombined, 2, [PackagingSheetBuilder.PrintKey(Part1, Day1)]);

        Assert.Equal(3, labels!.Count);
        Assert.Equal("own contact", labels[0].Lines[0]);
        Assert.Equal("s1 contact", labels[1].Lines[0]);
        Assert.Equal("s2 contact", labels[2].Lines[0]);
        Assert.Equal([(Part1, Guid.Empty), (Part1, Scheme1), (Part1, Scheme2)], h.Repository.ParticipantLabelRequests);
    }

    [Fact]
    public async Task GetLabelsAsync_Daily_OneLabelPerParticipantOfTheBlocksSchemeOnThatDate()
    {
        var h = new Harness();
        h.Sheet(PackagingMode.Daily, 0, Raw(
            groups: [Group(date: Day1)],
            items: [Item(null, Scheme1, Day1, sets: 2), Item(null, Scheme2, Day1, sets: 2)],
            participants:
            [
                ParticipantRow(null, Scheme1, Day1, participantId: Part1),
                ParticipantRow(null, Scheme1, Day1, participantId: Part2),
                ParticipantRow(null, Scheme1, Day2, participantId: Guid.NewGuid()),
                ParticipantRow(null, Scheme2, Day1, participantId: Guid.NewGuid()),
            ]));
        h.Repository.ParticipantLabelAddresses[(Part1, Scheme1)] = Address("p1");
        h.Repository.ParticipantLabelAddresses[(Part2, Scheme1)] = Address("p2");

        var labels = await h.Service.GetLabelsAsync(2026, 5, PackagingMode.Daily, 0, [PackagingSheetBuilder.PrintKey(Scheme1, Day1)]);

        Assert.Equal(2, labels!.Count);
        Assert.Equal([(Part1, Scheme1), (Part2, Scheme1)], h.Repository.ParticipantLabelRequests);
    }

    // ---------- Customs: failures ----------

    [Fact]
    public async Task GetCustomsDocumentAsync_GroupAddress_IsNotAvailableAndReadsNothing()
    {
        var h = new Harness();

        var outcome = await h.Service.GetCustomsDocumentAsync(2026, 5, PackagingMode.GroupAddress, 1, ["x"]);

        Assert.False(outcome.Success);
        Assert.Equal("Print Customs is not available for Group Address Packaging.", outcome.Error);
        Assert.Empty(outcome.Content);
        Assert.Empty(h.Repository.PackagingSheetRequests);
    }

    [Fact]
    public async Task GetCustomsDocumentAsync_MonthNotInitialised_ReportsNoSamples()
    {
        var h = new Harness(initialised: false);

        var outcome = await h.Service.GetCustomsDocumentAsync(2026, 5, PackagingMode.Daily, 0, ["x"]);

        Assert.False(outcome.Success);
        Assert.Equal("There are no samples to be packed.", outcome.Error);
    }

    [Fact]
    public async Task GetCustomsDocumentAsync_NoPackagingHeader_ReportsNoSamples()
    {
        var h = new Harness();

        var outcome = await h.Service.GetCustomsDocumentAsync(2026, 5, PackagingMode.ParticipantCombined, 1, []);

        Assert.Equal("There are no samples to be packed.", outcome.Error);
    }

    [Fact]
    public async Task GetCustomsDocumentAsync_DailyWithNoSelectedBlocks_AsksForSchemes()
    {
        var h = new Harness();
        h.WithTemplate();
        h.Sheet(PackagingMode.Daily, 0, Raw(
            groups: [Group(date: Day1)],
            items: [Item(null, Scheme1, Day1, sets: 2)],
            participants: [ParticipantRow(null, Scheme1, Day1)]));

        var none = await h.Service.GetCustomsDocumentAsync(2026, 5, PackagingMode.Daily, 0, []);
        var unmatched = await h.Service.GetCustomsDocumentAsync(2026, 5, PackagingMode.Daily, 0, ["not-a-key"]);

        Assert.False(none.Success);
        Assert.Equal("Please select schemes to print", none.Error);
        Assert.Equal("Please select schemes to print", unmatched.Error);
        Assert.Null(h.Merge.MergedDocuments);
    }

    [Fact]
    public async Task GetCustomsDocumentAsync_TemplateMissingFromStorage_Fails()
    {
        var h = new Harness();
        h.Sheet(PackagingMode.ParticipantCombined, 1, Raw(groups: [Group(participantId: Part1)]));

        var outcome = await h.Service.GetCustomsDocumentAsync(2026, 5, PackagingMode.ParticipantCombined, 1, []);

        Assert.False(outcome.Success);
        Assert.Equal("The Customs Pro-Forma template could not be found.", outcome.Error);
        Assert.Null(h.Merge.MergedDocuments);
    }

    // ---------- Customs: documents ----------

    [Fact]
    public async Task GetCustomsDocumentAsync_ParticipantCombined_OneDocumentPerDistinctParticipantWithTheirSchemesForTheDate()
    {
        var h = new Harness();
        h.WithTemplate([7, 7, 7]);
        h.Merge.Result = [4, 5, 6];
        var first = Group(participantId: Part1, date: Day1, labCode: "L001");
        first.ContactName = "Ann";
        first.Organisation = "Org";
        first.Address1 = "1 High St";
        first.Address2 = "Town";
        first.Address3 = "County";
        first.Address4 = "Region";
        first.Address5 = "Zone";
        first.Telephone = "0123";
        first.Country = "UK";
        var otherItem = SchemeItem(Part1, Dp2, Scheme2, Day1);
        otherItem.SchemeIdentifier = "PT0070";
        otherItem.CustomsDocumentDescription = "Serum";
        otherItem.CustomsDocumentVolume = "10ml";
        otherItem.NumberOfSamples = 8;
        h.Sheet(PackagingMode.ParticipantCombined, 2, Raw(
            groups: [first, Group(participantId: Part1, date: Day2, labCode: "L001"), Group(participantId: Part2, date: Day1, labCode: "L002")],
            schemeItems:
            [
                SchemeItem(Part1, Dp1, Scheme1, Day1),
                otherItem,
                SchemeItem(Part1, Dp3, Scheme1, Day2),
                SchemeItem(Part2, Guid.NewGuid(), Scheme1, Day1),
            ]));

        var outcome = await h.Service.GetCustomsDocumentAsync(2026, 5, PackagingMode.ParticipantCombined, 2, ["ignored"]);

        Assert.True(outcome.Success);
        Assert.Null(outcome.Error);
        Assert.Equal("Combined packaging May 2026 Week 2.docx", outcome.FileName);
        Assert.Equal([4, 5, 6], outcome.Content);
        Assert.Equal([7, 7, 7], h.Merge.MergedTemplate);
        Assert.Equal(2, h.Merge.MergedDocuments!.Count);

        var document = h.Merge.MergedDocuments[0];
        Assert.Equal("Ann", document.MergeValues["ContactName"]);
        Assert.Equal("Org", document.MergeValues["Organisation"]);
        Assert.Equal("1 High St", document.MergeValues["AddressLine1"]);
        Assert.Equal("Town", document.MergeValues["AddressLine2"]);
        Assert.Equal("County", document.MergeValues["AddressLine3"]);
        Assert.Equal("Region", document.MergeValues["AddressLine4"]);
        Assert.Equal("Zone", document.MergeValues["AddressLine5"]);
        Assert.Equal("0123", document.MergeValues["Telephone"]);
        Assert.Equal("13/05/2026", document.MergeValues["Date"]);
        Assert.Equal("UK", document.MergeValues["Country"]);
        Assert.Equal("L001", document.MergeValues["Labcode"]);

        var rows = document.Regions!["FeePayingSchemes"];
        Assert.Equal(2, rows.Count);
        Assert.Equal("PT0060", rows[0]["SchemeIdentifier"]);
        Assert.Equal("Blood samples", rows[0]["CustomsDescription"]);
        Assert.Equal("5ml", rows[0]["CustomsVolume"]);
        Assert.Equal("4", rows[0]["NumberOfSamples"]);
        Assert.Equal("PT0070", rows[1]["SchemeIdentifier"]);
        Assert.Equal("Serum", rows[1]["CustomsDescription"]);
        Assert.Equal("8", rows[1]["NumberOfSamples"]);

        Assert.Equal("L002", h.Merge.MergedDocuments[1].MergeValues["Labcode"]);
        Assert.Single(h.Merge.MergedDocuments[1].Regions!["FeePayingSchemes"]);
    }

    [Fact]
    public async Task GetCustomsDocumentAsync_Daily_OneDocumentPerParticipantAcrossTheSelectedSchemes()
    {
        var h = new Harness();
        h.WithTemplate();
        var p1Scheme1 = ParticipantRow(null, Scheme1, Day1, participantId: Part1, labCode: "L001");
        p1Scheme1.ContactName = "Ann";
        var scheme2Item = Item(null, Scheme2, Day1, sets: 1, identifier: "PT0070");
        scheme2Item.CustomsDocumentDescription = "Serum";
        h.Sheet(PackagingMode.Daily, 0, Raw(
            groups: [Group(date: Day1)],
            items: [Item(null, Scheme1, Day1, sets: 2), scheme2Item, Item(null, Guid.NewGuid(), Day1, sets: 1)],
            participants:
            [
                p1Scheme1,
                ParticipantRow(null, Scheme2, Day1, participantId: Part1, labCode: "L001"),
                ParticipantRow(null, Scheme1, Day1, participantId: Part2, labCode: "L002"),
                ParticipantRow(null, Scheme1, Day2, participantId: Part1, labCode: "L001"),
                ParticipantRow(null, Guid.Parse("30000000-0000-0000-0000-000000000003"), Day1, participantId: Guid.NewGuid(), labCode: "L003"),
            ]));

        var outcome = await h.Service.GetCustomsDocumentAsync(
            2026, 5, PackagingMode.Daily, 0, [PackagingSheetBuilder.PrintKey(Scheme1, Day1), PackagingSheetBuilder.PrintKey(Scheme2, Day1)]);

        Assert.True(outcome.Success);
        Assert.Equal("Daily packaging May 2026.docx", outcome.FileName);
        Assert.Equal(2, h.Merge.MergedDocuments!.Count);

        var ann = h.Merge.MergedDocuments[0];
        Assert.Equal("L001", ann.MergeValues["Labcode"]);
        Assert.Equal("Ann", ann.MergeValues["ContactName"]);
        Assert.Equal("13/05/2026", ann.MergeValues["Date"]);
        // Part1's Day1 rows for Scheme1 and Scheme2 each match an item; the Day2 row has none and is dropped.
        var rows = ann.Regions!["FeePayingSchemes"];
        Assert.Equal(["PT0060", "PT0070"], rows.Select(r => r["SchemeIdentifier"]));
        Assert.Equal("Serum", rows[1]["CustomsDescription"]);

        var other = h.Merge.MergedDocuments[1];
        Assert.Equal("L002", other.MergeValues["Labcode"]);
        Assert.Equal("PT0060", Assert.Single(other.Regions!["FeePayingSchemes"])["SchemeIdentifier"]);
    }

    [Theory]
    [InlineData("templates", "templates/customs/Customs Pro-Forma Template v20.10.2020.docx")]
    [InlineData("/templates/", "templates/customs/Customs Pro-Forma Template v20.10.2020.docx")]
    [InlineData("", "customs/Customs Pro-Forma Template v20.10.2020.docx")]
    [InlineData("/", "customs/Customs Pro-Forma Template v20.10.2020.docx")]
    public async Task GetCustomsDocumentAsync_ReadsTheTemplateFromTheConfiguredKey(string prefix, string expectedKey)
    {
        var h = new Harness();
        h.StorageOptions.Prefix = prefix;
        h.Storage.Files[expectedKey] = [1];
        h.Sheet(PackagingMode.ParticipantCombined, 1, Raw(groups: [Group(participantId: Part1)]));

        var outcome = await h.Service.GetCustomsDocumentAsync(2026, 5, PackagingMode.ParticipantCombined, 1, []);

        Assert.True(outcome.Success);
        Assert.Equal([1], h.Merge.MergedTemplate);
    }

    [Fact]
    public async Task GetCustomsDocumentAsync_HonoursConfiguredFolderAndFileName()
    {
        var h = new Harness();
        h.CustomsOptions.Folder = "/forms/";
        h.CustomsOptions.TemplateFileName = "Customs.docx";
        h.Storage.Files["templates/forms/Customs.docx"] = [2];
        h.Sheet(PackagingMode.ParticipantCombined, 1, Raw(groups: [Group(participantId: Part1)]));

        var outcome = await h.Service.GetCustomsDocumentAsync(2026, 5, PackagingMode.ParticipantCombined, 1, []);

        Assert.True(outcome.Success);
    }

    [Fact]
    public async Task GetCustomsDocumentAsync_NullStoragePrefixIsTreatedAsEmpty()
    {
        var h = new Harness();
        h.StorageOptions.Prefix = null!;
        h.Storage.Files["customs/Customs Pro-Forma Template v20.10.2020.docx"] = [3];
        h.Sheet(PackagingMode.ParticipantCombined, 1, Raw(groups: [Group(participantId: Part1)]));

        var outcome = await h.Service.GetCustomsDocumentAsync(2026, 5, PackagingMode.ParticipantCombined, 1, []);

        Assert.True(outcome.Success);
    }
}
