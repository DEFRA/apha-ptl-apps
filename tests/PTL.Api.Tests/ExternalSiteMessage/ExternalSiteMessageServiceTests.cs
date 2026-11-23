using PTL.Api.Tests.Audit;
using PTL.Core.Audit;
using PTL.Core.ExternalSiteMessage;

namespace PTL.Api.Tests.ExternalSiteMessage;

public class ExternalSiteMessageServiceTests
{
    private static readonly FakeActingUserAccessor ActingUser = new() { ActingUserId = Guid.NewGuid() };

    private static ExternalSiteMessageService CreateService(FakeExternalSiteMessageRepository repository) => new(repository, ActingUser);

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
        var audit = Assert.IsType<AuditEntry>(repository.LastAuditEntry);
        Assert.Equal("UpdateExternalSiteMessage", audit.Action);
        Assert.Equal("ExternalSiteMessage", audit.EntityType);
        Assert.Equal(ActingUser.ActingUserId, audit.ActingUserId);
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
        Assert.Null(repository.LastAuditEntry);
    }

    [Fact]
    public async Task UpdateAsync_EntityEscapedEditorOutput_IsSavedAsRealHtml()
    {
        var repository = new FakeExternalSiteMessageRepository();
        var service = CreateService(repository);

        var result = await service.UpdateAsync(
            "&lt;p&gt;Body&amp;nbsp;text&lt;/p&gt;", "&lt;p&gt;Test &lt;strong&gt;notice&lt;/strong&gt;&lt;/p&gt;", "vetqas@apha.gov.uk");

        Assert.Equal("<p>Body&nbsp;text</p>", repository.Updated!.Message);
        Assert.Equal("<p>Test <strong>notice</strong></p>", result.ImportantMessage);
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
    public async Task UpdateAsync_PastedEditorChrome_IsStrippedAndTextKept()
    {
        var repository = new FakeExternalSiteMessageRepository();
        var service = CreateService(repository);
        const string chrome =
            "<div class=\"tox\" role=\"application\" aria-disabled=\"false\"><div class=\"tox-toolbar__group\" role=\"toolbar\" title=\"\">"
            + "<button aria-label=\"Bold\"><svg><path d=\"M1\"/></svg></button></div></div>";

        await service.UpdateAsync("<p>Further information</p>" + chrome, chrome + "<p>Notice</p>", "vetqas@apha.gov.uk");

        Assert.Equal("<p>Further information</p>", repository.Updated!.Message);
        Assert.Equal("<p>Notice</p>", repository.Updated.ImportantMessage);
    }

    [Fact]
    public async Task UpdateAsync_ToolbarFormatting_IsKept()
    {
        var repository = new FakeExternalSiteMessageRepository();
        var service = CreateService(repository);
        const string html = "<p><strong>Bold</strong> <em>italic</em> <span style=\"text-decoration: underline;\">underline</span> m<sup>2</sup></p>";

        await service.UpdateAsync(html, html, "vetqas@apha.gov.uk");

        Assert.Equal(html, repository.Updated!.Message);
        Assert.Equal(html, repository.Updated.ImportantMessage);
    }

    [Fact]
    public async Task UpdateAsync_Link_IsUnwrappedToItsText()
    {
        var repository = new FakeExternalSiteMessageRepository();
        var service = CreateService(repository);

        await service.UpdateAsync("<p>Email <a href=\"mailto:vetqas@apha.gov.uk\">vetqas@apha.gov.uk</a></p>", "Notice", "vetqas@apha.gov.uk");

        Assert.Equal("<p>Email vetqas@apha.gov.uk</p>", repository.Updated!.Message);
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
