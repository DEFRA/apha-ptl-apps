using System.Net.Http.Json;
using System.Text.Json.Serialization;
using PTL.Common.Graph;
using PTL.Core.User;

namespace PTL.InternalWeb.Infrastructure;

/// <summary>
/// Staff directory search backed by Microsoft Graph (replaces legacy UserCreate.aspx's LDAP DirectorySearcher).
/// Like legacy, the search text is split on spaces and backslashes and each piece is matched against the account
/// name, first name, surname and display name (legacy's samaccountname, givenname, sn and cn) - here as a prefix
/// match. The username is built as "domain\account", lowercased, with the domain cut at its first '.', exactly as
/// legacy and the sign-in resolver build it. Like legacy, every match is returned (Graph's pages are followed), up
/// to a safety cap; when the cap is reached the result is flagged as truncated so the caller can say so.
/// </summary>
public sealed class GraphStaffDirectoryService(IHttpClientFactory httpClientFactory) : IStaffDirectoryService
{
    private const int MaxTerms = 5;
    private const int MaxTermLength = 64;
    private const int PageSize = 100;
    private const int MaxPages = 10;
    private const int MaxResults = 500;
    private const string Select = "id,onPremisesSamAccountName,onPremisesDomainName,mail,givenName,surname,displayName,accountEnabled";

    public async Task<StaffDirectorySearchResult> SearchAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        var terms = ExtractTerms(searchTerm);
        if (terms.Count == 0)
        {
            return new StaffDirectorySearchResult([], false);
        }

        // Graph only allows filtering on onPremisesSamAccountName in advanced query mode: the ConsistencyLevel header
        // plus $count=true (the count itself is not read). Disabled accounts are removed in the query so they cannot
        // use up a page; users with no on-premises account name are dropped below.
        var firstUrl = $"users?$select={Select}&$top={PageSize}&$count=true&$filter={Uri.EscapeDataString(BuildFilter(terms))}";

        var client = httpClientFactory.CreateClient(GraphServiceCollectionExtensions.GraphHttpClientName);
        var users = new List<StaffDirectoryUser>();
        var nextUrl = firstUrl;

        for (var page = 0; page < MaxPages && nextUrl is not null && users.Count <= MaxResults; page++)
        {
            var payload = await GetPageAsync(client, nextUrl, cancellationToken);
            users.AddRange((payload?.Value ?? [])
                .Where(u => u.AccountEnabled != false && !string.IsNullOrWhiteSpace(u.OnPremisesSamAccountName))
                .Select(ToStaffDirectoryUser));
            nextUrl = payload?.NextLink;
        }

        var truncated = users.Count > MaxResults || nextUrl is not null;
        var sorted = users
            .OrderBy(u => u.Username, StringComparer.Ordinal)
            .Take(MaxResults)
            .ToList();
        return new StaffDirectorySearchResult(sorted, truncated);
    }

    private static async Task<GraphUserList?> GetPageAsync(HttpClient client, string url, CancellationToken cancellationToken)
    {
        // The Graph access token is attached to every request this client makes, so a next-page link is only followed
        // if it points back at the same Graph host.
        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute)
            && !string.Equals(absolute.GetLeftPart(UriPartial.Authority), client.BaseAddress?.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
        {
            throw new GraphException("Microsoft Graph returned a next-page link for an unexpected host.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("ConsistencyLevel", "eventual");
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new GraphException($"Microsoft Graph user search failed with HTTP {(int)response.StatusCode}.");
        }

        return await response.Content.ReadFromJsonAsync<GraphUserList>(cancellationToken);
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
        "("
        + string.Join(
            " or ",
            terms.Select(t => t.Replace("'", "''", StringComparison.Ordinal)).Select(t =>
                $"startsWith(onPremisesSamAccountName,'{t}') or startsWith(givenName,'{t}') or startsWith(surname,'{t}') or startsWith(displayName,'{t}')"))
        + ") and accountEnabled eq true";

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

        [JsonPropertyName("@odata.nextLink")]
        public string? NextLink { get; set; }
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
