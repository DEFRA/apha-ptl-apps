namespace PTL.Core.User;

public interface IRoleRepository
{
    // spgaRole
    Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default);
}
