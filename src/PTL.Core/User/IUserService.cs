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
}
