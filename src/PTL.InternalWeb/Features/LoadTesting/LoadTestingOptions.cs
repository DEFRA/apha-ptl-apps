namespace PTL.InternalWeb.Features.LoadTesting;

/// <summary>Temporary switch that opens a username-only sign-in so JMeter can bypass Entra ID; set only in the performance environment.</summary>
public sealed class LoadTestingOptions
{
    public const string SectionName = "LoadTesting";

    /// <summary>Gets or sets a value indicating whether the load-testing sign-in is available (env var <c>LoadTesting__Enabled</c>).</summary>
    public bool Enabled { get; set; }
}
