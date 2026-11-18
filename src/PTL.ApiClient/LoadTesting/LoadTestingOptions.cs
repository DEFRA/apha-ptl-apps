namespace PTL.ApiClient;

/// <summary>Temporary switch that opens a sign-in bypassing the identity provider so JMeter can sign in; set only in the performance environment.</summary>
public sealed class LoadTestingOptions
{
    public const string SectionName = "LoadTesting";

    /// <summary>Gets or sets a value indicating whether the load-testing sign-in is available (env var <c>LoadTesting__Enabled</c>).</summary>
    public bool Enabled { get; set; }
}
