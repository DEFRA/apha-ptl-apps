namespace PTL.ExternalWeb.Features.Home;

// Legacy About.aspx: the application version and the environment it runs in.
public sealed record SystemInformationViewModel(string Version, string Environment);
