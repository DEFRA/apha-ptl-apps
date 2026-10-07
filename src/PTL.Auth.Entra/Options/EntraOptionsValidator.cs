using Microsoft.Extensions.Options;

namespace PTL.Auth.Entra.Options;

/// <summary>Fails fast at startup when required <see cref="EntraOptions"/> values are missing or malformed.</summary>
public sealed class EntraOptionsValidator : IValidateOptions<EntraOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, EntraOptions options)
    {
        var failures = new List<string>();

        RequireNotEmpty(options.TenantId, nameof(EntraOptions.TenantId), failures);
        RequireNotEmpty(options.ClientId, nameof(EntraOptions.ClientId), failures);
        RequireNotEmpty(options.ClientSecret, nameof(EntraOptions.ClientSecret), failures);
        RequireNotEmpty(options.CallbackPath, nameof(EntraOptions.CallbackPath), failures);
        RequireNotEmpty(options.SignedOutCallbackPath, nameof(EntraOptions.SignedOutCallbackPath), failures);
        RequireNotEmpty(options.FrontChannelLogoutPath, nameof(EntraOptions.FrontChannelLogoutPath), failures);

        if (options.Scopes is null || options.Scopes.Count == 0 || !options.Scopes.Contains("openid"))
        {
            failures.Add($"{nameof(EntraOptions.Scopes)} must include at least 'openid'.");
        }

        if (options.RefreshBeforeExpiry <= TimeSpan.Zero)
        {
            failures.Add($"{nameof(EntraOptions.RefreshBeforeExpiry)} must be a positive duration.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void RequireNotEmpty(string value, string propertyName, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures.Add($"{nameof(EntraOptions)}.{propertyName} is required.");
        }
    }
}
