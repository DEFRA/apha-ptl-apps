using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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

    [Fact]
    public async Task HandleAsync_Refused_LogsTheInternalUserIdAndRequestPath()
    {
        var logger = new RecordingLogger();
        var handler = new LoggingAuthorizationMiddlewareResultHandler(logger);
        var context = new DefaultHttpContext();
        context.Request.Path = "/SystemAdministration/CreateUser";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(InternalUserClaimTypes.InternalUserId, "11111111-1111-1111-1111-111111111111")], "Test"));
        var policy = new AuthorizationPolicyBuilder().RequireAssertion(_ => false).Build();
        var authorizeResult = PolicyAuthorizationResult.Forbid();

        try
        {
            // The real default handler needs a fully configured auth pipeline to actually
            // Forbid/Challenge - irrelevant here, this test only verifies the NEW logging
            // side-effect runs before delegation is attempted.
            await handler.HandleAsync(_ => Task.CompletedTask, context, policy, authorizeResult);
        }
        catch (InvalidOperationException)
        {
        }

        Assert.Contains(logger.Messages, m =>
            m.Contains("11111111-1111-1111-1111-111111111111", StringComparison.Ordinal) &&
            m.Contains("/SystemAdministration/CreateUser", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HandleAsync_Refused_AnonymousUser_LogsAsAnonymous()
    {
        var logger = new RecordingLogger();
        var handler = new LoggingAuthorizationMiddlewareResultHandler(logger);
        var context = new DefaultHttpContext();
        context.Request.Path = "/SystemAdministration/CreateUser";
        var policy = new AuthorizationPolicyBuilder().RequireAssertion(_ => false).Build();
        var authorizeResult = PolicyAuthorizationResult.Forbid();

        try
        {
            await handler.HandleAsync(_ => Task.CompletedTask, context, policy, authorizeResult);
        }
        catch (InvalidOperationException)
        {
        }

        Assert.Contains(logger.Messages, m => m.Contains("anonymous", StringComparison.Ordinal));
    }
}
