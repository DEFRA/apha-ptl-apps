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
}
