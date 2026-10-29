using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Distribution;
using PTL.ExternalWeb.Features.Distribution;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Features.Distribution;

public class DistributionControllerTests
{
    [Fact]
    public async Task Current_ReturnsMappedCurrentDistributions()
    {
        var participantId = Guid.NewGuid();
        var apiClient = new FakeDistributionApiClient
        {
            CurrentDistributions =
            [
                new CurrentDistributionResponse(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "D26-01A/BA",
                    "PT001",
                    "Bacteriology",
                    new DateTime(2026, 4, 3),
                    new DateTime(2026, 4, 30),
                    HasResults: false,
                    Submitted: false)
            ]
        };

        var controller = new DistributionController();
        AuthenticatedParticipant.Attach(controller, participantId);

        var result = await controller.Current(apiClient, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CurrentDistributionsViewModel>(view.Model);
        var row = Assert.Single(model.Distributions);
        Assert.Equal("D26-01A/BA", row.DistributionReference);
        Assert.False(row.ResultsEntered);
    }

    [Fact]
    public async Task Entry_Post_SuccessfulSave_RedirectsToCurrent()
    {
        var schemeId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var sampleResultItemId = Guid.NewGuid();
        var methodItemId = Guid.NewGuid();

        var apiClient = new FakeDistributionApiClient
        {
            ActualResultsSaveResult = new ActualResultsSaveResult(true, new Dictionary<Guid, string>())
        };

        var controller = new DistributionController();
        AuthenticatedParticipant.Attach(controller, participantId);

        var model = new DistributionResultsEntryViewModel
        {
            MonthlyDistributionSchemeId = schemeId,
            DistributionParticipantId = participantId,
            Heading = "D26-01A/BA PT001: Bacteriology",
            Tests =
            [
                new DistributionResultsEntryTestViewModel
                {
                    TestId = Guid.NewGuid(),
                    TestName = "Bacteriology",
                    MethodItems = [new DistributionResultsEntryMethodItemViewModel { TestMethodItemId = methodItemId, Name = "Method", FormatString = "^.*$", ReturnValueType = "Text", Result = "PCR" }],
                    Samples =
                    [
                        new DistributionResultsEntrySampleRowViewModel
                        {
                            SampleId = Guid.NewGuid(),
                            SampleNumber = "S001",
                            Values = [new DistributionResultsEntrySampleValueViewModel { SampleResultItemId = sampleResultItemId, TestResultItemId = Guid.NewGuid(), Result = "Negative" }]
                        }
                    ]
                }
            ]
        };

        var result = await controller.Entry(model, "save", apiClient, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DistributionController.Current), redirect.ActionName);
        Assert.Single(apiClient.SavedActualResultsRequests);
    }

    [Fact]
    public void ResultsEntryResultItemViewModel_DateReturnValueType_ShowsDdMmYyyyHint()
    {
        var item = new DistributionResultsEntryResultItemViewModel
        {
            ReturnValueType = "Date of testing",
            FormatString = "^.*$"
        };

        Assert.Equal("Input must be of format: dd/MM/yyyy", item.InputFormatMessage);
    }

    [Fact]
    public void Previous_ReturnsComingSoonViewForViewResults()
    {
        var controller = new DistributionController();

        var result = controller.Previous();

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("_ComingSoonBody", viewResult.ViewName);
        Assert.Equal("View results", viewResult.Model);
    }
}
