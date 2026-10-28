using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Participant;
using PTL.ExternalWeb.Features.Participant;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Features.Participant;

// Unit-level tests for the controller's own branching (view selection, error-dictionary mapping) -
// see Integration/ParticipantRouteSmokeTests.cs for full-pipeline coverage of the real participant
// claims/authorization/rendering.
public class ParticipantControllerTests
{
    private static ParticipantController CreateController(FakeParticipantDetailsEditApiClient? apiClient = null, FakeLookupApiClient? lookupApiClient = null) =>
        new(apiClient ?? new FakeParticipantDetailsEditApiClient(), lookupApiClient ?? new FakeLookupApiClient());

    [Fact]
    public async Task Index_Post_ValidationFailure_AddsModelStateErrorsFromApiResult()
    {
        var apiClient = new FakeParticipantDetailsEditApiClient
        {
            SubmitResult = new ParticipantDetailsEditSaveResult(false, new Dictionary<string, string[]> { ["ContactName"] = ["Enter Contact Name"] })
        };
        var controller = CreateController(apiClient);
        AuthenticatedParticipant.Attach(controller, Guid.NewGuid());

        var result = await controller.Index(new ParticipantDetailsEditViewModel(), CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.IsType<ParticipantDetailsEditViewModel>(viewResult.Model);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState["ContactName"]!.Errors, e => e.ErrorMessage == "Enter Contact Name");
    }

    [Fact]
    public async Task Index_Post_SuccessfulSubmission_RedirectsBackToIndex()
    {
        var apiClient = new FakeParticipantDetailsEditApiClient { SubmitResult = new ParticipantDetailsEditSaveResult(true, new Dictionary<string, string[]>()) };
        var controller = CreateController(apiClient);
        AuthenticatedParticipant.Attach(controller, Guid.NewGuid());

        var result = await controller.Index(new ParticipantDetailsEditViewModel { ContactName = "Jane Doe" }, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ParticipantController.Index), redirect.ActionName);
    }
}
