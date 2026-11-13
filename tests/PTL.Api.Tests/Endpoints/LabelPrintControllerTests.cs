using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.Api.Controllers;
using PTL.Contracts.Labels;
using PTL.Core.Labels;

namespace PTL.Api.Tests.Endpoints;

public class LabelPrintControllerTests : IClassFixture<WebApplicationFactory<Program>>
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

    private readonly WebApplicationFactory<Program> _factory;

    public LabelPrintControllerTests(WebApplicationFactory<Program> factory)
    {
        // Startup checks read these before the host is built (see HealthEndpointsTests).
        Environment.SetEnvironmentVariable("Database__Host", "localhost");
        Environment.SetEnvironmentVariable("Database__Name", "ProficiencyTesting");
        Environment.SetEnvironmentVariable("Database__IntegratedSecurity", "true");
        Environment.SetEnvironmentVariable("Database__TrustServerCertificate", "true");
        Environment.SetEnvironmentVariable("HealthCheck__ReadinessKey", "local-dev-readiness-key");

        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TabulationEmails:Enabled"] = "false",
                ["PublicationNotifications:Enabled"] = "false",
            })));
    }

    // PTL.Api has no cookie session, so a CSRF filter has nothing to protect and cannot even be built:
    // the API registers no anti-forgery services, which made every call fail with a 500.
    [Fact]
    public async Task Post_Print_ReturnsSuccessThroughTheRealPipeline()
    {
        var printerClient = new FakeLabelPrinterClient();
        var client = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<ILabelPrinterClient>();
            services.AddSingleton<ILabelPrinterClient>(printerClient);
        })).CreateClient();

        var response = await client.PostAsJsonAsync("/api/labels/print", new LabelPrintRequest(["Alice Example", "1 Sample Street"]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<LabelPrintResponse>())!.Printed);
        Assert.Equal(["Alice Example", "1 Sample Street"], printerClient.PrintedLabel!.Lines);
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
