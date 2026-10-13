using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PTL.InternalWeb.Authorization;
using PTL.InternalWeb.Features.Account;

namespace PTL.InternalWeb.Tests.Authorization;

// System Administration business rule: a refused request must be logged, not just refused.
public class LoggingAuthorizationMiddlewareResultHandlerTests
{
    private sealed class RecordingLogger : ILogger<LoggingAuthorizationMiddlewareResultHandler>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }

    // No-op stand-in so the real default handler's Forbid call can complete without a fully
    // configured ASP.NET Core auth pipeline - this test only cares that the NEW logging
    // side-effect ran, not what the default handler does with the result afterwards.
    private sealed class NoOpAuthenticationService : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => Task.FromResult(AuthenticateResult.NoResult());
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }

    private static DefaultHttpContext CreateContext()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuthenticationService>(new NoOpAuthenticationService());
        return new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
    }

    [Fact]
    public async Task HandleAsync_Refused_LogsTheInternalUserIdAndRequestPath()
    {
        var logger = new RecordingLogger();
        var handler = new LoggingAuthorizationMiddlewareResultHandler(logger);
        var context = CreateContext();
        context.Request.Path = "/SystemAdministration/CreateUser";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(InternalUserClaimTypes.InternalUserId, "11111111-1111-1111-1111-111111111111")], "Test"));
        var policy = new AuthorizationPolicyBuilder().RequireAssertion(_ => false).Build();
        var authorizeResult = PolicyAuthorizationResult.Forbid();

        await handler.HandleAsync(_ => Task.CompletedTask, context, policy, authorizeResult);

        Assert.Contains(logger.Messages, m =>
            m.Contains("11111111-1111-1111-1111-111111111111", StringComparison.Ordinal) &&
            m.Contains("/SystemAdministration/CreateUser", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HandleAsync_Refused_AnonymousUser_LogsAsAnonymous()
    {
        var logger = new RecordingLogger();
        var handler = new LoggingAuthorizationMiddlewareResultHandler(logger);
        var context = CreateContext();
        context.Request.Path = "/SystemAdministration/CreateUser";
        var policy = new AuthorizationPolicyBuilder().RequireAssertion(_ => false).Build();
        var authorizeResult = PolicyAuthorizationResult.Forbid();

        await handler.HandleAsync(_ => Task.CompletedTask, context, policy, authorizeResult);

        Assert.Contains(logger.Messages, m => m.Contains("anonymous", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HandleAsync_Succeeded_DoesNotLog()
    {
        var logger = new RecordingLogger();
        var handler = new LoggingAuthorizationMiddlewareResultHandler(logger);
        var context = CreateContext();
        var policy = new AuthorizationPolicyBuilder().RequireAssertion(_ => true).Build();
        var authorizeResult = PolicyAuthorizationResult.Success();
        var nextCalled = false;

        await handler.HandleAsync(_ => { nextCalled = true; return Task.CompletedTask; }, context, policy, authorizeResult);

        Assert.Empty(logger.Messages);
        Assert.True(nextCalled);
    }
}
