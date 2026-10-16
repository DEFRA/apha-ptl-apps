namespace PTL.Core.Configuration;

/// <summary>Public base URL of PTL.ExternalWeb, used for links to the external portal in outbound emails.</summary>
public sealed class ExternalOptions
{
    public const string SectionName = "External";

    public string AppUrl { get; set; } = string.Empty;
}
