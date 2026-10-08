namespace PTL.Core.InternalUser;

/// <summary>Outcome of resolving an Entra ID-authenticated internal user against tblUsers.</summary>
public sealed record InternalUserResolutionResult
{
    public required bool IsPermitted { get; init; }
    public InternalUser? User { get; init; }

    public static InternalUserResolutionResult Permitted(InternalUser user) => new() { IsPermitted = true, User = user };

    public static InternalUserResolutionResult Denied() => new() { IsPermitted = false };
}
