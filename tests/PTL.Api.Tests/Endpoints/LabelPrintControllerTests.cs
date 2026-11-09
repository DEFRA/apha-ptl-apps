using Microsoft.AspNetCore.Mvc;
using PTL.Api.Controllers;
using PTL.Contracts.Labels;
using PTL.Core.Labels;

namespace PTL.Api.Tests.Endpoints;

public class LabelPrintControllerTests
{
    private sealed class FakeLabelPrinterClient : ILabelPrinterClient
    {
        public bool PrintResult { get; set; } = true;
        public AddressLabel? PrintedLabel { get; private set; }

        public Task<bool> PrintAsync(AddressLabel label, CancellationToken cancellationToken = default)
        {
            PrintedLabel = label;
            return Task.FromResult(PrintResult);
        }
    }

    [Fact]
    public async Task Print_ForwardsLinesToTheLabelPrinterClientAndReturnsItsResult()
    {
        var printerClient = new FakeLabelPrinterClient();
        var controller = new LabelPrintController(printerClient);

        var result = await controller.Print(new LabelPrintRequest(["Alice Example", "1 Sample Street"]), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<LabelPrintResponse>(ok.Value);
        Assert.True(response.Printed);
        Assert.Equal(["Alice Example", "1 Sample Street"], printerClient.PrintedLabel!.Lines);
    }

    [Fact]
    public async Task Print_PrinterFails_ReturnsPrintedFalse()
    {
        var printerClient = new FakeLabelPrinterClient { PrintResult = false };
        var controller = new LabelPrintController(printerClient);

        var result = await controller.Print(new LabelPrintRequest(["Alice Example"]), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<LabelPrintResponse>(ok.Value);
        Assert.False(response.Printed);
    }
}
