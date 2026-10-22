using System.Net.Http.Json;
using System.Text.Json.Serialization;
using PTL.Common.Graph;
using PTL.Core.User;

namespace PTL.Api.Infrastructure;

/// <summary>
/// Staff directory search backed by Microsoft Graph (replaces legacy UserCreate.aspx's LDAP DirectorySearcher).
/// Like legacy, the search text is split on spaces and backslashes and each piece is matched against the account
/// name, first name, surname and display name (legacy's samaccountname, givenname, sn and cn) - here as a prefix
/// match. The username is built as "domain\account", lowercased, with the domain cut at its first '.', exactly as
/// legacy and the sign-in resolver build it.
/// </summary>
public sealed class GraphStaffDirectoryService(IHttpClientFactory httpClientFactory) : IStaffDirectoryService
{
    private const int MaxTerms = 5;
    private const int MaxTermLength = 64;
    private const int MaxResults = 50;
    private const string Select = "id,onPremisesSamAccountName,onPremisesDomainName,mail,givenName,surname,displayName,accountEnabled";

    public async Task<IReadOnlyList<StaffDirectoryUser>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        var terms = ExtractTerms(searchTerm);
        if (terms.Count == 0)
        {
            return [];
        }

        // Graph only allows filtering on onPremisesSamAccountName in advanced query mode: the ConsistencyLevel header
        // plus $count=true (the count itself is not read). Disabled accounts and users with no on-premises account name
        // are dropped below rather than in the query.
        var url = $"users?$select={Select}&$top=100&$count=true&$filter={Uri.EscapeDataString(BuildFilter(terms))}";

        var client = httpClientFactory.CreateClient(GraphServiceCollectionExtensions.GraphHttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("ConsistencyLevel", "eventual");
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new GraphException($"Microsoft Graph user search failed with HTTP {(int)response.StatusCode}.");
        }

        var payload = await response.Content.ReadFromJsonAsync<GraphUserList>(cancellationToken);
        return (payload?.Value ?? [])
            .Where(u => u.AccountEnabled != false && !string.IsNullOrWhiteSpace(u.OnPremisesSamAccountName))
            .Select(ToStaffDirectoryUser)
            .OrderBy(u => u.Username, StringComparer.Ordinal)
            .Take(MaxResults)
            .ToList();
    }

    private static List<string> ExtractTerms(string searchTerm) =>
        searchTerm
            .Split([' ', '\t', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.Length > MaxTermLength ? t[..MaxTermLength] : t)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxTerms)
            .ToList();

    // A single quote ends an OData string literal, so it is doubled; the whole filter is then URL-encoded.
    private static string BuildFilter(IEnumerable<string> terms) =>
        string.Join(
            " or ",
            terms.Select(t => t.Replace("'", "''", StringComparison.Ordinal)).Select(t =>
                $"startsWith(onPremisesSamAccountName,'{t}') or startsWith(givenName,'{t}') or startsWith(surname,'{t}') or startsWith(displayName,'{t}')"));

    private static StaffDirectoryUser ToStaffDirectoryUser(GraphUser user)
    {
        var domain = (user.OnPremisesDomainName ?? string.Empty).Split('.')[0];
        var account = user.OnPremisesSamAccountName!;
        var firstName = user.GivenName ?? string.Empty;
        var lastName = user.Surname ?? string.Empty;

        return new StaffDirectoryUser
        {
            Username = (domain.Length > 0 ? $"{domain}\\{account}" : account).ToLowerInvariant(),
            Email = user.Mail ?? string.Empty,
            FriendlyName = !string.IsNullOrWhiteSpace(user.DisplayName) ? user.DisplayName : $"{firstName} {lastName}".Trim(),
            FirstName = firstName,
            LastName = lastName
        };
    }

    private sealed class GraphUserList
    {
        [JsonPropertyName("value")]
        public List<GraphUser>? Value { get; set; }
    }

    private sealed class GraphUser
    {
        [JsonPropertyName("onPremisesSamAccountName")]
        public string? OnPremisesSamAccountName { get; set; }

        [JsonPropertyName("onPremisesDomainName")]
        public string? OnPremisesDomainName { get; set; }

        [JsonPropertyName("mail")]
        public string? Mail { get; set; }

        [JsonPropertyName("givenName")]
        public string? GivenName { get; set; }

        [JsonPropertyName("surname")]
        public string? Surname { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("accountEnabled")]
        public bool? AccountEnabled { get; set; }
    }
}
