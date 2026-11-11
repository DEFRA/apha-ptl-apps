using PTL.Contracts.ExternalUser;
using PTL.Core.ExternalUser;

namespace PTL.Api.Tests.ExternalUser;

// In-memory stand-in for vwExternalUser and its three procedures, including their rules: lookups are ordered
// by user type, and linking only ever touches rows of the type that carry the email and have no contact id yet.
internal sealed class FakeExternalUserRepository : IExternalUserRepository
{
    private readonly List<ExternalUserMatch> _rows = [];

    public int FindBySsoIdExtCalls { get; private set; }

    public int FindByEmailCalls { get; private set; }

    public int LinkCalls { get; private set; }

    // Set by a test that wants Link to find nothing, as when a concurrent sign-in claims the record first.
    public bool LinkFindsNothing { get; set; }

    public ExternalUserMatch Add(
        ExternalUserType userType,
        string? email = "user@example.com",
        Guid? ssoIdExt = null,
        bool isActive = true,
        Guid? userId = null,
        Guid? labParticipantId = null,
        string? labCode = null,
        string? labName = null)
    {
        var row = new ExternalUserMatch
        {
            UserType = userType,
            UserId = userId ?? Guid.NewGuid(),
            Email = email,
            SsoIdExt = ssoIdExt,
            IsActive = isActive,
            LabParticipantId = labParticipantId,
            LabCode = labCode,
            LabName = labName
        };
        _rows.Add(row);
        return row;
    }

    public IReadOnlyList<ExternalUserMatch> Rows => _rows;

    public Task<IReadOnlyList<ExternalUserMatch>> FindBySsoIdExtAsync(Guid ssoIdExt, CancellationToken cancellationToken = default)
    {
        FindBySsoIdExtCalls++;
        return Ordered(_rows.Where(row => row.SsoIdExt == ssoIdExt));
    }

    public Task<IReadOnlyList<ExternalUserMatch>> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        FindByEmailCalls++;
        return Ordered(_rows.Where(row => string.Equals(row.Email, email, StringComparison.OrdinalIgnoreCase)));
    }

    public Task<IReadOnlyList<ExternalUserMatch>> LinkAsync(ExternalUserType userType, string email, Guid ssoIdExt, CancellationToken cancellationToken = default)
    {
        LinkCalls++;
        if (LinkFindsNothing)
        {
            return Ordered([]);
        }

        for (var index = 0; index < _rows.Count; index++)
        {
            var row = _rows[index];
            if (row.UserType == userType && row.SsoIdExt is null && string.Equals(row.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                _rows[index] = row with { SsoIdExt = ssoIdExt };
            }
        }

        return Ordered(_rows.Where(row => row.SsoIdExt == ssoIdExt));
    }

    private static Task<IReadOnlyList<ExternalUserMatch>> Ordered(IEnumerable<ExternalUserMatch> rows) =>
        Task.FromResult<IReadOnlyList<ExternalUserMatch>>(rows.OrderBy(row => row.UserType).ToList());
}
