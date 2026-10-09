namespace PTL.Core.User;

public sealed class UserService(IUserRepository userRepository, IStaffDirectoryService staffDirectoryService) : IUserService
{
    public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) =>
        userRepository.GetAllAsync(cancellationToken);

    public async Task<IReadOnlyList<StaffDirectoryUser>> SearchStaffDirectoryAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        var candidates = await staffDirectoryService.SearchAsync(searchTerm, cancellationToken);
        var existingUsernames = (await userRepository.GetAllAsync(cancellationToken))
            .Select(u => u.Username)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return candidates.Where(c => !existingUsernames.Contains(c.Username)).ToList();
    }

    public async Task<User> CreateUserAsync(string username, string email, string friendlyName, string firstName, string lastName, string department, CancellationToken cancellationToken = default)
    {
        var deptValue = department.Trim();
        var validation = UserValidator.Validate(email, deptValue);
        if (!validation.IsValid)
        {
            throw new UserValidationException(validation.Errors);
        }

        var existing = await userRepository.GetAllAsync(cancellationToken);
        if (existing.Any(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)))
        {
            throw new UserValidationException([new UserValidationError(string.Empty, UserValidator.DuplicateMessage)]);
        }

        var newUser = new User
        {
            UserId = Guid.NewGuid(),
            Username = username,
            Email = email,
            FriendlyName = friendlyName,
            FirstName = firstName,
            LastName = lastName,
            Department = deptValue,
            IsInactive = false,
            InactiveDate = null
        };

        await userRepository.CreateAsync(newUser, cancellationToken);
        return newUser;
    }

    public Task<IReadOnlyList<User>> GetTestConsultantsAsync(CancellationToken cancellationToken = default) =>
        userRepository.GetTestConsultantsAsync(cancellationToken);

    public Task UpdateTestConsultantAsync(Guid userId, string department, bool isInactive, DateTime? inactiveDate, CancellationToken cancellationToken = default) =>
        userRepository.UpdateDepartmentAsync(userId, department, isInactive, inactiveDate, cancellationToken);
}
