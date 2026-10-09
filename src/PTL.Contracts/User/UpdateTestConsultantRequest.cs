namespace PTL.Contracts.User;

// Mirrors legacy spuUserDept's own parameters exactly - it updates Department/IsInactive/
// InactiveDate together in one UPDATE statement, so all three must always be supplied.
public sealed record UpdateTestConsultantRequest(string Department, bool IsInactive, DateTime? InactiveDate);
