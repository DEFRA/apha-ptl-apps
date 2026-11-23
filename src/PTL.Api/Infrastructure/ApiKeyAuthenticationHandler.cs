using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using PTL.Common.ApiAuthentication;

namespace PTL.Api.Infrastructure;

/// <summary>
/// Authenticates the calling web front-end from the <c>X-Api-Key</c> header and records its
/// <c>X-Client-App</c> header as a claim; the fallback authorization policy then restricts that
/// claim to the known front-ends. A missing or wrong key is a 401, a missing or unknown client app
/// a 403. Header values are never logged.
/// </summary>
internal sealed partial class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<ApiAuthenticationOptions> apiAuthenticationOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";

    public const string ClientAppClaimType = "client_app";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Always compare, so only the key check itself decides whether the request is authenticated.
        var expectedKey = apiAuthenticationOptions.Value.ApiKey;
        _ = TryGetHeader(ApiAuthenticationOptions.ApiKeyHeaderName, out var providedKey);

        if (KeysMatch(providedKey, expectedKey))
        {
            var identity = new ClaimsIdentity(SchemeName);
            if (TryGetHeader(ApiAuthenticationOptions.ClientAppHeaderName, out var clientApp))
            {
                identity.AddClaim(new Claim(ClientAppClaimType, clientApp));
            }

            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }

        // No key is NoResult, not Fail, so the anonymous health endpoints are unaffected; the challenge below logs it.
        if (providedKey.Length == 0)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        LogInvalidApiKey(Logger);
        return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        if (!TryGetHeader(ApiAuthenticationOptions.ApiKeyHeaderName, out _))
        {
            LogMissingApiKey(Logger);
        }

        return base.HandleChallengeAsync(properties);
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        LogInvalidClientApp(Logger);
        return base.HandleForbiddenAsync(properties);
    }

    private bool TryGetHeader(string name, out string value)
    {
        var values = Request.Headers[name];
        value = values.ToString();
        return !StringValues.IsNullOrEmpty(values) && value.Length > 0;
    }

    // Hashing first gives both sides the same length, so the comparison time reveals nothing about the key.
    private static bool KeysMatch(string provided, string expected) =>
        !string.IsNullOrEmpty(expected)
        && CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(provided)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)));

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "API request rejected: missing API key.")]
    private static partial void LogMissingApiKey(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "API request rejected: invalid API key.")]
    private static partial void LogInvalidApiKey(ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "API request rejected: missing or invalid client app.")]
    private static partial void LogInvalidClientApp(ILogger logger);
}
