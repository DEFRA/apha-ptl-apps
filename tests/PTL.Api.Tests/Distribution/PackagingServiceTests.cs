using PTL.Core.Distribution;
using static PTL.Api.Tests.Distribution.PackagingTestData;

namespace PTL.Api.Tests.Distribution;

// Pins legacy PackagingSheet.aspx.vb read and ButtonSave_Click/ButtonApply_Click save behaviour:
// ticking Packed/Posted stamps today's date on an empty date only, Posted also stamps Packed and
// queues the Distribution Posted email.
public class PackagingServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 13, 10, 30, 0, TimeSpan.Zero);

    private sealed class Harness
    {
        public FakeDistributionRepository Repository { get; } = new();
        public FakePostedNotifier Notifier { get; } = new();
        public PackagingService Service { get; }

        public Harness(bool initialised = true)
        {
            if (initialised)
            {
                Repository.Schedules[(2026, 5)] = (MonthlyDistributionId, []);
            }

            Service = new PackagingService(Repository, Notifier, new FixedTimeProvider(Now));
        }

        public void Sheet(PackagingMode mode, int week, PackagingRawData raw) =>
            Repository.PackagingSheets[(MonthlyDistributionId, mode, week)] = raw;
    }

    private static PackagingRawData GroupRaw(Guid dp, DateTime? packaged = null, DateTime? posted = null, bool prepComplete = true) =>
        Raw(
            groups: [Group(GroupA)],
            items: [Item(GroupA, Scheme1, sets: 2, prepComplete: prepComplete)],
            participants: [ParticipantRow(GroupA, Scheme1, distributionParticipantId: dp, participantId: Part1, packaged: packaged, posted: posted)]);

    private static PackagingStageUpdate Tick(Guid dp, bool packed, bool posted, PackagingMode mode = PackagingMode.GroupAddress, int week = 1) =>
        new(mode, week, dp, packed, posted);

    // ---------- GetPackagingAsync ----------

    [Fact]
    public async Task GetPackagingAsync_MonthNotInitialised_ReturnsNullWithoutFetchingTheSheet()
    {
        var h = new Harness(initialised: false);

        var result = await h.Service.GetPackagingAsync(2026, 5, PackagingMode.GroupAddress, 1);

        Assert.Null(result);
        Assert.Empty(h.Repository.PackagingSheetRequests);
    }

    [Fact]
    public async Task GetPackagingAsync_RepositoryHasNoHeader_ReturnsNull()
    {
        var h = new Harness();

        var result = await h.Service.GetPackagingAsync(2026, 5, PackagingMode.ParticipantCombined, 2);

        Assert.Null(result);
        Assert.Equal((MonthlyDistributionId, PackagingMode.ParticipantCombined, 2), Assert.Single(h.Repository.PackagingSheetRequests));
    }

    [Fact]
    public async Task GetPackagingAsync_ReturnsBuilderResultForTheGivenModeAndWeek()
    {
        var h = new Harness();
        h.Sheet(PackagingMode.ParticipantCombined, 3, Raw(
            groups: [Group(participantId: Part1, labCode: "L009", labName: "Lab Nine")],
            schemeItems: [SchemeItem(Part1, Dp1, Scheme1)]));

        var result = await h.Service.GetPackagingAsync(2026, 5, PackagingMode.ParticipantCombined, 3);

        Assert.NotNull(result);
        Assert.Equal(PackagingMode.ParticipantCombined, result!.Mode);
        Assert.Equal(3, result.Week);
        Assert.Equal("L009: Lab Nine", Assert.Single(result.Blocks).Heading);
    }

    [Fact]
    public async Task GetPackagingAsync_Daily_UsesTheClockToDecideWhichDaysAreDue()
    {
        var h = new Harness();
        h.Sheet(PackagingMode.Daily, 0, Raw(
            groups: [Group(date: Now.UtcDateTime.Date)],
            items: [Item(null, Scheme1, Now.UtcDateTime.Date, sets: 1)],
            participants: [ParticipantRow(null, Scheme1, Now.UtcDateTime.Date)]));

        var result = await h.Service.GetPackagingAsync(2026, 5, PackagingMode.Daily, 0);

        Assert.True(Assert.Single(result!.Blocks).DueToBePosted);
    }

    // ---------- SavePackagingAsync ----------

    [Fact]
    public async Task SavePackagingAsync_MonthNotInitialised_FailsAndTouchesNothingElse()
    {
        var h = new Harness(initialised: false);

        var outcome = await h.Service.SavePackagingAsync(2026, 5, [Tick(Dp1, true, true)]);

        Assert.False(outcome.Success);
        Assert.Empty(h.Repository.PackagingSheetRequests);
        Assert.Empty(h.Notifier.BuildRequests);
        Assert.Empty(h.Repository.PackagingUpdates);
        Assert.Empty(h.Notifier.Dispatched);
    }

    [Fact]
    public async Task SavePackagingAsync_NoTicks_SucceedsWithoutFetchingOrPersistingAnything()
    {
        var h = new Harness();

        var outcome = await h.Service.SavePackagingAsync(2026, 5, [Tick(Dp1, false, false)]);

        Assert.True(outcome.Success);
        Assert.Empty(h.Repository.PackagingSheetRequests);
        Assert.Empty(h.Repository.PackagingUpdates);
        Assert.Empty(h.Notifier.Dispatched);
        Assert.Empty(Assert.Single(h.Notifier.BuildRequests));
    }

    [Fact]
    public async Task SavePackagingAsync_PackedTick_StampsPackedDateOnlyAndSendsNoEmail()
    {
        var h = new Harness();
        h.Sheet(PackagingMode.GroupAddress, 1, GroupRaw(Dp1));

        var outcome = await h.Service.SavePackagingAsync(2026, 5, [Tick(Dp1, packed: true, posted: false)]);

        Assert.True(outcome.Success);
        var (updates, emails) = Assert.Single(h.Repository.PackagingUpdates);
        var update = Assert.Single(updates);
        Assert.Equal(new PackagingProgressUpdate(Dp1, Now.UtcDateTime, null), update);
        Assert.Empty(emails);
        Assert.Empty(Assert.Single(h.Notifier.BuildRequests));
        Assert.Single(h.Notifier.Dispatched);
    }

    [Fact]
    public async Task SavePackagingAsync_PackedTickOnAlreadyPackedRow_NeverOverwritesAndPersistsNothing()
    {
        var h = new Harness();
        h.Sheet(PackagingMode.GroupAddress, 1, GroupRaw(Dp1, packaged: Day1));

        await h.Service.SavePackagingAsync(2026, 5, [Tick(Dp1, true, false)]);

        Assert.Empty(h.Repository.PackagingUpdates);
        Assert.Empty(h.Notifier.Dispatched);
    }

    [Fact]
    public async Task SavePackagingAsync_PostedTick_StampsPackedAndPostedAndQueuesTheEmail()
    {
        var h = new Harness();
        h.Sheet(PackagingMode.GroupAddress, 1, GroupRaw(Dp1));
        var queued = new DistributionPostedEmailEntity { DistributionPostedEmailId = Guid.NewGuid(), Email = "a@example.test" };
        h.Notifier.EmailsToReturn.Add(queued);

        await h.Service.SavePackagingAsync(2026, 5, [Tick(Dp1, packed: false, posted: true)]);

        var posted = Assert.Single(Assert.Single(h.Notifier.BuildRequests));
        Assert.Equal(new PostedParticipant(Part1, Scheme1, "17152A/BA", "PT0060: Mastitis bovine"), posted);
        var (updates, emails) = Assert.Single(h.Repository.PackagingUpdates);
        Assert.Equal(new PackagingProgressUpdate(Dp1, Now.UtcDateTime, Now.UtcDateTime), Assert.Single(updates));
        Assert.Same(queued, Assert.Single(emails));
        Assert.Same(queued, Assert.Single(Assert.Single(h.Notifier.Dispatched)));
    }

    [Fact]
    public async Task SavePackagingAsync_PostedTickKeepsAnExistingPackedDate()
    {
        var h = new Harness();
        h.Sheet(PackagingMode.GroupAddress, 1, GroupRaw(Dp1, packaged: Day1));

        await h.Service.SavePackagingAsync(2026, 5, [Tick(Dp1, true, true)]);

        var update = Assert.Single(Assert.Single(h.Repository.PackagingUpdates).Updates);
        Assert.Equal(new PackagingProgressUpdate(Dp1, Day1, Now.UtcDateTime), update);
    }

    [Fact]
    public async Task SavePackagingAsync_AlreadyPostedRow_ProducesNoChangeAndNoEmail()
    {
        var h = new Harness();
        h.Sheet(PackagingMode.GroupAddress, 1, GroupRaw(Dp1, packaged: Day1, posted: Day1));

        await h.Service.SavePackagingAsync(2026, 5, [Tick(Dp1, true, true)]);

        Assert.Empty(h.Repository.PackagingUpdates);
        Assert.Empty(Assert.Single(h.Notifier.BuildRequests));
        Assert.Empty(h.Notifier.Dispatched);
    }

    // Data oddity: Posted set but Packed empty - the code still stamps Packed (and keeps Posted, no email).
    [Fact]
    public async Task SavePackagingAsync_PostedAlreadySetButPackedEmpty_StampsPackedAndKeepsPostedWithoutAnEmail()
    {
        var h = new Harness();
        h.Sheet(PackagingMode.GroupAddress, 1, GroupRaw(Dp1, packaged: null, posted: Day2));

        await h.Service.SavePackagingAsync(2026, 5, [Tick(Dp1, false, true)]);

        var (updates, emails) = Assert.Single(h.Repository.PackagingUpdates);
        Assert.Equal(new PackagingProgressUpdate(Dp1, Now.UtcDateTime, Day2), Assert.Single(updates));
        Assert.Empty(emails);
        Assert.Empty(Assert.Single(h.Notifier.BuildRequests));
    }

    [Fact]
    public async Task SavePackagingAsync_UnpreparedSchemeRowIsIgnored()
    {
        var h = new Harness();
        h.Sheet(PackagingMode.GroupAddress, 1, GroupRaw(Dp1, prepComplete: false));

        await h.Service.SavePackagingAsync(2026, 5, [Tick(Dp1, true, true)]);

        Assert.Empty(h.Repository.PackagingUpdates);
    }

    [Fact]
    public async Task SavePackagingAsync_UnknownParticipantIdIsIgnored()
    {
        var h = new Harness();
        h.Sheet(PackagingMode.GroupAddress, 1, GroupRaw(Dp1));

        await h.Service.SavePackagingAsync(2026, 5, [Tick(Dp2, true, true)]);

        Assert.Empty(h.Repository.PackagingUpdates);
    }

    [Fact]
    public async Task SavePackagingAsync_ParticipantWithNoMatchingItemIsIgnored()
    {
        var h = new Harness();
        h.Sheet(PackagingMode.GroupAddress, 1, Raw(
            groups: [Group(GroupA)],
            items: [Item(GroupA, Scheme2, sets: 1)],
            participants: [ParticipantRow(GroupA, Scheme1, distributionParticipantId: Dp1)]));

        await h.Service.SavePackagingAsync(2026, 5, [Tick(Dp1, true, false)]);

        Assert.Empty(h.Repository.PackagingUpdates);
    }

    [Fact]
    public async Task SavePackagingAsync_DuplicateParticipantUpdatesInOneSheetAreProcessedOnce()
    {
        var h = new Harness();
        h.Sheet(PackagingMode.GroupAddress, 1, GroupRaw(Dp1));

        await h.Service.SavePackagingAsync(2026, 5, [Tick(Dp1, true, false), Tick(Dp1, true, true)]);

        var (updates, _) = Assert.Single(h.Repository.PackagingUpdates);
        Assert.Equal(new PackagingProgressUpdate(Dp1, Now.UtcDateTime, null), Assert.Single(updates));
    }

    [Fact]
    public async Task SavePackagingAsync_FetchesOncePerDistinctModeAndWeek()
    {
        var h = new Harness();
        h.Sheet(PackagingMode.GroupAddress, 1, GroupRaw(Dp1));
        h.Sheet(PackagingMode.GroupAddress, 2, Raw(
            groups: [Group(GroupB)],
            items: [Item(GroupB, Scheme1, sets: 1)],
            participants: [ParticipantRow(GroupB, Scheme1, distributionParticipantId: Dp2)]));

        await h.Service.SavePackagingAsync(2026, 5,
        [
            Tick(Dp1, true, false, PackagingMode.GroupAddress, 1),
            Tick(Dp1, true, false, PackagingMode.GroupAddress, 1),
            Tick(Dp2, true, false, PackagingMode.GroupAddress, 2),
        ]);

        Assert.Equal(2, h.Repository.PackagingSheetRequests.Count);
        Assert.Contains((MonthlyDistributionId, PackagingMode.GroupAddress, 1), h.Repository.PackagingSheetRequests);
        Assert.Contains((MonthlyDistributionId, PackagingMode.GroupAddress, 2), h.Repository.PackagingSheetRequests);
        var (updates, _) = Assert.Single(h.Repository.PackagingUpdates);
        Assert.Equal([Dp1, Dp2], updates.Select(u => u.DistributionParticipantId));
    }

    [Fact]
    public async Task SavePackagingAsync_DailyForcesWeekZeroSoDifferentWeeksShareOneFetch()
    {
        var h = new Harness();
        h.Sheet(PackagingMode.Daily, 0, GroupRaw(Dp1));

        await h.Service.SavePackagingAsync(2026, 5,
        [
            Tick(Dp1, true, false, PackagingMode.Daily, 4),
            Tick(Dp2, true, false, PackagingMode.Daily, 1),
        ]);

        Assert.Equal((MonthlyDistributionId, PackagingMode.Daily, 0), Assert.Single(h.Repository.PackagingSheetRequests));
        Assert.Equal(Dp1, Assert.Single(Assert.Single(h.Repository.PackagingUpdates).Updates).DistributionParticipantId);
    }

    [Fact]
    public async Task SavePackagingAsync_SheetWithNoHeaderIsSkipped()
    {
        var h = new Harness();

        var outcome = await h.Service.SavePackagingAsync(2026, 5, [Tick(Dp1, true, true)]);

        Assert.True(outcome.Success);
        Assert.Single(h.Repository.PackagingSheetRequests);
        Assert.Empty(h.Repository.PackagingUpdates);
    }

    [Fact]
    public async Task SavePackagingAsync_ParticipantCombinedRowsComeFromTheSchemeItems()
    {
        var h = new Harness();
        h.Sheet(PackagingMode.ParticipantCombined, 2, Raw(
            groups: [Group(participantId: Part2)],
            schemeItems:
            [
                SchemeItem(Part2, Dp1, Scheme2, packaged: null, posted: null),
                SchemeItem(Part2, Dp2, Scheme1, prepComplete: false),
            ]));

        await h.Service.SavePackagingAsync(2026, 5,
        [
            Tick(Dp1, false, true, PackagingMode.ParticipantCombined, 2),
            Tick(Dp2, true, true, PackagingMode.ParticipantCombined, 2),
        ]);

        var posted = Assert.Single(Assert.Single(h.Notifier.BuildRequests));
        Assert.Equal(new PostedParticipant(Part2, Scheme2, "17152A/BA", "PT0060: Mastitis bovine"), posted);
        var update = Assert.Single(Assert.Single(h.Repository.PackagingUpdates).Updates);
        Assert.Equal(new PackagingProgressUpdate(Dp1, Now.UtcDateTime, Now.UtcDateTime), update);
    }

    [Fact]
    public async Task SavePackagingAsync_DuplicateIdAcrossSchemeItemsAndParticipants_FirstRowWins()
    {
        var h = new Harness();
        h.Sheet(PackagingMode.GroupAddress, 1, Raw(
            groups: [Group(GroupA)],
            items: [Item(GroupA, Scheme1, sets: 1)],
            participants:
            [
                ParticipantRow(GroupA, Scheme1, distributionParticipantId: Dp1, packaged: null),
                ParticipantRow(GroupA, Scheme1, distributionParticipantId: Dp1, packaged: Day1),
            ],
            schemeItems: [SchemeItem(Part1, Dp1, Scheme1, packaged: Day2)]));

        await h.Service.SavePackagingAsync(2026, 5, [Tick(Dp1, true, false)]);

        // The scheme-item row (packaged Day2) is registered first, so nothing needs stamping.
        Assert.Empty(h.Repository.PackagingUpdates);
    }

    [Fact]
    public async Task SavePackagingAsync_EmailsWithNoProgressStillPersistAndDispatch()
    {
        var h = new Harness();
        h.Notifier.ReturnEmailsEvenWhenNothingPosted = true;
        var email = new DistributionPostedEmailEntity { DistributionPostedEmailId = Guid.NewGuid(), Email = "b@example.test" };
        h.Notifier.EmailsToReturn.Add(email);

        var outcome = await h.Service.SavePackagingAsync(2026, 5, []);

        Assert.True(outcome.Success);
        var (updates, emails) = Assert.Single(h.Repository.PackagingUpdates);
        Assert.Empty(updates);
        Assert.Same(email, Assert.Single(emails));
        Assert.Same(email, Assert.Single(Assert.Single(h.Notifier.Dispatched)));
    }
}
