using Microsoft.Extensions.Options;

namespace PTL.Auth.Cidm.Options;

public sealed class CidmOptionsValidator : IValidateOptions<CidmOptions>
{
    public ValidateOptionsResult Validate(string? name, CidmOptions options)
    {
        var failures = new List<string>();

        RequireNotEmpty(options.Address, nameof(CidmOptions.Address), failures);
        RequireNotEmpty(options.Policy, nameof(CidmOptions.Policy), failures);
        RequireNotEmpty(options.ClientId, nameof(CidmOptions.ClientId), failures);
        RequireNotEmpty(options.ClientSecret, nameof(CidmOptions.ClientSecret), failures);
        RequireNotEmpty(options.ServiceId, nameof(CidmOptions.ServiceId), failures);
        RequireNotEmpty(options.CallbackPath, nameof(CidmOptions.CallbackPath), failures);
        RequireNotEmpty(options.SignedOutCallbackPath, nameof(CidmOptions.SignedOutCallbackPath), failures);

        if (!string.IsNullOrWhiteSpace(options.Address) &&
            (!Uri.TryCreate(options.Address, UriKind.Absolute, out var addressUri) || addressUri.Scheme != Uri.UriSchemeHttps))
        {
            failures.Add($"{nameof(CidmOptions.Address)} must be an absolute https:// URL.");
        }

        if (options.Scopes is null || options.Scopes.Count == 0 || !options.Scopes.Contains("openid"))
        {
            failures.Add($"{nameof(CidmOptions.Scopes)} must include at least 'openid'.");
        }

        if (options.RefreshBeforeExpiry <= TimeSpan.Zero)
        {
            failures.Add($"{nameof(CidmOptions.RefreshBeforeExpiry)} must be a positive duration.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void RequireNotEmpty(string value, string propertyName, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures.Add($"{nameof(CidmOptions)}.{propertyName} is required.");
        }
    }
}
