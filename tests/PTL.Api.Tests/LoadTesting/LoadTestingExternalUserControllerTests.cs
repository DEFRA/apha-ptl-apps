using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PTL.Api.LoadTesting;
using PTL.Api.Tests.ExternalUser;
using PTL.Contracts.ExternalUser;
using PTL.Contracts.LoadTesting;

namespace PTL.Api.Tests.LoadTesting;

public class LoadTestingExternalUserControllerTests
{
    private const string Email = "tester@example.test";
    private static readonly Guid Linked = Guid.Parse("321cbd77-fec2-f111-aaad-7c1e52845f5a");

    private readonly FakeExternalUserRepository _users = new();

    private LoadTestingExternalUserController CreateController(string? headerValue = "true")
    {
        var httpContext = new DefaultHttpContext();
        if (headerValue is not null)
        {
            httpContext.Request.Headers[LoadTestingHeaders.Enabled] = headerValue;
        }

        return new LoadTestingExternalUserController(_users)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    private async Task<LoadTestingLookupResponse> LookupAsync(string email = Email)
    {
        var result = await CreateController().Lookup(new LoadTestingLookupRequest(email), CancellationToken.None);
        return Assert.IsType<LoadTestingLookupResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    public async Task Lookup_WithoutTheLoadTestingHeader_IsNotFound(string? headerValue)
    {
        var result = await CreateController(headerValue).Lookup(new LoadTestingLookupRequest(Email), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
        Assert.Equal(0, _users.FindByEmailCalls);
    }

    [Fact]
    public async Task Lookup_BlankEmail_IsBadRequest()
    {
        var result = await CreateController().Lookup(new LoadTestingLookupRequest("  "), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Lookup_NoUserWithTheEmail_ReportsNotFound()
    {
        var body = await LookupAsync();

        Assert.Equal(LoadTestingLookupStatus.NotFound, body.Status);
        Assert.Null(body.SsoIdExt);
    }

    [Fact]
    public async Task Lookup_LinkedUser_ReturnsTheStoredContactIdWithoutWritingAnything()
    {
        _users.Add(ExternalUserType.Participant, Email, Linked, labCode: "4097");
        _users.Add(ExternalUserType.Participant, Email, Linked, labCode: "4098");

        var body = await LookupAsync();

        Assert.Equal(LoadTestingLookupStatus.Found, body.Status);
        Assert.Equal(Linked, body.SsoIdExt);
        Assert.Equal(0, _users.LinkCalls);
    }

    [Fact]
    public async Task Lookup_UnlinkedUser_ReportsNotLinkedAndNeverLinks()
    {
        _users.Add(ExternalUserType.Viewer, Email, ssoIdExt: null);

        var body = await LookupAsync();

        Assert.Equal(LoadTestingLookupStatus.NotLinked, body.Status);
        Assert.Null(body.SsoIdExt);
        Assert.Equal(0, _users.LinkCalls);
    }

    [Fact]
    public async Task Lookup_OnlyTheLowestUserTypeCounts()
    {
        _users.Add(ExternalUserType.Participant, Email, Linked, labCode: "4097");
        _users.Add(ExternalUserType.Viewer, Email, Guid.NewGuid());

        var body = await LookupAsync();

        Assert.Equal(Linked, body.SsoIdExt);
    }

    [Fact]
    public async Task Lookup_DifferentContactIdsOnTheLowestType_ReportsAmbiguous()
    {
        _users.Add(ExternalUserType.Participant, Email, Linked, labCode: "4097");
        _users.Add(ExternalUserType.Participant, Email, Guid.NewGuid(), labCode: "4098");

        var body = await LookupAsync();

        Assert.Equal(LoadTestingLookupStatus.Ambiguous, body.Status);
        Assert.Null(body.SsoIdExt);
    }
}
