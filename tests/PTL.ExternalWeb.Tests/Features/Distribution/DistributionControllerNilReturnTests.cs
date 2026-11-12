using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Distribution;
using PTL.ExternalWeb.Features.Distribution;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Features.Distribution;

// The participant Nil Return screen: every path through its GET and POST actions.
public class DistributionControllerNilReturnTests
{
    private static ActualResultsResponse Own(Guid schemeId, bool isReadOnly = false)
    {
        var open = new ActualResultsResponse(Guid.NewGuid(), schemeId, "D26-01A/BA PT001: Bacteriology", "audit", new DateTime(2026, 4, 30), false, []);
        return open with { IsReadOnly = isReadOnly };
    }

    private static DistributionController SignedIn(FakeDistributionApiClient apiClient)
    {
        var controller = new DistributionController(apiClient);
        AuthenticatedParticipant.Attach(controller, Guid.NewGuid());
        return controller;
    }

    private static DistributionController NotSignedIn(FakeDistributionApiClient apiClient) =>
        new(apiClient) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

    private static DistributionNilReturnViewModel Posted(Guid? schemeId, string? reason = "No samples received") =>
        new() { MonthlyDistributionSchemeId = schemeId, NilReturnReason = reason };

    private static string[] FormErrors(DistributionController controller) =>
        [.. controller.ModelState[string.Empty]!.Errors.Select(error => error.ErrorMessage)];

    [Fact]
    public async Task EntryGet_NoRecordForTheSignedInParticipant_ReturnsToCurrent()
    {
        var result = await SignedIn(new FakeDistributionApiClient { ActualResults = null }).Entry(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(nameof(DistributionController.Current), Assert.IsType<RedirectToActionResult>(result).ActionName);
    }

    [Fact]
    public async Task EntryPost_ClosedDistribution_RedisplaysTheSavedNilReturn()
    {
        var schemeId = Guid.NewGuid();
        var apiClient = new FakeDistributionApiClient
        {
            ActualResults = Own(schemeId, isReadOnly: true),
            ParticipantNilReturn = new ParticipantNilReturnResponse(true, "No samples received")
        };

        var result = await SignedIn(apiClient).Entry(new DistributionResultsEntryViewModel { MonthlyDistributionSchemeId = schemeId }, null, CancellationToken.None);

        var model = Assert.IsType<DistributionResultsEntryViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.True(model.HasNilReturn);
        Assert.Equal("No samples received", model.NilReturnReason);
    }

    [Fact]
    public async Task EntryPost_ClosedDistribution_NoNilReturnSaved_LeavesTheNilReturnStateEmpty()
    {
        var schemeId = Guid.NewGuid();
        var apiClient = new FakeDistributionApiClient { ActualResults = Own(schemeId, isReadOnly: true), ParticipantNilReturn = null };

        var result = await SignedIn(apiClient).Entry(new DistributionResultsEntryViewModel { MonthlyDistributionSchemeId = schemeId }, null, CancellationToken.None);

        var model = Assert.IsType<DistributionResultsEntryViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.False(model.HasNilReturn);
        Assert.Null(model.NilReturnReason);
    }

    [Fact]
    public async Task Get_NoSignedInParticipant_ReturnsToCurrent()
    {
        var schemeId = Guid.NewGuid();

        var result = await NotSignedIn(new FakeDistributionApiClient { ActualResults = Own(schemeId) }).NilReturn(schemeId, CancellationToken.None);

        Assert.Equal(nameof(DistributionController.Current), Assert.IsType<RedirectToActionResult>(result).ActionName);
    }

    [Fact]
    public async Task Get_NoRecordForTheSignedInParticipant_ReturnsToCurrent()
    {
        var result = await SignedIn(new FakeDistributionApiClient { ActualResults = null }).NilReturn(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(nameof(DistributionController.Current), Assert.IsType<RedirectToActionResult>(result).ActionName);
    }

    [Fact]
    public async Task Get_ClosedDistribution_GoesBackToTheEntryPageInsteadOfOfferingTheForm()
    {
        var schemeId = Guid.NewGuid();

        var result = await SignedIn(new FakeDistributionApiClient { ActualResults = Own(schemeId, isReadOnly: true) })
            .NilReturn(schemeId, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DistributionController.Entry), redirect.ActionName);
        Assert.Equal(schemeId, redirect.RouteValues!["schemeId"]);
    }

    [Fact]
    public async Task Get_NothingSavedYet_ShowsAnEmptyForm()
    {
        var schemeId = Guid.NewGuid();

        var result = await SignedIn(new FakeDistributionApiClient { ActualResults = Own(schemeId), ParticipantNilReturn = null })
            .NilReturn(schemeId, CancellationToken.None);

        var model = Assert.IsType<DistributionNilReturnViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.False(model.HasNilReturn);
        Assert.Null(model.NilReturnReason);
    }

    [Fact]
    public async Task Post_Cancel_ReturnsToTheEntryPageWithoutSaving()
    {
        var schemeId = Guid.NewGuid();
        var apiClient = new FakeDistributionApiClient { ActualResults = Own(schemeId) };

        var result = await SignedIn(apiClient).NilReturn(Posted(schemeId), "cancel", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DistributionController.Entry), redirect.ActionName);
        Assert.Equal(schemeId, redirect.RouteValues!["schemeId"]);
        Assert.Empty(apiClient.SavedParticipantNilReturns);
    }

    [Fact]
    public async Task Post_NoSchemeIdPosted_ReturnsToCurrentWithoutSaving()
    {
        var apiClient = new FakeDistributionApiClient { ActualResults = Own(Guid.NewGuid()) };

        var result = await SignedIn(apiClient).NilReturn(Posted(null), "submit", CancellationToken.None);

        Assert.Equal(nameof(DistributionController.Current), Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Empty(apiClient.SavedParticipantNilReturns);
    }

    [Fact]
    public async Task Post_NoRecordForTheSignedInParticipant_ReturnsToCurrentWithoutSaving()
    {
        var apiClient = new FakeDistributionApiClient { ActualResults = null };

        var result = await SignedIn(apiClient).NilReturn(Posted(Guid.NewGuid()), "submit", CancellationToken.None);

        Assert.Equal(nameof(DistributionController.Current), Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Empty(apiClient.SavedParticipantNilReturns);
    }

    [Fact]
    public async Task Post_ClosedDistribution_ShowsTheClosedMessageAndSavesNothing()
    {
        var schemeId = Guid.NewGuid();
        var apiClient = new FakeDistributionApiClient { ActualResults = Own(schemeId, isReadOnly: true) };
        var controller = SignedIn(apiClient);

        var result = await controller.NilReturn(Posted(schemeId, "Too late"), "submit", CancellationToken.None);

        var model = Assert.IsType<DistributionNilReturnViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("Too late", model.NilReturnReason);
        Assert.Equal(["This distribution is closed and results can no longer be edited."], FormErrors(controller));
        Assert.Empty(apiClient.SavedParticipantNilReturns);
    }

    [Fact]
    public async Task Post_SaveEndpointReturnsNothing_AsksTheParticipantToTryAgain()
    {
        var schemeId = Guid.NewGuid();
        var apiClient = new FakeDistributionApiClient { ActualResults = Own(schemeId), ParticipantNilReturnSaveResult = null };
        var controller = SignedIn(apiClient);

        var result = await controller.NilReturn(Posted(schemeId), "submit", CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Equal(["Nil return could not be saved. Please try again later."], FormErrors(controller));
    }

    [Fact]
    public async Task Post_DeadlinePassedWhileSaving_ShowsTheClosedMessage()
    {
        var schemeId = Guid.NewGuid();
        var apiClient = new FakeDistributionApiClient
        {
            ActualResults = Own(schemeId),
            ParticipantNilReturnSaveResult = new ParticipantNilReturnSaveResult(false, DeadlinePassed: true)
        };
        var controller = SignedIn(apiClient);

        var result = await controller.NilReturn(Posted(schemeId), "submit", CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Equal(["This distribution is closed and results can no longer be edited."], FormErrors(controller));
    }

    [Fact]
    public async Task Post_ResultsAlreadyEntered_ExplainsWhyANilReturnCannotBeSubmitted()
    {
        var schemeId = Guid.NewGuid();
        var apiClient = new FakeDistributionApiClient
        {
            ActualResults = Own(schemeId),
            ParticipantNilReturnSaveResult = new ParticipantNilReturnSaveResult(false, HasResults: true)
        };
        var controller = SignedIn(apiClient);

        var result = await controller.NilReturn(Posted(schemeId), "submit", CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Equal(["Results have already been entered for this distribution and a nil return cannot be submitted."], FormErrors(controller));
    }
}
