using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Api.Infrastructure;
using PTL.Common.ApiAuthentication;

namespace PTL.Api.Tests.Infrastructure;

public sealed class ApiKeyAuthenticationHandlerTests : IDisposable
{
    private const string ExpectedKey = "expected-secret-key";
    private const string WrongKey = "wrong-secret-key";

    private readonly CapturingLoggerFactory _loggerFactory = new();

    public void Dispose() => _loggerFactory.Dispose();

    private async Task<(ApiKeyAuthenticationHandler Handler, HttpContext Context)> CreateAsync(
        string? apiKey, string? clientApp, string configuredKey = ExpectedKey)
    {
        var context = new DefaultHttpContext();
        if (apiKey is not null)
        {
            context.Request.Headers[ApiAuthenticationOptions.ApiKeyHeaderName] = apiKey;
        }

        if (clientApp is not null)
        {
            context.Request.Headers[ApiAuthenticationOptions.ClientAppHeaderName] = clientApp;
        }

        var handler = new ApiKeyAuthenticationHandler(
            new StaticOptionsMonitor(),
            _loggerFactory,
            UrlEncoder.Default,
            Options.Create(new ApiAuthenticationOptions { ApiKey = configuredKey }));
        await handler.InitializeAsync(
            new AuthenticationScheme(ApiKeyAuthenticationHandler.SchemeName, null, typeof(ApiKeyAuthenticationHandler)), context);
        return (handler, context);
    }

    [Theory]
    [InlineData(ApiAuthenticationOptions.InternalWeb)]
    [InlineData(ApiAuthenticationOptions.ExternalWeb)]
    public async Task ValidKey_AuthenticatesTheClientAppOnly(string clientApp)
    {
        var (handler, _) = await CreateAsync(ExpectedKey, clientApp);

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        var claim = Assert.Single(result.Principal!.Claims);
        Assert.Equal(ApiKeyAuthenticationHandler.ClientAppClaimType, claim.Type);
        Assert.Equal(clientApp, claim.Value);
        Assert.Null(result.Principal.Identity!.Name);
        Assert.DoesNotContain(_loggerFactory.Entries, e => e.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task ValidKey_WithoutClientApp_AuthenticatesWithNoClientAppClaim()
    {
        var (handler, _) = await CreateAsync(ExpectedKey, clientApp: null);

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.Empty(result.Principal!.Claims);
    }

    [Fact]
    public async Task MissingKey_IsNoResult_AndTheChallengeLogsIt()
    {
        var (handler, context) = await CreateAsync(apiKey: null, ApiAuthenticationOptions.InternalWeb);

        var result = await handler.AuthenticateAsync();
        await handler.ChallengeAsync(new AuthenticationProperties());

        Assert.True(result.None);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Contains(_loggerFactory.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("missing API key", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InvalidKey_FailsAndLogsWithoutEitherKeyValue()
    {
        var (handler, context) = await CreateAsync(WrongKey, ApiAuthenticationOptions.InternalWeb);

        var result = await handler.AuthenticateAsync();
        await handler.ChallengeAsync(new AuthenticationProperties());

        Assert.NotNull(result.Failure);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Contains(_loggerFactory.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("invalid API key", StringComparison.Ordinal));
        Assert.DoesNotContain(_loggerFactory.Entries, e => e.Message.Contains(WrongKey, StringComparison.Ordinal) || e.Message.Contains(ExpectedKey, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnyKey_FailsWhenNoKeyIsConfigured()
    {
        var (handler, _) = await CreateAsync(WrongKey, ApiAuthenticationOptions.InternalWeb, configuredKey: string.Empty);

        var result = await handler.AuthenticateAsync();

        Assert.NotNull(result.Failure);
    }

    [Fact]
    public async Task Forbid_Returns403AndLogsWithoutTheClientAppValue()
    {
        const string clientApp = "UnknownApp";
        var (handler, context) = await CreateAsync(ExpectedKey, clientApp);

        await handler.ForbidAsync(new AuthenticationProperties());

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Contains(_loggerFactory.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("missing or invalid client app", StringComparison.Ordinal));
        Assert.DoesNotContain(_loggerFactory.Entries, e => e.Message.Contains(clientApp, StringComparison.Ordinal));
    }

    private sealed class StaticOptionsMonitor : IOptionsMonitor<AuthenticationSchemeOptions>
    {
        public AuthenticationSchemeOptions CurrentValue { get; } = new();

        public AuthenticationSchemeOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<AuthenticationSchemeOptions, string?> listener) => null;
    }

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed class CapturingLoggerFactory : ILoggerFactory, ILogger
    {
        public List<LogEntry> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => this;

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public void Dispose()
        {
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
    }
}
