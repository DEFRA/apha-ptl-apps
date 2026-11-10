using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using PTL.Contracts.Lookup;
using PTL.InternalWeb.Features.Distribution;
using PTL.InternalWeb.Tests.TestSupport;
using PTL.SharedUI.Notifications;
using FullGroupAddressResponse = PTL.Contracts.GroupAddress.GroupAddressResponse;

namespace PTL.InternalWeb.Tests.Features.Distribution;

// Group Address Labels - legacy Distributions/GroupAddress.aspx. No search/filter - always
// fetches every configured Group Address and renders the grid (or the empty-state message).
public class DistributionControllerGroupAddressLabelsTests
{
    private static DistributionController CreateController(
        FakeLookupApiClient lookupApiClient,
        FakeGroupAddressApiClient? groupAddressApiClient = null,
        FakeLabelPrinterApiClient? labelPrinterApiClient = null) =>
        new(
            new FakeDistributionApiClient(),
            new FakeSearchApiClient(),
            lookupApiClient,
            groupAddressApiClient ?? new FakeGroupAddressApiClient(),
            new FakeParticipantApiClient(),
            labelPrinterApiClient ?? new FakeLabelPrinterApiClient())
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider())
        };

    private static GroupAddressLabelsViewModel ModelOf(IActionResult result) =>
        Assert.IsType<GroupAddressLabelsViewModel>(Assert.IsType<ViewResult>(result).Model);

    [Fact]
    public async Task Get_WithGroupAddresses_ReturnsAViewModelWhoseGroupAddressesMatchExactlyWhatTheServiceReturned()
    {
        var countryId = Guid.NewGuid();
        var groupAddresses = new List<GroupAddressResponse>
        {
            new(Guid.NewGuid(), "GA1", "1 Group Street", countryId),
            new(Guid.NewGuid(), "GA2", "2 Group Street", countryId),
        };
        var lookupApiClient = new FakeLookupApiClient { GroupAddresses = groupAddresses };
        var controller = CreateController(lookupApiClient);

        var model = ModelOf(await controller.GroupAddressLabels(CancellationToken.None));

        Assert.Equal(groupAddresses, model.GroupAddresses);
    }

    [Fact]
    public async Task Get_NoGroupAddressesConfigured_GroupAddressesIsAnEmptyListNotNull()
    {
        var lookupApiClient = new FakeLookupApiClient();
        var controller = CreateController(lookupApiClient);

        var model = ModelOf(await controller.GroupAddressLabels(CancellationToken.None));

        Assert.NotNull(model.GroupAddresses);
        Assert.Empty(model.GroupAddresses);
    }

    [Fact]
    public async Task PrintGroupAddressLabel_KnownId_PrintsTheLegacyFieldOrderAndRedirectsBackWithSuccessNotification()
    {
        var countryId = Guid.NewGuid();
        var groupAddressId = Guid.NewGuid();
        var groupAddressApiClient = new FakeGroupAddressApiClient
        {
            GroupAddress = new FullGroupAddressResponse(groupAddressId, "GA1", "1 Group Street", "Group District", string.Empty, string.Empty, string.Empty, countryId, "01234 567890", string.Empty)
        };
        var lookupApiClient = new FakeLookupApiClient { Countries = [new CountryResponse(countryId, "United Kingdom")] };
        var labelPrinterApiClient = new FakeLabelPrinterApiClient();
        var controller = CreateController(lookupApiClient, groupAddressApiClient, labelPrinterApiClient);

        var result = await controller.PrintGroupAddressLabel(groupAddressId, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DistributionController.GroupAddressLabels), redirect.ActionName);
        var lines = Assert.Single(labelPrinterApiClient.PrintedLines);
        Assert.Equal(["1 Group Street", "Group District", "United Kingdom", "01234 567890"], lines);
        Assert.Equal(NotificationType.Success, controller.TempData.GetNotification()!.Type);
    }

    [Fact]
    public async Task PrintGroupAddressLabel_PrinterFails_RedirectsWithErrorNotification()
    {
        var groupAddressId = Guid.NewGuid();
        var groupAddressApiClient = new FakeGroupAddressApiClient
        {
            GroupAddress = new FullGroupAddressResponse(groupAddressId, "GA1", "1 Group Street", string.Empty, string.Empty, string.Empty, string.Empty, Guid.NewGuid(), string.Empty, string.Empty)
        };
        var labelPrinterApiClient = new FakeLabelPrinterApiClient { PrintResult = false };
        var controller = CreateController(new FakeLookupApiClient(), groupAddressApiClient, labelPrinterApiClient);

        var result = await controller.PrintGroupAddressLabel(groupAddressId, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(NotificationType.Error, controller.TempData.GetNotification()!.Type);
    }

    [Fact]
    public async Task PrintGroupAddressLabel_UnknownId_ReturnsNotFound()
    {
        var controller = CreateController(new FakeLookupApiClient());

        var result = await controller.PrintGroupAddressLabel(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}
