using PTL.Core.Audit;

namespace PTL.Core.User;

public sealed class UserService(IUserRepository userRepository, IStaffDirectoryService staffDirectoryService, IActingUserAccessor actingUser) : IUserService
{
    private const string UserEntityType = "User";

    public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) =>
        userRepository.GetAllAsync(cancellationToken);

    public async Task<StaffDirectorySearchResult> SearchStaffDirectoryAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        var candidates = await staffDirectoryService.SearchAsync(searchTerm, cancellationToken);
        var existingUsernames = (await userRepository.GetAllAsync(cancellationToken))
            .Select(u => u.Username)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new StaffDirectorySearchResult(
            candidates.Users.Where(c => !existingUsernames.Contains(c.Username)).ToList(),
            candidates.IsTruncated);
    }

    public async Task<User> CreateUserAsync(NewUserDetails details, Guid? actingUserId = null, CancellationToken cancellationToken = default)
    {
        var deptValue = details.Department.Trim();
        var validation = UserValidator.Validate(details);
        if (!validation.IsValid)
        {
            throw new UserValidationException(validation.Errors);
        }

        var existing = await userRepository.GetAllAsync(cancellationToken);
        if (existing.Any(u => string.Equals(u.Username, details.Username, StringComparison.OrdinalIgnoreCase)))
        {
            throw new UserValidationException([new UserValidationError(string.Empty, UserValidator.DuplicateMessage)]);
        }

        var newUser = new User
        {
            UserId = Guid.NewGuid(),
            Username = details.Username,
            Email = details.Email,
            FriendlyName = details.FriendlyName,
            FirstName = details.FirstName,
            LastName = details.LastName,
            Department = deptValue,
            IsInactive = false,
            InactiveDate = null
        };

        // Audited in the SAME transaction as the insert - if the audit write fails, the account
        // itself is never persisted either (see IUserRepository.CreateWithAuditAsync remarks).
        var auditEntry = new AuditEntry(actingUserId, "CreateUser", UserEntityType, newUser.UserId.ToString(), $"Username: {details.Username}");
        await userRepository.CreateWithAuditAsync(newUser, auditEntry, cancellationToken);
        return newUser;
    }

    public Task<IReadOnlyList<User>> GetTestConsultantsAsync(CancellationToken cancellationToken = default) =>
        userRepository.GetTestConsultantsAsync(cancellationToken);

    public async Task UpdateTestConsultantAsync(Guid userId, string department, bool isInactive, DateTime? inactiveDate, CancellationToken cancellationToken = default)
    {
        var auditEntry = new AuditEntry(actingUser.ActingUserId, "UpdateInternalTestConsultant", UserEntityType, userId.ToString(), $"Department: {department}; Inactive: {isInactive}");
        await userRepository.UpdateDepartmentAsync(userId, department, isInactive, inactiveDate, auditEntry, cancellationToken);
    }
}
