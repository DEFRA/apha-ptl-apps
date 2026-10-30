using PTL.Core.User;

namespace PTL.InternalWeb.Tests.TestSupport;

// Test double for IStaffDirectoryService so Create User tests don't call Microsoft Graph.
internal sealed class FakeStaffDirectoryService : IStaffDirectoryService
{
    public IReadOnlyList<StaffDirectoryUser> Results { get; set; } = [];
    public bool IsTruncated { get; set; }
    public Exception? Exception { get; set; }
    public string? LastSearchTerm { get; private set; }
    public int SearchCallCount { get; private set; }

    public Task<StaffDirectorySearchResult> SearchAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        LastSearchTerm = searchTerm;
        SearchCallCount++;
        return Exception is null
            ? Task.FromResult(new StaffDirectorySearchResult(Results, IsTruncated))
            : Task.FromException<StaffDirectorySearchResult>(Exception);
    }
}
