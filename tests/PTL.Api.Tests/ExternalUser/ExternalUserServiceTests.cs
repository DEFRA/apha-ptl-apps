using PTL.Api.Tests.Participant;
using PTL.Core.ExternalUser;
using PTL.Core.Viewer;
using CoreParticipant = PTL.Core.Participant.Participant;
using CoreTestConsultant = PTL.Core.TestConsultant.TestConsultant;

namespace PTL.Api.Tests.ExternalUser;

public class ExternalUserServiceTests
{
    private static ExternalUserService CreateService(
        FakeParticipantRepository? participantRepository = null,
        FakeViewerRepository? viewerRepository = null,
        FakeTestConsultantRepository? testConsultantRepository = null) =>
        new(
            participantRepository ?? new FakeParticipantRepository(),
            viewerRepository ?? new FakeViewerRepository(),
            testConsultantRepository ?? new FakeTestConsultantRepository());

    [Fact]
    public async Task ResolveAsync_NoRecognisedRoles_ReturnsEmptyResult()
    {
        var service = CreateService();

        var result = await service.ResolveAsync(Guid.NewGuid(), "user@example.com", "Jane Doe", ["Some Other Role"]);

        Assert.Empty(result.Roles);
        Assert.False(result.IsResolved);
        Assert.Null(result.ParticipantId);
        Assert.Null(result.ViewerId);
        Assert.Null(result.TestConsultantId);
        Assert.Equal("Jane Doe", result.DisplayName);
    }

    [Fact]
    public async Task ResolveAsync_ParticipantRole_MatchesBySsoIdExt()
    {
        var ssoIdExt = Guid.NewGuid();
        var participant = new CoreParticipant { ParticipantId = Guid.NewGuid(), SsoIdExt = ssoIdExt, CustomerId = Guid.NewGuid(), LabCode = "LAB001" };
        var participantRepository = new FakeParticipantRepository();
        await participantRepository.CreateAsync(participant);
        var service = CreateService(participantRepository: participantRepository);

        var result = await service.ResolveAsync(ssoIdExt, "participant@example.com", "Jane Doe", ["Participant"]);

        Assert.Equal(["Participant"], result.Roles);
        Assert.Equal(participant.ParticipantId, result.ParticipantId);
    }

    [Fact]
    public async Task ResolveAsync_ParticipantRole_NoSsoIdExtMatch_FallsBackToEmailAndBackfillsSsoIdExt()
    {
        var participantId = Guid.NewGuid();
        var email = "participant@example.com";
        var participant = new CoreParticipant { ParticipantId = participantId, SsoId = Guid.NewGuid(), Email = email, CustomerId = Guid.NewGuid(), LabCode = "LAB001" };
        var participantRepository = new FakeParticipantRepository();
        await participantRepository.CreateAsync(participant);
        var service = CreateService(participantRepository: participantRepository);
        var ssoIdExt = Guid.NewGuid();

        var result = await service.ResolveAsync(ssoIdExt, email, "Jane Doe", ["Participant"]);

        Assert.Equal(["Participant"], result.Roles);
        Assert.Equal(participantId, result.ParticipantId);
        var backfilled = await participantRepository.GetByIdAsync(participantId);
        Assert.Equal(ssoIdExt, backfilled!.SsoIdExt);
    }

    [Fact]
    public async Task ResolveAsync_ParticipantRole_NoMatchAtAll_OmitsRoleAndDoesNotCreate()
    {
        var participantRepository = new FakeParticipantRepository();
        var service = CreateService(participantRepository: participantRepository);

        var result = await service.ResolveAsync(Guid.NewGuid(), "unknown@example.com", "Jane Doe", ["Participant"]);

        Assert.Empty(result.Roles);
        Assert.Null(result.ParticipantId);
    }

    [Fact]
    public async Task ResolveAsync_ViewerRole_MatchesBySsoIdExt()
    {
        var ssoIdExt = Guid.NewGuid();
        var viewerRepository = new FakeViewerRepository();
        var viewer = await viewerRepository.CreateAsync(new ViewerEntity { ViewerId = Guid.NewGuid(), SsoIdExt = ssoIdExt, Name = "Viewer One" });
        var service = CreateService(viewerRepository: viewerRepository);

        var result = await service.ResolveAsync(ssoIdExt, "viewer@example.com", "Jane Doe", ["Viewer"]);

        Assert.Equal(["Viewer"], result.Roles);
        Assert.Equal(viewer.ViewerId, result.ViewerId);
    }

    [Fact]
    public async Task ResolveAsync_ViewerRole_NoMatch_OmitsRole()
    {
        var service = CreateService();

        var result = await service.ResolveAsync(Guid.NewGuid(), "unknown@example.com", "Jane Doe", ["Viewer"]);

        Assert.Empty(result.Roles);
        Assert.Null(result.ViewerId);
    }

    [Fact]
    public async Task ResolveAsync_TestConsultantRole_MatchesByEmailAndBackfillsSsoIdExt()
    {
        var testConsultantId = Guid.NewGuid();
        const string email = "consultant@example.com";
        var testConsultantRepository = new FakeTestConsultantRepository();
        testConsultantRepository.Seed(new CoreTestConsultant { ExternalTestConsultantId = testConsultantId, Email = email, Name = "Test Consultant One" });
        var service = CreateService(testConsultantRepository: testConsultantRepository);
        var ssoIdExt = Guid.NewGuid();

        var result = await service.ResolveAsync(ssoIdExt, email, "Jane Doe", ["Test Consultant"]);

        Assert.Equal(["Test Consultant"], result.Roles);
        Assert.Equal(testConsultantId, result.TestConsultantId);
        Assert.Contains(testConsultantRepository.UpdateCalls, call => call.ExternalTestConsultantId == testConsultantId && call.SsoIdExt == ssoIdExt);
    }

    [Fact]
    public async Task ResolveAsync_TestConsultantRole_NoMatch_OmitsRole()
    {
        var service = CreateService();

        var result = await service.ResolveAsync(Guid.NewGuid(), "unknown@example.com", "Jane Doe", ["Test Consultant"]);

        Assert.Empty(result.Roles);
        Assert.Null(result.TestConsultantId);
    }

    [Fact]
    public async Task ResolveAsync_MultipleRolesWithMixedOutcomes_ReturnsOnlyResolvedRoles()
    {
        var ssoIdExt = Guid.NewGuid();
        var viewerRepository = new FakeViewerRepository();
        var viewer = await viewerRepository.CreateAsync(new ViewerEntity { ViewerId = Guid.NewGuid(), SsoIdExt = ssoIdExt, Name = "Viewer One" });
        var participantRepository = new FakeParticipantRepository();
        var service = CreateService(participantRepository, viewerRepository);

        var result = await service.ResolveAsync(ssoIdExt, "unknown@example.com", "Jane Doe", ["Viewer", "Participant", "Test Consultant"]);

        Assert.Equal(["Viewer"], result.Roles);
        Assert.Equal(viewer.ViewerId, result.ViewerId);
        Assert.Null(result.ParticipantId);
        Assert.Null(result.TestConsultantId);
    }

    [Fact]
    public async Task ResolveAsync_RoleNameCasingDiffersFromCidm_StillMatches()
    {
        var ssoIdExt = Guid.NewGuid();
        var viewerRepository = new FakeViewerRepository();
        var viewer = await viewerRepository.CreateAsync(new ViewerEntity { ViewerId = Guid.NewGuid(), SsoIdExt = ssoIdExt, Name = "Viewer One" });
        var service = CreateService(viewerRepository: viewerRepository);

        var result = await service.ResolveAsync(ssoIdExt, "viewer@example.com", "Jane Doe", ["viewer"]);

        Assert.Equal(["Viewer"], result.Roles);
        Assert.Equal(viewer.ViewerId, result.ViewerId);
    }
}
