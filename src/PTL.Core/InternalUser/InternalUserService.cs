namespace PTL.Core.InternalUser;

public sealed class InternalUserService(IInternalUserRepository internalUserRepository) : IInternalUserService
{
    private const string InternalUserRole = "Internal User";
    private const string SchedulingRole = "Scheduling";
    private const string DistributionsRole = "Distributions";

    public async Task<InternalUserResolutionResult> ResolveAsync(
        Guid ssoIdInt,
        string username,
        CancellationToken cancellationToken = default)
    {
        var user = await internalUserRepository.GetBySsoIdIntAsync(ssoIdInt, cancellationToken);
        if (user is null)
        {
            user = await internalUserRepository.GetByUsernameAsync(username, cancellationToken);
            if (user is null)
            {
                return InternalUserResolutionResult.Denied();
            }

            await internalUserRepository.UpdateSsoIdIntAsync(user.UserId, ssoIdInt, cancellationToken);
            user.SsoIdInt = ssoIdInt;
        }

        user.Roles = WithSyntheticDistributionsRole(user.Roles);

        return InternalUserResolutionResult.Permitted(user);
    }

    // Legacy parity: CustomIdentity.vb adds a synthetic "Distributions" role (not a real tblRoles
    // row) whenever the user holds "Internal User" or "Scheduling".
    private static IReadOnlyList<string> WithSyntheticDistributionsRole(IReadOnlyList<string> roles)
    {
        if (roles.Contains(DistributionsRole) ||
            (!roles.Contains(InternalUserRole) && !roles.Contains(SchedulingRole)))
        {
            return roles;
        }

        return [.. roles, DistributionsRole];
    }
}
