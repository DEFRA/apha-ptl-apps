namespace PTL.ExternalWeb.Features.LoadTesting;

/// <summary>Temporary switch that opens an email-only sign-in so JMeter can bypass CIDM; set only in the performance environment.</summary>
public sealed class LoadTestingOptions
{
    public const string SectionName = "LoadTesting";

    /// <summary>Gets or sets a value indicating whether the load-testing sign-in is available (env var <c>LoadTesting__Enabled</c>).</summary>
    public bool Enabled { get; set; }
}
