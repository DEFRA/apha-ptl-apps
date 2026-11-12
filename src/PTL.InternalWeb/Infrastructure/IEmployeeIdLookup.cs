namespace PTL.InternalWeb.Infrastructure;

/// <summary>Reads a signed-in user's employee id from the directory.</summary>
public interface IEmployeeIdLookup
{
    /// <summary>Finds the employee id of a user from their Entra ID object ID (the <c>oid</c> token claim).</summary>
    /// <param name="objectId">The Entra ID object ID.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The employee id, or <see langword="null"/> when the user does not exist or has none.</returns>
    /// <exception cref="PTL.Common.Graph.GraphException">The Graph call failed.</exception>
    Task<string?> FindEmployeeIdAsync(Guid objectId, CancellationToken cancellationToken);
}
