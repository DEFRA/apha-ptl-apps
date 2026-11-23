using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Polly.Timeout;
using PTL.ExternalWeb.Features.ErrorPages;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Features.ErrorPages;

public class SupportContactViewComponentTests
{
    private const string LeadIn = "If the problem continues, contact the Animal and Plant Health Agency";

    private readonly CapturingLogger _logger = new();

    private SupportContactViewComponent Component(FakeExternalSiteMessageApiClient apiClient, HttpContext? httpContext = null) =>
        new(apiClient, _logger)
        {
            ViewComponentContext = new ViewComponentContext
            {
                ViewContext = new ViewContext { HttpContext = httpContext ?? new DefaultHttpContext() },
            },
        };

    private static SupportContactViewModel Model(Microsoft.AspNetCore.Mvc.IViewComponentResult result) =>
        Assert.IsType<SupportContactViewModel>(Assert.IsType<ViewViewComponentResult>(result).ViewData!.Model);

    [Fact]
    public async Task InvokeAsync_SupportEmailSet_ShowsItTrimmed_WithTheLeadIn()
    {
        var component = Component(new FakeExternalSiteMessageApiClient("  support@apha.test \t"));

        var model = Model(await component.InvokeAsync(LeadIn));

        Assert.Equal(new SupportContactViewModel(LeadIn, "support@apha.test"), model);
        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public async Task InvokeAsync_UsesTheDefaultView()
    {
        var component = Component(new FakeExternalSiteMessageApiClient());

        var result = Assert.IsType<ViewViewComponentResult>(await component.InvokeAsync(LeadIn));

        Assert.Null(result.ViewName);
    }

    [Fact]
    public async Task InvokeAsync_PassesTheRequestAbortedToken()
    {
        using var aborted = new CancellationTokenSource();
        var httpContext = new DefaultHttpContext { RequestAborted = aborted.Token };
        var apiClient = new FakeExternalSiteMessageApiClient();

        await Component(apiClient, httpContext).InvokeAsync(LeadIn);

        Assert.Equal(aborted.Token, apiClient.ReceivedCancellationToken);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public async Task InvokeAsync_NoSupportEmailSet_ShowsTheLeadInWithoutAnEmail(string? supportEmailAddress)
    {
        var component = Component(new FakeExternalSiteMessageApiClient(supportEmailAddress!));

        var model = Model(await component.InvokeAsync(LeadIn));

        Assert.Equal(LeadIn, model.LeadIn);
        Assert.Null(model.EmailAddress);
        Assert.Empty(_logger.Entries);
    }

    public static TheoryData<Exception> UnavailableApiExceptions() => new()
    {
        new HttpRequestException("PTL.Api is unreachable"),
        new TaskCanceledException("PTL.Api did not answer in time"),
        new BrokenCircuitException(),
        new TimeoutRejectedException(),
        new JsonException("not JSON"),
    };

    [Theory]
    [MemberData(nameof(UnavailableApiExceptions))]
    public async Task InvokeAsync_SupportEmailCannotBeRead_ShowsTheLeadInWithoutAnEmail_AndLogsAWarning(Exception exception)
    {
        var component = Component(new FakeExternalSiteMessageApiClient("support@apha.test", exception));

        var model = Model(await component.InvokeAsync(LeadIn));

        Assert.Equal(LeadIn, model.LeadIn);
        Assert.Null(model.EmailAddress);
        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Same(exception, entry.Exception);
        Assert.Equal("Could not read the support email address for an error page.", entry.Message);
    }

    [Fact]
    public async Task InvokeAsync_UnexpectedException_IsNotSwallowed()
    {
        var exception = new InvalidOperationException("a defect, not an unavailable API");
        var component = Component(new FakeExternalSiteMessageApiClient("support@apha.test", exception));

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => component.InvokeAsync(LeadIn));

        Assert.Same(exception, thrown);
        Assert.Empty(_logger.Entries);
    }

    private sealed class CapturingLogger : ILogger<SupportContactViewComponent>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));
    }
}
