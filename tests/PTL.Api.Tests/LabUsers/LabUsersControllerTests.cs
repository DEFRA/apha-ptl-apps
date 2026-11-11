using Microsoft.AspNetCore.Mvc;
using PTL.Api.Controllers;
using PTL.Api.Tests.Participant;
using PTL.Contracts.LabUsers;
using PTL.Contracts.LoginInstructions;
using PTL.Core.LabUsers;
using PTL.Core.LoginInstructions;
using CoreParticipant = PTL.Core.Participant.Participant;

namespace PTL.Api.Tests.LabUsers;

public class LabUsersControllerTests
{
    private sealed class FixedLoginInstructionsService(LoginInstructionsOutcome outcome) : ILoginInstructionsService
    {
        public Task<LoginInstructionsResult> SendAsync(LoginInstructionUserType userType, Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(new LoginInstructionsResult(outcome));
    }

    private readonly FakeLabUserRepository _labUsers = new();
    private readonly FakeParticipantRepository _participants = new();

    private LabUsersController CreateController(LoginInstructionsOutcome outcome = LoginInstructionsOutcome.Sent) =>
        new(new LabUserService(_labUsers, _participants, new FixedLoginInstructionsService(outcome)));

    private async Task<Guid> AddLabAsync() =>
        (await _participants.CreateAsync(new CoreParticipant { LabCode = "0142", LabName = "Bristol", IsActive = true })).ParticipantId;

    private LabUserLink AddLink(Guid participantId, bool isActive = true)
    {
        var link = new LabUserLink
        {
            LabUserParticipantId = Guid.NewGuid(),
            LabUserId = Guid.NewGuid(),
            ParticipantId = participantId,
            Name = "Emma Wright",
            Email = "emma@example.org",
            IsActive = isActive,
            LabCode = "0142",
            LabName = "Bristol"
        };
        _labUsers.Links.Add(link);
        return link;
    }

    [Fact]
    public async Task List_KnownLab_ReturnsOkWithTheLabAndItsPeople()
    {
        var labId = await AddLabAsync();
        AddLink(labId);

        var response = await CreateController().List(labId, CancellationToken.None);

        var body = Assert.IsType<LabUserListResponse>(Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.Equal("0142", body.LabCode);
        Assert.Single(body.Users);
    }

    [Fact]
    public async Task List_UnknownLab_IsNotFound()
    {
        var response = await CreateController().List(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(response.Result);
    }

    [Fact]
    public async Task Get_PersonOfTheLab_ReturnsOk_AndAPersonOfAnotherLabIsNotFound()
    {
        var labId = await AddLabAsync();
        var mine = AddLink(labId);
        var theirs = AddLink(Guid.NewGuid());
        var controller = CreateController();

        var found = await controller.Get(labId, mine.LabUserId, CancellationToken.None);
        var missing = await controller.Get(labId, theirs.LabUserId, CancellationToken.None);

        Assert.IsType<LabUserDetailsResponse>(Assert.IsType<OkObjectResult>(found.Result).Value);
        Assert.IsType<NotFoundResult>(missing.Result);
    }

    [Fact]
    public async Task Add_ValidRequest_ReturnsOkWithTheOutcome()
    {
        var labId = await AddLabAsync();

        var response = await CreateController().Add(labId, new LabUserSaveRequest("Ramesh", "ramesh@lab.com", true), CancellationToken.None);

        var body = Assert.IsType<LabUserSaveResult>(Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.Equal(LabUserSaveOutcome.Added, body.Outcome);
    }

    [Fact]
    public async Task Add_InvalidRequest_ReturnsOkWithTheFieldErrors()
    {
        var labId = await AddLabAsync();

        var response = await CreateController().Add(labId, new LabUserSaveRequest("", "ramesh@lab.com", true), CancellationToken.None);

        var body = Assert.IsType<LabUserSaveResult>(Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.False(body.Success);
        Assert.Contains("Name", body.FieldErrors.Keys);
    }

    [Fact]
    public async Task Add_UnknownLab_IsNotFound()
    {
        var response = await CreateController().Add(Guid.NewGuid(), new LabUserSaveRequest("Ramesh", "ramesh@lab.com", true), CancellationToken.None);

        Assert.IsType<NotFoundResult>(response.Result);
    }

    [Fact]
    public async Task Update_PersonOfTheLab_ReturnsOk_AndAPersonOfAnotherLabIsNotFound()
    {
        var labId = await AddLabAsync();
        var mine = AddLink(labId);
        var theirs = AddLink(Guid.NewGuid());
        var controller = CreateController();

        var updated = await controller.Update(labId, mine.LabUserId, new LabUserSaveRequest("Emma Wright", "emma@example.org", false), CancellationToken.None);
        var missing = await controller.Update(labId, theirs.LabUserId, new LabUserSaveRequest("x", "x@example.org", false), CancellationToken.None);

        Assert.Equal(LabUserSaveOutcome.Updated, Assert.IsType<LabUserSaveResult>(Assert.IsType<OkObjectResult>(updated.Result).Value).Outcome);
        Assert.IsType<NotFoundResult>(missing.Result);
    }

    [Theory]
    [InlineData(LoginInstructionsOutcome.Sent, true)]
    [InlineData(LoginInstructionsOutcome.SendFailed, false)]
    [InlineData(LoginInstructionsOutcome.NoEmailAddress, false)]
    public async Task SendLoginInstructions_ActivePerson_ReturnsOkWithTheOutcome(LoginInstructionsOutcome outcome, bool success)
    {
        var labId = await AddLabAsync();
        var link = AddLink(labId);

        var response = await CreateController(outcome).SendLoginInstructions(labId, link.LabUserId, CancellationToken.None);

        var body = Assert.IsType<LoginInstructionsResponse>(Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.Equal(success, body.Success);
    }

    [Fact]
    public async Task SendLoginInstructions_InactivePerson_ReturnsOkWithTheReason()
    {
        var labId = await AddLabAsync();
        var link = AddLink(labId, isActive: false);

        var response = await CreateController().SendLoginInstructions(labId, link.LabUserId, CancellationToken.None);

        var body = Assert.IsType<LoginInstructionsResponse>(Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.False(body.Success);
        Assert.Contains("active lab user", body.Message);
    }

    [Fact]
    public async Task SendLoginInstructions_PersonOfAnotherLab_IsNotFound()
    {
        var labId = await AddLabAsync();
        var theirs = AddLink(Guid.NewGuid());

        var response = await CreateController().SendLoginInstructions(labId, theirs.LabUserId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(response.Result);
    }

    // The service never answers "not found" with a value, but the controller must not turn one into a success if it ever does.
    [Fact]
    public async Task SendLoginInstructions_NotFoundOutcome_IsNotFound()
    {
        var labId = await AddLabAsync();
        var link = AddLink(labId);

        var response = await CreateController(LoginInstructionsOutcome.NotFound).SendLoginInstructions(labId, link.LabUserId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(response.Result);
    }
}
