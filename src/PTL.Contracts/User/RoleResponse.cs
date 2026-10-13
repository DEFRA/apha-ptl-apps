namespace PTL.Contracts.User;

// GET /api/roles - the fixed list of internal PT-LIMS application roles (Admin, Scheme Admin,
// Test Consultant, Viewer, etc.), ordered per tblRoles.fldOrder.
public sealed record RoleResponse(Guid RoleId, string Name);
