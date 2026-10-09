namespace PTL.Core.Configuration;

/// <summary>
/// Application-level settings describing PTLIMS itself, as distinct from any external integration's
/// own configuration (GOV.UK Notify, S3, etc.). Currently just the public base URL of
/// PTL.InternalWeb, which PTL.Api needs to build links back into the web front-end (e.g. the
/// invoice-ready Notify email's download link) - the API cannot derive that address itself.
/// </summary>
public sealed class InternalOptions
{
    public const string SectionName = "Internal";

    public string AppUrl { get; set; } = string.Empty;
}
