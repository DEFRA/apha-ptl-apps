namespace PTL.Core.User;

public interface IUserService
{
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default);

    // Throws UserValidationException when business rules are violated (no email, over-long values, duplicate account).
    Task<User> CreateUserAsync(NewUserDetails details, Guid? actingUserId = null, CancellationToken cancellationToken = default);

    // The Internal Test Consultant Department Management grid (spgaUserTestConsultant).
    Task<IReadOnlyList<User>> GetTestConsultantsAsync(CancellationToken cancellationToken = default);

    // Updates Department/IsInactive/InactiveDate together (spuUserDept) - the caller (InternalWeb)
    // decides the InactiveDate value, since that's purely a UI round-trip concern, not a domain rule.
    Task UpdateTestConsultantAsync(Guid userId, string department, bool isInactive, DateTime? inactiveDate, CancellationToken cancellationToken = default);
}
