using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.Api.Tests.ExternalUser;
using PTL.Api.Tests.InternalUser;
using PTL.Api.Tests.TestSupport;
using PTL.Contracts.ExternalUser;
using PTL.Contracts.LoadTesting;
using PTL.Core.ExternalUser;
using PTL.Core.InternalUser;
using CoreInternalUser = PTL.Core.InternalUser.InternalUser;

namespace PTL.Api.Tests.LoadTesting;

// The switch header is model-bound ([FromHeader]); these prove the binding through the real pipeline, not just the action.
public class LoadTestingRouteTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid Linked = Guid.Parse("321cbd77-fec2-f111-aaad-7c1e52845f5a");

    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeInternalUserRepository _internalUsers = new();
    private readonly FakeExternalUserRepository _externalUsers = new();

    public LoadTestingRouteTests(WebApplicationFactory<Program> factory)
    {
        // Startup checks read these before the host is built (see HealthEndpointsTests).
        Environment.SetEnvironmentVariable("Database__Host", "localhost");
        Environment.SetEnvironmentVariable("Database__Name", "ProficiencyTesting");
        Environment.SetEnvironmentVariable("Database__IntegratedSecurity", "true");
        Environment.SetEnvironmentVariable("Database__TrustServerCertificate", "true");
        Environment.SetEnvironmentVariable("HealthCheck__ReadinessKey", "local-dev-readiness-key");

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TabulationEmails:Enabled"] = "false",
                ["PublicationNotifications:Enabled"] = "false",
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IInternalUserRepository>();
                services.AddSingleton<IInternalUserRepository>(_internalUsers);
                services.RemoveAll<IExternalUserRepository>();
                services.AddSingleton<IExternalUserRepository>(_externalUsers);
            });
        });
    }

    private async Task<HttpResponseMessage> PostAsync(string path, object body, string? header)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        if (header is not null)
        {
            request.Headers.Add(LoadTestingHeaders.Enabled, header);
        }

        return await _factory.CreateAuthenticatedClient().SendAsync(request);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    public async Task InternalLookup_WithoutTheHeader_IsNotFound(string? header)
    {
        var response = await PostAsync("/api/load-testing/internal-users/lookup", new LoadTestingInternalLookupRequest(@"DEFRA\tester"), header);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task InternalLookup_WithTheHeader_ReturnsTheStoredObjectId()
    {
        _internalUsers.Seed(new CoreInternalUser { UserId = Guid.NewGuid(), Username = @"DEFRA\tester", SsoIdInt = Linked });

        var response = await PostAsync("/api/load-testing/internal-users/lookup", new LoadTestingInternalLookupRequest(@"DEFRA\tester"), "true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoadTestingInternalLookupResponse>();
        Assert.Equal(new LoadTestingInternalLookupResponse(LoadTestingInternalLookupStatus.Found, Linked), body);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    public async Task ExternalLookup_WithoutTheHeader_IsNotFound(string? header)
    {
        var response = await PostAsync("/api/load-testing/external-users/lookup", new LoadTestingLookupRequest("tester@example.test"), header);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, _externalUsers.FindByEmailCalls);
    }

    [Fact]
    public async Task ExternalLookup_WithTheHeader_ReturnsTheStoredContactId()
    {
        _externalUsers.Add(ExternalUserType.Participant, "tester@example.test", Linked, labCode: "4097");

        var response = await PostAsync("/api/load-testing/external-users/lookup", new LoadTestingLookupRequest("tester@example.test"), "true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoadTestingLookupResponse>();
        Assert.Equal(new LoadTestingLookupResponse(LoadTestingLookupStatus.Found, Linked), body);
    }
}
