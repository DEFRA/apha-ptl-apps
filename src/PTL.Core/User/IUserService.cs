namespace PTL.Core.User;

public interface IUserService
{
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default);

    // Filters out any candidate already linked to a PT-LIMS account - matches legacy
    // GetADList's mUserList.ContainsByName(fullname) check, and is what makes a search where
    // every match already has an account collapse to an empty result set.
    Task<IReadOnlyList<StaffDirectoryUser>> SearchStaffDirectoryAsync(string searchTerm, CancellationToken cancellationToken = default);

    // Throws UserValidationException when business rules are violated (no email, duplicate account).
    Task<User> CreateUserAsync(string username, string email, string friendlyName, string firstName, string lastName, string department, CancellationToken cancellationToken = default);

    // The Internal Test Consultant Department Management grid (spgaUserTestConsultant).
    Task<IReadOnlyList<User>> GetTestConsultantsAsync(CancellationToken cancellationToken = default);

    // Updates Department/IsInactive/InactiveDate together (spuUserDept) - the caller (InternalWeb)
    // decides the InactiveDate value, since that's purely a UI round-trip concern, not a domain rule.
    Task UpdateTestConsultantAsync(Guid userId, string department, bool isInactive, DateTime? inactiveDate, CancellationToken cancellationToken = default);
}
