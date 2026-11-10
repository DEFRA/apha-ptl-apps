using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.Api.Controllers;
using PTL.Api.Tests.Audit;
using PTL.Api.Tests.ExternalUser;
using PTL.Api.Tests.Participant;
using PTL.Contracts.LoginInstructions;
using PTL.Contracts.TestConsultant;
using PTL.Contracts.Viewer;
using PTL.Core.LoginInstructions;
using PTL.Core.Viewer;

namespace PTL.Api.Tests.Endpoints;

public class LoginInstructionsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed class ScriptedLoginInstructionsService(LoginInstructionsOutcome outcome) : ILoginInstructionsService
    {
        public List<(LoginInstructionUserType UserType, Guid Id)> Calls { get; } = [];

        public Task<LoginInstructionsResult> SendAsync(LoginInstructionUserType userType, Guid id, CancellationToken cancellationToken = default)
        {
            Calls.Add((userType, id));
            return Task.FromResult(new LoginInstructionsResult(outcome));
        }
    }

    private readonly WebApplicationFactory<Program> _factory;

    public LoginInstructionsControllerTests(WebApplicationFactory<Program> factory)
    {
        // Startup checks read these before the host is built (see HealthEndpointsTests).
        Environment.SetEnvironmentVariable("Database__Host", "localhost");
        Environment.SetEnvironmentVariable("Database__Name", "ProficiencyTesting");
        Environment.SetEnvironmentVariable("Database__IntegratedSecurity", "true");
        Environment.SetEnvironmentVariable("Database__TrustServerCertificate", "true");
        Environment.SetEnvironmentVariable("HealthCheck__ReadinessKey", "local-dev-readiness-key");

        // Both flags off: the hosted schedulers return at once, so no test run ever reaches a real database.
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TabulationEmails:Enabled"] = "false",
                ["PublicationNotifications:Enabled"] = "false",
            })));
    }

    [Fact]
    public async Task SendToParticipant_Sent_ReturnsSuccessForThatParticipant()
    {
        var service = new ScriptedLoginInstructionsService(LoginInstructionsOutcome.Sent);
        var id = Guid.NewGuid();

        var result = await new LoginInstructionsController(service).SendToParticipant(id, CancellationToken.None);

        Assert.True(Assert.IsType<LoginInstructionsResponse>(Assert.IsType<OkObjectResult>(result.Result).Value).Success);
        Assert.Equal((LoginInstructionUserType.Participant, id), Assert.Single(service.Calls));
    }

    [Fact]
    public async Task SendToViewer_Sent_ReturnsSuccessForThatViewer()
    {
        var service = new ScriptedLoginInstructionsService(LoginInstructionsOutcome.Sent);
        var id = Guid.NewGuid();

        var result = await new LoginInstructionsController(service).SendToViewer(id, CancellationToken.None);

        Assert.True(Assert.IsType<LoginInstructionsResponse>(Assert.IsType<OkObjectResult>(result.Result).Value).Success);
        Assert.Equal((LoginInstructionUserType.Viewer, id), Assert.Single(service.Calls));
    }

    [Fact]
    public async Task SendToExternalTestConsultant_Sent_ReturnsSuccessForThatConsultant()
    {
        var service = new ScriptedLoginInstructionsService(LoginInstructionsOutcome.Sent);
        var id = Guid.NewGuid();

        var result = await new LoginInstructionsController(service).SendToExternalTestConsultant(id, CancellationToken.None);

        Assert.True(Assert.IsType<LoginInstructionsResponse>(Assert.IsType<OkObjectResult>(result.Result).Value).Success);
        Assert.Equal((LoginInstructionUserType.ExternalTestConsultant, id), Assert.Single(service.Calls));
    }

    [Fact]
    public async Task Send_UnknownRecord_ReturnsNotFound()
    {
        var result = await new LoginInstructionsController(new ScriptedLoginInstructionsService(LoginInstructionsOutcome.NotFound)).SendToViewer(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Send_NoEmailAddress_ReturnsAFailureWithTheReason()
    {
        var result = await new LoginInstructionsController(new ScriptedLoginInstructionsService(LoginInstructionsOutcome.NoEmailAddress)).SendToViewer(Guid.NewGuid(), CancellationToken.None);

        var response = Assert.IsType<LoginInstructionsResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.False(response.Success);
        Assert.Contains("no email address", response.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Send_NotifyFailure_ReturnsAFailureThatInvitesARetry()
    {
        var result = await new LoginInstructionsController(new ScriptedLoginInstructionsService(LoginInstructionsOutcome.SendFailed)).SendToViewer(Guid.NewGuid(), CancellationToken.None);

        var response = Assert.IsType<LoginInstructionsResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.False(response.Success);
        Assert.Contains("try again", response.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoginInstructionsService_ResolvesFromTheRealContainer()
    {
        using var scope = _factory.Services.CreateScope();

        Assert.IsType<LoginInstructionsService>(scope.ServiceProvider.GetRequiredService<ILoginInstructionsService>());
    }

    [Fact]
    public async Task Post_UnknownViewer_ReturnsNotFoundThroughTheRealRoute()
    {
        var client = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<ILoginInstructionsService>();
            services.AddSingleton<ILoginInstructionsService>(new ScriptedLoginInstructionsService(LoginInstructionsOutcome.NotFound));
        })).CreateClient();

        var response = await client.PostAsync($"/api/login-instructions/viewers/{Guid.NewGuid()}", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_Sent_ReturnsSuccessThroughTheRealRoute()
    {
        var service = new ScriptedLoginInstructionsService(LoginInstructionsOutcome.Sent);
        var client = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<ILoginInstructionsService>();
            services.AddSingleton<ILoginInstructionsService>(service);
        })).CreateClient();
        var id = Guid.NewGuid();

        var response = await client.PostAsync($"/api/login-instructions/external-test-consultants/{id}", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<LoginInstructionsResponse>())!.Success);
        Assert.Equal((LoginInstructionUserType.ExternalTestConsultant, id), Assert.Single(service.Calls));
    }

    [Fact]
    public async Task ViewerList_ExposesTheSentFlag()
    {
        var repository = new FakeViewerRepository();
        repository.Viewers.Add(new ViewerEntity { ViewerId = Guid.NewGuid(), Name = "Sent One", Email = "a@example.com", LoginInstructionsSent = true });
        repository.Viewers.Add(new ViewerEntity { ViewerId = Guid.NewGuid(), Name = "New One", Email = "b@example.com" });

        var result = await new ViewerController(new ViewerService(repository, new FakeActingUserAccessor())).GetAll(CancellationToken.None);

        var viewers = Assert.IsAssignableFrom<IReadOnlyList<ViewerResponse>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.True(viewers.Single(v => v.Name == "Sent One").LoginInstructionsSent);
        Assert.False(viewers.Single(v => v.Name == "New One").LoginInstructionsSent);
    }

    [Fact]
    public async Task ExternalTestConsultantList_ExposesTheSentFlag()
    {
        var repository = new FakeTestConsultantRepository();
        repository.Seed(new PTL.Core.TestConsultant.TestConsultant { ExternalTestConsultantId = Guid.NewGuid(), Name = "Sent One", Email = "a@example.com", LoginInstructionsSent = true });
        repository.Seed(new PTL.Core.TestConsultant.TestConsultant { ExternalTestConsultantId = Guid.NewGuid(), Name = "New One", Email = "b@example.com" });

        var result = await new ExternalTestConsultantController(new PTL.Core.TestConsultant.TestConsultantService(repository, new FakeActingUserAccessor())).GetAll(CancellationToken.None);

        var consultants = Assert.IsAssignableFrom<IReadOnlyList<ExternalTestConsultantResponse>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.True(consultants.Single(c => c.Name == "Sent One").LoginInstructionsSent);
        Assert.False(consultants.Single(c => c.Name == "New One").LoginInstructionsSent);
    }
}
