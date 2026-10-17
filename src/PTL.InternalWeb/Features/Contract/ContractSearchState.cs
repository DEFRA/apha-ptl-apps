using System.Text.Json;
using PTL.Contracts.Contract;

namespace PTL.InternalWeb.Features.Contract;

// Remembers the last Contracts list search (period, year, term, paging) in a session cookie so the
// list reopens as the user left it when they come back from a contract, the breadcrumb or a Cancel
// button, none of which carry the query string.
internal static class ContractSearchState
{
    private const string CookieName = "ptl-contract-search";
    private const int MaxPageSize = 500;

    private static readonly string[] CriteriaKeys = ["yearId", "period", "searchTerm", "page", "pageSize"];

    // Any explicit criterion in the URL means the user (or a pagination link) chose the search.
    public static bool HasCriteria(HttpContext? context) =>
        context is not null && CriteriaKeys.Any(context.Request.Query.ContainsKey);

    public static void Save(HttpContext context, ContractSearchViewModel search) =>
        context.Response.Cookies.Append(CookieName, JsonSerializer.Serialize(search), BuildCookieOptions(context));

    // Only the search for this customer is restored, and only if the cookie still holds sane values.
    public static ContractSearchViewModel? TryRestore(HttpContext? context, Guid customerId)
    {
        if (context is null || !context.Request.Cookies.TryGetValue(CookieName, out var json))
        {
            return null;
        }

        try
        {
            var saved = JsonSerializer.Deserialize<ContractSearchViewModel>(json);
            return saved is not null
                && saved.CustomerId == customerId
                && Enum.IsDefined(saved.Period)
                && saved.Page >= 1
                && saved.PageSize is >= 1 and <= MaxPageSize
                    ? saved
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static void Clear(HttpContext? context)
    {
        if (context is not null)
        {
            context.Response.Cookies.Delete(CookieName, BuildCookieOptions(context));
        }
    }

    private static CookieOptions BuildCookieOptions(HttpContext context) => new()
    {
        HttpOnly = true,
        IsEssential = true,
        SameSite = SameSiteMode.Lax,
        Secure = true,
        Path = context.Request.PathBase.HasValue ? context.Request.PathBase.Value : "/",
    };
}
