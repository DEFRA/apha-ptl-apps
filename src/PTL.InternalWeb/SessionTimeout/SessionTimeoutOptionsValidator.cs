using Microsoft.Extensions.Options;

namespace PTL.InternalWeb.SessionTimeout;

/// <summary>Fails fast at startup when <see cref="SessionTimeoutOptions"/> values are missing or malformed.</summary>
public sealed class SessionTimeoutOptionsValidator : IValidateOptions<SessionTimeoutOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, SessionTimeoutOptions options)
    {
        var failures = new List<string>();

        if (options.InactivityWarningAfter <= TimeSpan.Zero)
        {
            failures.Add($"{nameof(SessionTimeoutOptions.InactivityWarningAfter)} must be a positive duration.");
        }

        if (options.ExpireAfterWarning <= TimeSpan.Zero)
        {
            failures.Add($"{nameof(SessionTimeoutOptions.ExpireAfterWarning)} must be a positive duration.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
