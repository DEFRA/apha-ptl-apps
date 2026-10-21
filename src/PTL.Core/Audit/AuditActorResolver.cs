using PTL.Core.User;

namespace PTL.Core.Audit;

// Who an audit row names: the signed-in internal user the web app forwards, by login name.
// Legacy used the CustomIdentity name and fell back to "Guest" when nobody was signed in.
public interface IAuditActorResolver
{
    Task<string> GetAuditNameAsync(CancellationToken cancellationToken = default);
}

public sealed class AuditActorResolver(IActingUserAccessor actingUser, IUserRepository userRepository) : IAuditActorResolver
{
    private const string Fallback = "Guest";
    private const int MaxLength = 50;

    public async Task<string> GetAuditNameAsync(CancellationToken cancellationToken = default)
    {
        if (actingUser.ActingUserId is not { } id)
        {
            return Fallback;
        }

        var users = await userRepository.GetAllAsync(cancellationToken);
        var name = users.FirstOrDefault(u => u.UserId == id)?.Username;
        if (string.IsNullOrWhiteSpace(name))
        {
            return Fallback;
        }

        return name.Length > MaxLength ? name[..MaxLength] : name;
    }
}
