using PTL.Core.Distribution;
using PTL.Data.Storage;

namespace PTL.Data.Tests.Storage;

public class AttachmentStagingStoreTests
{
    private static readonly Guid SchemeId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid OtherSchemeId = Guid.Parse("11111111-0000-0000-0000-000000000002");
    private static readonly Guid Token = Guid.Parse("22222222-0000-0000-0000-000000000001");
    private static readonly Guid OtherToken = Guid.Parse("22222222-0000-0000-0000-000000000002");
    private static readonly StagedAttachment File = new("report.pdf", "application/pdf", [1, 2, 3]);

    private sealed class ManualTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task SaveGetDelete_RoundTripsByScheme()
    {
        var store = new InMemoryAttachmentStagingStore(new ManualTimeProvider());

        await store.SaveAsync(SchemeId, Token, File);

        Assert.Same(File, await store.GetAsync(SchemeId, Token));
        Assert.Null(await store.GetAsync(OtherSchemeId, Token));

        await store.DeleteAsync(SchemeId, Token);

        Assert.Null(await store.GetAsync(SchemeId, Token));
    }

    [Fact]
    public async Task Save_NullAttachment_Throws()
    {
        var store = new InMemoryAttachmentStagingStore(new ManualTimeProvider());

        await Assert.ThrowsAsync<ArgumentNullException>(() => store.SaveAsync(SchemeId, Token, null!));
    }

    [Fact]
    public async Task Get_AfterTheExpiry_ReturnsNull()
    {
        var clock = new ManualTimeProvider();
        var store = new InMemoryAttachmentStagingStore(clock);
        await store.SaveAsync(SchemeId, Token, File);

        clock.Now += InMemoryAttachmentStagingStore.Expiry + TimeSpan.FromSeconds(1);

        Assert.Null(await store.GetAsync(SchemeId, Token));
    }

    [Fact]
    public async Task Get_SlidesTheExpiry()
    {
        var clock = new ManualTimeProvider();
        var store = new InMemoryAttachmentStagingStore(clock);
        await store.SaveAsync(SchemeId, Token, File);

        clock.Now += InMemoryAttachmentStagingStore.Expiry - TimeSpan.FromSeconds(1);
        Assert.Same(File, await store.GetAsync(SchemeId, Token));

        clock.Now += InMemoryAttachmentStagingStore.Expiry - TimeSpan.FromSeconds(1);
        Assert.Same(File, await store.GetAsync(SchemeId, Token));
    }

    [Fact]
    public async Task Save_RemovesTheExpiredEntriesOfOtherUploads()
    {
        var clock = new ManualTimeProvider();
        var store = new InMemoryAttachmentStagingStore(clock);
        await store.SaveAsync(SchemeId, Token, File);

        clock.Now += InMemoryAttachmentStagingStore.Expiry + TimeSpan.FromSeconds(1);
        await store.SaveAsync(SchemeId, OtherToken, File);

        Assert.Null(await store.GetAsync(SchemeId, Token));
        Assert.Same(File, await store.GetAsync(SchemeId, OtherToken));
    }
}
