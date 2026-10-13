namespace PTL.Core.User;

public interface IUserRepository
{
    // spgaUser
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default);

    // spiUser
    Task CreateAsync(User user, CancellationToken cancellationToken = default);

    // spdUser
    Task DeleteAsync(Guid userId, CancellationToken cancellationToken = default);

    // spgaUserTestConsultant - the Internal Test Consultant Department Management grid.
    Task<IReadOnlyList<User>> GetTestConsultantsAsync(CancellationToken cancellationToken = default);

    // spuUserDept - updates Department/IsInactive/InactiveDate together, matching the stored
    // procedure's own parameters exactly.
    Task UpdateDepartmentAsync(Guid userId, string department, bool isInactive, DateTime? inactiveDate, CancellationToken cancellationToken = default);
}
