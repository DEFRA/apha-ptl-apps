using PTL.Core.SystemMessage;

namespace PTL.Api.Tests.SystemMessage;

public class SystemMessageServiceTests
{
    [Fact]
    public async Task GetImportantMessageAsync_MessagePublished_ReturnsRepositoryResult()
    {
        var repository = new FakeSystemMessageRepository { ImportantMessage = "<p>Planned maintenance</p>" };
        var service = new SystemMessageService(repository);

        var result = await service.GetImportantMessageAsync();

        Assert.Equal("<p>Planned maintenance</p>", result);
    }

    [Fact]
    public async Task GetImportantMessageAsync_NoMessagePublished_ReturnsNull()
    {
        var repository = new FakeSystemMessageRepository();
        var service = new SystemMessageService(repository);

        var result = await service.GetImportantMessageAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task GetMessageAsync_MessagePublished_ReturnsRepositoryResult()
    {
        var repository = new FakeSystemMessageRepository { Message = "<p>Welcome</p>" };
        var service = new SystemMessageService(repository);

        var result = await service.GetMessageAsync();

        Assert.Equal("<p>Welcome</p>", result);
    }

    // ITHC-001: the external site renders these as HTML, and legacy stored whatever was posted.
    [Fact]
    public async Task GetMessageAsync_StoredMarkupIsRestrictedToTheRichTextAllowList()
    {
        var repository = new FakeSystemMessageRepository { Message = "<p onclick=\"alert(1)\">Welcome</p><img src=x onerror=alert(1)>" };
        var service = new SystemMessageService(repository);

        Assert.Equal("<p>Welcome</p>", await service.GetMessageAsync());
    }

    // Rows saved before the save-time decode fix hold entity-escaped HTML. Decoding them is the repair ExternalWeb used
    // to apply after reading - it now happens before sanitising, so an escaped payload cannot be decoded into markup.
    [Fact]
    public async Task GetImportantMessageAsync_EntityEscapedRowIsDecodedBeforeBeingSanitised()
    {
        var repository = new FakeSystemMessageRepository { ImportantMessage = "&lt;p&gt;Planned &lt;b&gt;maintenance&lt;/b&gt;&lt;/p&gt;&lt;img src=x onerror=alert(1)&gt;" };
        var service = new SystemMessageService(repository);

        Assert.Equal("<p>Planned <b>maintenance</b></p>", await service.GetImportantMessageAsync());
    }

    [Fact]
    public async Task GetMessageAsync_NoMessagePublished_ReturnsNull()
    {
        var repository = new FakeSystemMessageRepository();
        var service = new SystemMessageService(repository);

        var result = await service.GetMessageAsync();

        Assert.Null(result);
    }
}
