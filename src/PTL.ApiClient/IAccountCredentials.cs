namespace PTL.ApiClient;

/// <summary>Minimal shape <see cref="PtlAccountControllerBase"/> needs from each app's own login view model.</summary>
public interface IAccountCredentials
{
    string? Username { get; }

    string? Password { get; }

    string? ReturnUrl { get; }
}
