using Microsoft.AspNetCore.Mvc;
using PTL.Api.Controllers;
using PTL.Api.Tests.Participant;
using PTL.Contracts.Participant;
using PTL.Core.Participant;

namespace PTL.Api.Tests.Endpoints;

public class ParticipantDetailsEditControllerTests
{
    private static (ParticipantDetailsEditController Controller, FakeParticipantRepository Participants, FakePendingParticipantUpdateRepository PendingUpdates) CreateController()
    {
        var participants = new FakeParticipantRepository();
        var pendingUpdates = new FakePendingParticipantUpdateRepository();
        var service = new ParticipantDetailsEditService(participants, pendingUpdates);
        return (new ParticipantDetailsEditController(service), participants, pendingUpdates);
    }

    [Fact]
    public async Task Get_ParticipantNotFound_ReturnsNotFound()
    {
        var (controller, _, _) = CreateController();

        var result = await controller.Get(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Get_ParticipantFound_ReturnsMappedResponse()
    {
        var (controller, participants, _) = CreateController();
        var participant = await participants.CreateAsync(new PTL.Core.Participant.Participant { LabCode = "001", ContactName = "Jane Doe" });

        var result = await controller.Get(participant.ParticipantId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ParticipantDetailsEditResponse>(ok.Value);
        Assert.Equal("Jane Doe", response.ContactName);
        Assert.Equal("001", response.LabCode);
        Assert.False(response.HasOutstandingUpdate);
    }

    [Fact]
    public async Task Submit_ValidRequest_ReturnsSuccessResult()
    {
        var (controller, participants, _) = CreateController();
        var participant = await participants.CreateAsync(new PTL.Core.Participant.Participant { LabCode = "001" });

        var request = new ParticipantDetailsEditSaveRequest(
            "Jane Doe", "Test Lab", "1 Test Street", "Testville", "", "", "",
            Guid.NewGuid(), "01234 567890", "jane@example.com", "");

        var result = await controller.Submit(participant.ParticipantId, request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var saveResult = Assert.IsType<ParticipantDetailsEditSaveResult>(ok.Value);
        Assert.True(saveResult.Success);
    }

    [Fact]
    public async Task Submit_InvalidRequest_ReturnsFailureResultWithFieldErrors()
    {
        var (controller, participants, _) = CreateController();
        var participant = await participants.CreateAsync(new PTL.Core.Participant.Participant { LabCode = "001" });

        var request = new ParticipantDetailsEditSaveRequest(
            "", "", "", "", "", "", "", Guid.Empty, "", "", "");

        var result = await controller.Submit(participant.ParticipantId, request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var saveResult = Assert.IsType<ParticipantDetailsEditSaveResult>(ok.Value);
        Assert.False(saveResult.Success);
        Assert.Contains("ContactName", saveResult.Errors.Keys);
    }

    [Fact]
    public async Task Submit_OutstandingPendingUpdateAlreadyExists_ReturnsFailureResult()
    {
        var (controller, participants, pendingUpdates) = CreateController();
        var participant = await participants.CreateAsync(new PTL.Core.Participant.Participant { LabCode = "001" });
        await pendingUpdates.UpsertAsync(new PendingParticipantUpdate { ParticipantId = participant.ParticipantId, IsSubmitted = true });

        var request = new ParticipantDetailsEditSaveRequest(
            "Jane Doe", "Test Lab", "1 Test Street", "Testville", "", "", "",
            Guid.NewGuid(), "01234 567890", "jane@example.com", "");

        var result = await controller.Submit(participant.ParticipantId, request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var saveResult = Assert.IsType<ParticipantDetailsEditSaveResult>(ok.Value);
        Assert.False(saveResult.Success);
    }
}
