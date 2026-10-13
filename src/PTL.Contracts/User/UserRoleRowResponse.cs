namespace PTL.Contracts.User;

// One row of the Manage User Roles grid - a user plus the set of role ids currently assigned to them.
public sealed record UserRoleRowResponse(Guid UserId, string Username, string FriendlyName, IReadOnlyList<Guid> RoleIds);
