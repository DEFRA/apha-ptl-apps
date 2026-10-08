namespace PTL.Core.User;

public interface IUserRepository
{
    // spgaUser
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default);

    // spiUser
    Task CreateAsync(User user, CancellationToken cancellationToken = default);
}
