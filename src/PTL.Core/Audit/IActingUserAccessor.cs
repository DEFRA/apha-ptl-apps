namespace PTL.Core.Audit;

// Who is making the current System Administration change, for the audit trail. Null when the
// caller did not identify itself - the change is still audited, just without an acting user.
public interface IActingUserAccessor
{
    Guid? ActingUserId { get; }
}
