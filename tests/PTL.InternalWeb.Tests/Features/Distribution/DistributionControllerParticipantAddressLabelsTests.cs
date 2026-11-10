using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Lookup;
using PTL.Contracts.Participant;
using PTL.Contracts.Search;
using PTL.InternalWeb.Features.Distribution;
using PTL.InternalWeb.Features.Search;
using PTL.InternalWeb.Tests.TestSupport;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Tests.Features.Distribution;

// Participant Address Labels - legacy Distributions/ParticipantAddress.aspx. Reuses the same
// dbo.spgSearchParticipant-backed search as Features/Search's Participants tab.
public class DistributionControllerParticipantAddressLabelsTests
{
    private static DistributionController CreateController(
        FakeSearchApiClient searchApiClient,
        FakeLookupApiClient? lookupApiClient = null,
        FakeParticipantApiClient? participantApiClient = null,
        FakeLabelPrinterApiClient? labelPrinterApiClient = null) =>
        new(
            new FakeDistributionApiClient(),
            searchApiClient,
            lookupApiClient ?? new FakeLookupApiClient(),
            new FakeGroupAddressApiClient(),
            participantApiClient ?? new FakeParticipantApiClient(),
            labelPrinterApiClient ?? new FakeLabelPrinterApiClient())
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider())
        };

    private static ParticipantAddressLabelsViewModel ModelOf(IActionResult result) =>
        Assert.IsType<ParticipantAddressLabelsViewModel>(Assert.IsType<ViewResult>(result).Model);

    private static ParticipantSearchResultResponse Row(string labCode) =>
        new(Guid.NewGuid(), Guid.NewGuid(), labCode, "OLD1", "Lab One", "Bob Example");

    [Fact]
    public async Task Get_FirstLoad_NotSearchedAndNoFormSupplied_ResultsStayNullAndTheServiceIsNotCalled()
    {
        var search = new FakeSearchApiClient();
        var controller = CreateController(search);

        var model = ModelOf(await controller.ParticipantAddressLabels(searched: false, search: null, CancellationToken.None));

        Assert.Null(model.Results);
        Assert.Empty(search.ParticipantSearches);
        Assert.Null(model.Search.LabCode);
        Assert.Null(model.Search.LabName);
        Assert.Null(model.Search.ContactName);
    }

    [Fact]
    public async Task Get_Searched_BlankFields_StillSearches_TreatingBlankAsMatchEveryParticipant()
    {
        var search = new FakeSearchApiClient { ParticipantResults = [Row("LAB1")] };
        var controller = CreateController(search);

        var model = ModelOf(await controller.ParticipantAddressLabels(searched: true, search: new ParticipantSearchForm(), CancellationToken.None));

        var criteria = Assert.Single(search.ParticipantSearches);
        Assert.Null(criteria.LabCode);
        Assert.Null(criteria.LabName);
        Assert.Null(criteria.ContactName);
        Assert.Single(model.Results!);
        Assert.Equal("LAB1", model.Results![0].LabCode);
    }

    [Fact]
    public async Task Get_Searched_NoMatches_ReturnsAnEmptyListNotNull()
    {
        var search = new FakeSearchApiClient();
        var controller = CreateController(search);

        var model = ModelOf(await controller.ParticipantAddressLabels(searched: true, search: new ParticipantSearchForm(), CancellationToken.None));

        Assert.NotNull(model.Results);
        Assert.Empty(model.Results);
    }

    [Fact]
    public async Task Get_Searched_WithCriteria_ForwardsExactlyTheSubmittedValuesToTheService()
    {
        var search = new FakeSearchApiClient { ParticipantResults = [Row("LAB2")] };
        var controller = CreateController(search);
        var form = new ParticipantSearchForm { LabCode = "LAB2", LabName = "Lab Two", ContactName = "Carol Example" };

        await controller.ParticipantAddressLabels(searched: true, search: form, CancellationToken.None);

        var criteria = Assert.Single(search.ParticipantSearches);
        Assert.Equal(form.ToCriteria(), criteria);
    }

    [Fact]
    public async Task Get_Searched_ButModelStateInvalid_DoesNotCallTheServiceAndResultsStayNull()
    {
        var search = new FakeSearchApiClient { ParticipantResults = [Row("LAB1")] };
        var controller = CreateController(search);
        controller.ModelState.AddModelError("Search.LabCode", "The Lab Code must be at most 8 characters.");

        var model = ModelOf(await controller.ParticipantAddressLabels(searched: true, search: new ParticipantSearchForm { LabCode = "TOO-LONG-CODE" }, CancellationToken.None));

        Assert.Empty(search.ParticipantSearches);
        Assert.Null(model.Results);
    }

    [Fact]
    public async Task PrintParticipantAddressLabel_KnownId_PrintsWithContactOrganisationAndTelephoneAfterCountry_AndRedirectsBackWithTheSameSearchCriteria()
    {
        var countryId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var participant = new ParticipantResponse(
            participantId, Guid.NewGuid(), Guid.NewGuid(), "LAB1", "Lab One", Guid.NewGuid(),
            "Bob Example", "Sample Labs", "1 Sample Street", "Sample District", string.Empty, string.Empty, string.Empty,
            countryId, "01234 567890", string.Empty, string.Empty, string.Empty, string.Empty, true, null, false, null);
        var participantApiClient = new FakeParticipantApiClient { ParticipantResponse = participant };
        var lookupApiClient = new FakeLookupApiClient { Countries = [new CountryResponse(countryId, "United Kingdom")] };
        var labelPrinterApiClient = new FakeLabelPrinterApiClient();
        var controller = CreateController(new FakeSearchApiClient(), lookupApiClient, participantApiClient, labelPrinterApiClient);

        var result = await controller.PrintParticipantAddressLabel(participantId, "LAB1", "Lab One", "Bob Example", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DistributionController.ParticipantAddressLabels), redirect.ActionName);
        Assert.Equal(true, redirect.RouteValues!["searched"]);
        Assert.Equal("LAB1", redirect.RouteValues!["search.LabCode"]);
        Assert.Equal("Lab One", redirect.RouteValues!["search.LabName"]);
        Assert.Equal("Bob Example", redirect.RouteValues!["search.ContactName"]);

        var lines = Assert.Single(labelPrinterApiClient.PrintedLines);
        Assert.Contains("Bob Example", lines);
        Assert.Contains("Sample Labs", lines);
        var orderedLines = lines.ToList();
        Assert.True(orderedLines.IndexOf("United Kingdom") < orderedLines.IndexOf("01234 567890"));
        Assert.Equal(NotificationType.Success, controller.TempData.GetNotification()!.Type);
    }

    [Fact]
    public async Task PrintParticipantAddressLabel_UnknownId_ReturnsNotFound()
    {
        var controller = CreateController(new FakeSearchApiClient());

        var result = await controller.PrintParticipantAddressLabel(Guid.NewGuid(), null, null, null, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}
