namespace PTL.Core.User;

// [NEEDS INVESTIGATION] Stub only, backed by an in-memory sample list - not a real directory
// integration. Wire this up to Microsoft Graph (or whatever the identity migration workstream
// decides) once that decision is made, then delete this class. Matches legacy's search shape
// (matches on login/forename/surname) but against fixed sample data instead of a live directory.
public sealed class StubStaffDirectoryService : IStaffDirectoryService
{
    private static readonly IReadOnlyList<StaffDirectoryUser> SampleDirectory =
    [
        new() { Username = "m100001", Email = "jane.smith@apha.gov.uk", FriendlyName = "Jane Smith", FirstName = "Jane", LastName = "Smith" },
        new() { Username = "m100002", Email = "john.doe@apha.gov.uk", FriendlyName = "John Doe", FirstName = "John", LastName = "Doe" },
        new() { Username = "m100003", Email = string.Empty, FriendlyName = "Sam Taylor", FirstName = "Sam", LastName = "Taylor" }
    ];

    public Task<IReadOnlyList<StaffDirectoryUser>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        var term = searchTerm.Trim();
        var matches = SampleDirectory
            .Where(u =>
                u.Username.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                u.FirstName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                u.LastName.Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return Task.FromResult<IReadOnlyList<StaffDirectoryUser>>(matches);
    }
}
