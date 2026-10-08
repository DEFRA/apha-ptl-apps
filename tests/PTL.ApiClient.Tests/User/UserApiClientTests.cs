using System.Net;
using PTL.ApiClient;
using PTL.ApiClient.Tests;
using PTL.Contracts.User;

namespace PTL.ApiClient.Tests.User;

public class UserApiClientTests
{
    private static UserApiClient CreateClient(HttpStatusCode statusCode, string? jsonContent) =>
        new(new HttpClient(new StubHttpMessageHandler(statusCode, jsonContent)) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task SearchStaffDirectoryAsync_Success_ReturnsDeserializedResults()
    {
        const string json = """[{"username":"m100001","email":"jane@apha.gov.uk","friendlyName":"Jane Smith","firstName":"Jane","lastName":"Smith"}]""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.SearchStaffDirectoryAsync("jane");

        Assert.Equal("m100001", Assert.Single(result).Username);
    }

    [Fact]
    public async Task SearchStaffDirectoryAsync_NullResponse_ReturnsEmpty()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.SearchStaffDirectoryAsync("jane");

        Assert.Empty(result);
    }

    [Fact]
    public async Task CreateUserAsync_Success_ReturnsSuccessfulResult()
    {
        const string json = """{"userId":"11111111-1111-1111-1111-111111111111","username":"m100001","friendlyName":"Jane Smith","firstName":"Jane","lastName":"Smith","email":"jane@apha.gov.uk","department":"Science"}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.CreateUserAsync(new CreateUserRequest("m100001", "jane@apha.gov.uk", "Jane Smith", "Jane", "Smith", "Science"));

        Assert.True(result.Success);
        Assert.Equal("m100001", result.User?.Username);
    }

    [Fact]
    public async Task CreateUserAsync_BadRequest_ReturnsFieldErrors()
    {
        const string json = """{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"errors":{"Email":["The selected User has no Email Address stored in Active Directory and cannot be added."]}}""";
        var client = CreateClient(HttpStatusCode.BadRequest, json);

        var result = await client.CreateUserAsync(new CreateUserRequest("m100001", string.Empty, "Jane Smith", "Jane", "Smith", "Science"));

        Assert.False(result.Success);
        Assert.Contains("Email", result.FieldErrors.Keys);
    }

    [Fact]
    public async Task GetUsersAsync_NullResponse_ReturnsEmpty()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.GetUsersAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetUserRoleGridAsync_Success_ReturnsDeserializedRows()
    {
        const string json = """[{"userId":"11111111-1111-1111-1111-111111111111","username":"m100001","friendlyName":"Jane Smith","roleIds":["22222222-2222-2222-2222-222222222222"]}]""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetUserRoleGridAsync();

        Assert.Single(Assert.Single(result).RoleIds);
    }

    [Fact]
    public async Task SetUserRolesAsync_Blocked_ReturnsFailureMessage()
    {
        const string json = """{"success":false,"message":"You cannot remove your own Admin access."}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.SetUserRolesAsync(Guid.NewGuid(), new SetUserRolesRequest([]));

        Assert.False(result.Success);
        Assert.Equal("You cannot remove your own Admin access.", result.Message);
    }
}
