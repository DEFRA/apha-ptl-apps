using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Api.Controllers;
using PTL.Api.Tests.Customer;
using PTL.Api.Tests.Participant;
using PTL.Contracts.ExternalUser;
using PTL.Core.ExternalUser;
using CoreCustomer = PTL.Core.Customer.Customer;
using CoreParticipant = PTL.Core.Participant.Participant;

namespace PTL.Api.Tests.ExternalUser;

public class ExternalUserControllerTests
{
    private readonly FakeExternalUserRepository _users = new();
    private readonly FakeParticipantRepository _participants = new();
    private readonly FakeCustomerRepository _customers = new();

    private ExternalUserController CreateController() =>
        new(new ExternalUserService(_users, _participants, _customers, NullLogger<ExternalUserService>.Instance));

    private static async Task<ResolveExternalUserResponse> OkBodyAsync(Task<ActionResult<ResolveExternalUserResponse>> call)
    {
        var response = await call;
        var ok = Assert.IsType<OkObjectResult>(response.Result);
        return Assert.IsType<ResolveExternalUserResponse>(ok.Value);
    }

    private static ValidationProblemDetails BadRequestBody(ActionResult<ResolveExternalUserResponse> response)
    {
        var badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
        return Assert.IsType<ValidationProblemDetails>(badRequest.Value);
    }

    [Fact]
    public async Task ResolveExternalUser_NoMatchingRecord_ReturnsOkWithNotFoundStatusAndNoAccess()
    {
        var body = await OkBodyAsync(CreateController().ResolveExternalUser(
            new ResolveExternalUserRequest(Guid.NewGuid(), "user@example.com", "Jane Doe"), CancellationToken.None));

        Assert.Equal(ExternalUserResolutionStatus.NotFound, body.Status);
        Assert.Empty(body.Roles);
        Assert.Empty(body.Labs);
        Assert.Null(body.UserType);
        Assert.Equal("Jane Doe", body.DisplayName);
    }

    [Fact]
    public async Task ResolveExternalUser_MatchingViewer_ReturnsOkWithViewerRoleAndId()
    {
        var ssoIdExt = Guid.NewGuid();
        var viewer = _users.Add(ExternalUserType.Viewer, ssoIdExt: ssoIdExt);

        var body = await OkBodyAsync(CreateController().ResolveExternalUser(
            new ResolveExternalUserRequest(ssoIdExt, "viewer@example.com", "Jane Doe"), CancellationToken.None));

        Assert.Equal(ExternalUserResolutionStatus.Resolved, body.Status);
        Assert.Equal(ExternalUserType.Viewer, body.UserType);
        Assert.Equal([ExternalRoleNames.Viewer], body.Roles);
        Assert.Equal(viewer.UserId, body.ViewerId);
    }

    [Fact]
    public async Task ResolveExternalUser_MatchingParticipantWithOneLab_ReturnsOkWithTheLabAndOnlineOrdering()
    {
        var ssoIdExt = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        await _customers.CreateAsync(new CoreCustomer { CustomerId = customerId, CanOrderOnline = true });
        var participant = await _participants.CreateAsync(new CoreParticipant { CustomerId = customerId, LabCode = "LAB001" });
        _users.Add(ExternalUserType.Participant, ssoIdExt: ssoIdExt, userId: participant.ParticipantId, labParticipantId: participant.ParticipantId, labCode: "LAB001", labName: "North Lab");

        var body = await OkBodyAsync(CreateController().ResolveExternalUser(
            new ResolveExternalUserRequest(ssoIdExt, "participant@example.com", "Jane Doe"), CancellationToken.None));

        Assert.Equal([ExternalRoleNames.Participant, ExternalRoleNames.Viewer], body.Roles);
        Assert.Equal(participant.ParticipantId, body.ParticipantId);
        Assert.Equal("LAB001", body.LabCode);
        Assert.True(body.CanOrderOnline);
        Assert.Equal(new ExternalLab(participant.ParticipantId, "LAB001", "North Lab"), Assert.Single(body.Labs));
    }

    [Fact]
    public async Task ResolveExternalUser_MatchingTestConsultantAndLabUser_ReturnTheirOwnIds()
    {
        var consultantContact = Guid.NewGuid();
        var labUserContact = Guid.NewGuid();
        var consultant = _users.Add(ExternalUserType.TestConsultant, email: "tc@example.com", ssoIdExt: consultantContact);
        var labUser = _users.Add(ExternalUserType.LabUser, email: "lab@example.com", ssoIdExt: labUserContact, labParticipantId: Guid.NewGuid(), labCode: "LAB001", labName: "A");

        var consultantBody = await OkBodyAsync(CreateController().ResolveExternalUser(
            new ResolveExternalUserRequest(consultantContact, "tc@example.com", "TC"), CancellationToken.None));
        var labUserBody = await OkBodyAsync(CreateController().ResolveExternalUser(
            new ResolveExternalUserRequest(labUserContact, "lab@example.com", "Lab"), CancellationToken.None));

        Assert.Equal(consultant.UserId, consultantBody.TestConsultantId);
        Assert.Equal(labUser.UserId, labUserBody.LabUserId);
        Assert.Equal(ExternalRoleNames.LabUser, labUserBody.Roles[0]);
    }

    [Fact]
    public async Task ResolveExternalUser_EmptyContactId_ReturnsBadRequestAndDoesNotResolve()
    {
        var response = await CreateController().ResolveExternalUser(
            new ResolveExternalUserRequest(Guid.Empty, "user@example.com", "Jane Doe"), CancellationToken.None);

        var problem = BadRequestBody(response);
        Assert.Equal([nameof(ResolveExternalUserRequest.SsoIdExt)], problem.Errors.Keys);
        Assert.Equal(0, _users.FindBySsoIdExtCalls);
    }

    [Fact]
    public async Task ResolveExternalUser_EmailLongerThanAnyValidAddress_ReturnsBadRequest()
    {
        var email = new string('a', ResolveExternalUserRequest.MaxEmailLength) + "@example.com";

        var response = await CreateController().ResolveExternalUser(
            new ResolveExternalUserRequest(Guid.NewGuid(), email, "Jane Doe"), CancellationToken.None);

        Assert.Equal([nameof(ResolveExternalUserRequest.Email)], BadRequestBody(response).Errors.Keys);
    }

    [Fact]
    public async Task ResolveExternalUser_DisplayNameTooLong_ReturnsBadRequest()
    {
        var displayName = new string('a', ResolveExternalUserRequest.MaxDisplayNameLength + 1);

        var response = await CreateController().ResolveExternalUser(
            new ResolveExternalUserRequest(Guid.NewGuid(), "user@example.com", displayName), CancellationToken.None);

        Assert.Equal([nameof(ResolveExternalUserRequest.DisplayName)], BadRequestBody(response).Errors.Keys);
    }

    [Fact]
    public async Task ResolveExternalUser_SeveralInvalidFields_ReportsEachOne()
    {
        var response = await CreateController().ResolveExternalUser(
            new ResolveExternalUserRequest(Guid.Empty, new string('a', 300), new string('b', 300)), CancellationToken.None);

        Assert.Equal(3, BadRequestBody(response).Errors.Count);
    }

    [Fact]
    public async Task GetLabs_ReturnsOkWithTheActiveLabs()
    {
        var ssoIdExt = Guid.NewGuid();
        var participant = await _participants.CreateAsync(new CoreParticipant { CustomerId = Guid.NewGuid(), LabCode = "LAB001" });
        _users.Add(ExternalUserType.Participant, ssoIdExt: ssoIdExt, userId: participant.ParticipantId, labParticipantId: participant.ParticipantId, labCode: "LAB001", labName: "North Lab");

        var response = await CreateController().GetLabs(ssoIdExt, CancellationToken.None);

        var labs = Assert.IsAssignableFrom<IReadOnlyList<ExternalLab>>(Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.Equal(new ExternalLab(participant.ParticipantId, "LAB001", "North Lab"), Assert.Single(labs));
    }

    [Fact]
    public async Task GetLab_OneOfTheUsersLabs_ReturnsOk_AndAnyOtherLabIsNotFound()
    {
        var ssoIdExt = Guid.NewGuid();
        var participant = await _participants.CreateAsync(new CoreParticipant { CustomerId = Guid.NewGuid(), LabCode = "LAB001" });
        _users.Add(ExternalUserType.Participant, ssoIdExt: ssoIdExt, userId: participant.ParticipantId, labParticipantId: participant.ParticipantId, labCode: "LAB001", labName: "North Lab");
        var controller = CreateController();

        var found = await controller.GetLab(ssoIdExt, participant.ParticipantId, CancellationToken.None);
        var missing = await controller.GetLab(ssoIdExt, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal("LAB001", Assert.IsType<ExternalLabContext>(Assert.IsType<OkObjectResult>(found.Result).Value).LabCode);
        Assert.IsType<NotFoundResult>(missing.Result);
    }

    [Fact]
    public async Task ResolveExternalUser_LimitsExactlyAtTheMaximum_AreAccepted()
    {
        var body = await OkBodyAsync(CreateController().ResolveExternalUser(
            new ResolveExternalUserRequest(
                Guid.NewGuid(),
                new string('a', ResolveExternalUserRequest.MaxEmailLength),
                new string('b', ResolveExternalUserRequest.MaxDisplayNameLength)),
            CancellationToken.None));

        Assert.Equal(ExternalUserResolutionStatus.NotFound, body.Status);
    }
}
