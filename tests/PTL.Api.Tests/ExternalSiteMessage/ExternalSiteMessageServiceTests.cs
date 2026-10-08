using PTL.Core.ExternalSiteMessage;

namespace PTL.Api.Tests.ExternalSiteMessage;

public class ExternalSiteMessageServiceTests
{
    private static ExternalSiteMessageService CreateService(FakeExternalSiteMessageRepository repository) => new(repository);

    [Fact]
    public async Task GetAsync_ReturnsRepositoryValue()
    {
        var repository = new FakeExternalSiteMessageRepository
        {
            Message = new PTL.Core.ExternalSiteMessage.ExternalSiteMessage { Message = "Body", ImportantMessage = "Notice", SupportEmailAddress = "vetqas@apha.gov.uk" }
        };
        var service = CreateService(repository);

        var result = await service.GetAsync();

        Assert.Equal("Body", result.Message);
    }

    [Fact]
    public async Task UpdateAsync_ValidInput_PersistsAllThreeFields()
    {
        var repository = new FakeExternalSiteMessageRepository();
        var service = CreateService(repository);

        var result = await service.UpdateAsync("<p>Body</p>", "<p>Notice</p>", "vetqas@apha.gov.uk");

        Assert.Equal("<p>Body</p>", result.Message);
        Assert.NotNull(repository.Updated);
        Assert.Equal("vetqas@apha.gov.uk", repository.Updated!.SupportEmailAddress);
    }

    [Fact]
    public async Task UpdateAsync_ImportantMessageTooLong_ThrowsValidationExceptionAndDoesNotPersist()
    {
        var repository = new FakeExternalSiteMessageRepository();
        var service = CreateService(repository);

        var ex = await Assert.ThrowsAsync<ExternalSiteMessageValidationException>(
            () => service.UpdateAsync("Body", new string('a', 501), "vetqas@apha.gov.uk"));

        Assert.Contains(ex.Errors, e => e.Field == "ImportantMessage");
        Assert.Null(repository.Updated);
    }

    [Fact]
    public async Task UpdateAsync_ClearedImportantMessage_Succeeds()
    {
        var repository = new FakeExternalSiteMessageRepository();
        var service = CreateService(repository);

        var result = await service.UpdateAsync("Body", string.Empty, "vetqas@apha.gov.uk");

        Assert.Equal(string.Empty, result.ImportantMessage);
    }

    [Fact]
    public async Task UpdateAsync_InvalidEmail_ThrowsValidationException()
    {
        var service = CreateService(new FakeExternalSiteMessageRepository());

        var ex = await Assert.ThrowsAsync<ExternalSiteMessageValidationException>(
            () => service.UpdateAsync("Body", "Notice", "not-an-email"));

        Assert.Contains(ex.Errors, e => e.Field == "SupportEmailAddress");
    }
}
