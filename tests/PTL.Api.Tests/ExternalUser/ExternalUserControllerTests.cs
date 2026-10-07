using Microsoft.AspNetCore.Mvc;
using PTL.Api.Controllers;
using PTL.Api.Tests.Participant;
using PTL.Contracts.ExternalUser;
using PTL.Core.ExternalUser;
using PTL.Core.Viewer;
using CoreParticipant = PTL.Core.Participant.Participant;

namespace PTL.Api.Tests.ExternalUser;

public class ExternalUserControllerTests
{
    private static ExternalUserController CreateController(
        FakeParticipantRepository? participantRepository = null,
        FakeViewerRepository? viewerRepository = null,
        FakeTestConsultantRepository? testConsultantRepository = null) =>
        new(new ExternalUserService(
            participantRepository ?? new FakeParticipantRepository(),
            viewerRepository ?? new FakeViewerRepository(),
            testConsultantRepository ?? new FakeTestConsultantRepository()));

    [Fact]
    public async Task ResolveExternalUser_NoMatchingRoles_ReturnsOkWithEmptyRoles()
    {
        var controller = CreateController();

        var response = await controller.ResolveExternalUser(
            new ResolveExternalUserRequest(Guid.NewGuid(), "user@example.com", "Jane Doe", ["Participant"]),
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var body = Assert.IsType<ResolveExternalUserResponse>(okResult.Value);
        Assert.Empty(body.Roles);
        Assert.Equal("Jane Doe", body.DisplayName);
    }

    [Fact]
    public async Task ResolveExternalUser_MatchingViewer_ReturnsOkWithResolvedRole()
    {
        var ssoIdExt = Guid.NewGuid();
        var viewerRepository = new FakeViewerRepository();
        var viewer = await viewerRepository.CreateAsync(new ViewerEntity { ViewerId = Guid.NewGuid(), SsoIdExt = ssoIdExt, Name = "Viewer One" });
        var controller = CreateController(viewerRepository: viewerRepository);

        var response = await controller.ResolveExternalUser(
            new ResolveExternalUserRequest(ssoIdExt, "viewer@example.com", "Jane Doe", ["Viewer"]),
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var body = Assert.IsType<ResolveExternalUserResponse>(okResult.Value);
        Assert.Equal(["Viewer"], body.Roles);
        Assert.Equal(viewer.ViewerId, body.ViewerId);
    }

    [Fact]
    public async Task ResolveExternalUser_MatchingParticipant_ReturnsOkWithParticipantId()
    {
        var ssoIdExt = Guid.NewGuid();
        var participantRepository = new FakeParticipantRepository();
        var participant = await participantRepository.CreateAsync(new CoreParticipant { SsoIdExt = ssoIdExt, CustomerId = Guid.NewGuid(), LabCode = "LAB001" });
        var controller = CreateController(participantRepository: participantRepository);

        var response = await controller.ResolveExternalUser(
            new ResolveExternalUserRequest(ssoIdExt, "participant@example.com", "Jane Doe", ["Participant"]),
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var body = Assert.IsType<ResolveExternalUserResponse>(okResult.Value);
        Assert.Equal(["Participant"], body.Roles);
        Assert.Equal(participant.ParticipantId, body.ParticipantId);
    }
}
