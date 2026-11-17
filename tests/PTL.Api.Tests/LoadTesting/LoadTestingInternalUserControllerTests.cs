using Microsoft.AspNetCore.Mvc;
using PTL.Api.LoadTesting;
using PTL.Api.Tests.InternalUser;
using PTL.Contracts.LoadTesting;
using CoreInternalUser = PTL.Core.InternalUser.InternalUser;

namespace PTL.Api.Tests.LoadTesting;

public class LoadTestingInternalUserControllerTests
{
    private const string Username = @"DEFRA\tester";
    private static readonly Guid Linked = Guid.Parse("321cbd77-fec2-f111-aaad-7c1e52845f5a");

    private readonly FakeInternalUserRepository _users = new();

    private LoadTestingInternalUserController CreateController() => new(_users);

    private async Task<LoadTestingInternalLookupResponse> LookupAsync(string username = Username)
    {
        var result = await CreateController().Lookup("true", new LoadTestingInternalLookupRequest(username), CancellationToken.None);
        return Assert.IsType<LoadTestingInternalLookupResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    public async Task Lookup_WithoutTheLoadTestingHeader_IsNotFound(string? headerValue)
    {
        var result = await CreateController().Lookup(headerValue, new LoadTestingInternalLookupRequest(Username), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Theory]
    [InlineData("  ")]
    [InlineData(null)]
    public async Task Lookup_BlankOrMissingUsername_IsBadRequestNamingTheField(string? username)
    {
        var result = await CreateController().Lookup("true", new LoadTestingInternalLookupRequest(username!), CancellationToken.None);

        var problem = Assert.IsType<ValidationProblemDetails>(Assert.IsType<BadRequestObjectResult>(result.Result).Value);
        Assert.Equal(["The username is required."], problem.Errors[nameof(LoadTestingInternalLookupRequest.Username)]);
    }

    [Fact]
    public async Task Lookup_UnknownUsername_ReportsNotFound()
    {
        var body = await LookupAsync();

        Assert.Equal(LoadTestingInternalLookupStatus.NotFound, body.Status);
        Assert.Null(body.SsoIdInt);
    }

    [Fact]
    public async Task Lookup_LinkedUser_ReturnsTheStoredObjectIdWithoutWritingAnything()
    {
        _users.Seed(new CoreInternalUser { UserId = Guid.NewGuid(), Username = Username, SsoIdInt = Linked });

        var body = await LookupAsync();

        Assert.Equal(LoadTestingInternalLookupStatus.Found, body.Status);
        Assert.Equal(Linked, body.SsoIdInt);
        Assert.Empty(_users.UpdateCalls);
    }

    [Fact]
    public async Task Lookup_UnlinkedUser_ReportsNotLinkedAndNeverLinks()
    {
        _users.Seed(new CoreInternalUser { UserId = Guid.NewGuid(), Username = Username, SsoIdInt = null });

        var body = await LookupAsync();

        Assert.Equal(LoadTestingInternalLookupStatus.NotLinked, body.Status);
        Assert.Null(body.SsoIdInt);
        Assert.Empty(_users.UpdateCalls);
    }
}
