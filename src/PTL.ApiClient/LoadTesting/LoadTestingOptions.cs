using PTL.Common.LoadTesting;

namespace PTL.ApiClient;

/// <summary>Temporary switch that opens a sign-in bypassing the identity provider so JMeter can sign in; set only in the performance environment.</summary>
public sealed class LoadTestingOptions
{
    public const string SectionName = "LoadTesting";

    /// <summary>Gets or sets a value indicating whether the load-testing sign-in is available (env var <c>LoadTesting__Enabled</c>).</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the last day (UTC) the sign-in can be used, even when <see cref="Enabled"/> (<c>LoadTesting:ExpiresOn</c>).</summary>
    public DateOnly? ExpiresOn { get; set; }

    public bool IsAvailable(TimeProvider timeProvider) => Enabled && LoadTestingExpiry.IsOpen(ExpiresOn, timeProvider);
}
