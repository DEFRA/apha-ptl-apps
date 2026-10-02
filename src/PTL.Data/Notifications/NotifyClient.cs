using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PTL.Core.Notifications;

namespace PTL.Data.Notifications;

/// <summary>
/// Minimal GOV.UK Notify email-send client (docs/migration/email-notification-migration.md,
/// docs/migration/invoice-migration.md). Calls the public Notify REST API directly - no official
/// SDK dependency - authenticating with a short-lived HS256 JWT built from the configured API key,
/// exactly as GOV.UK Notify's own client libraries do. This is the one shared integration point
/// every feature's notifications go through; it is not invoice-specific.
/// </summary>
public sealed class NotifyClient(HttpClient httpClient, IOptions<NotifyOptions> options) : INotifyClient
{
    private readonly NotifyOptions _options = options.Value;

    public async Task SendEmailAsync(
        string templateId,
        string emailAddress,
        IReadOnlyDictionary<string, string>? personalisation = null,
        string? reference = null,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "v2/notifications/email")
        {
            Content = JsonContent.Create(new
            {
                email_address = emailAddress,
                template_id = templateId,
                personalisation,
                reference
            })
        };

        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", BuildToken());

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // GOV.UK Notify API keys are copied as "{name}-{serviceId guid}-{secret guid}" - the last 73
    // characters are always the two hyphenated GUIDs (36 + 1 + 36), regardless of how long the
    // leading name segment is.
    private string BuildToken()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || _options.ApiKey.Length < 73)
        {
            throw new InvalidOperationException($"Configuration value '{NotifyOptions.SectionName}:ApiKey' is required and must be a valid GOV.UK Notify API key.");
        }

        var keyPart = _options.ApiKey[^73..];
        var issuer = keyPart[..36];
        var secret = keyPart[37..];

        var header = Base64UrlEncode("""{"typ":"JWT","alg":"HS256"}"""u8.ToArray());
        var payload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new { iss = issuer, iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds() }));
        var unsigned = $"{header}.{payload}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var signature = Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(unsigned)));

        return $"{unsigned}.{signature}";
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
