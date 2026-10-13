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

    [Fact]
    public async Task GetMessageAsync_NoMessagePublished_ReturnsNull()
    {
        var repository = new FakeSystemMessageRepository();
        var service = new SystemMessageService(repository);

        var result = await service.GetMessageAsync();

        Assert.Null(result);
    }
}
