namespace PTL.Core.TestConsultant;

// [NEEDS INVESTIGATION] Stub only. Legacy's GenerateLogin.Execute created an account in the
// on-premises VLA SSO system via a SOAP web service, then queued a mail-merge "welcome email".
// Per the story's Developer Note, this whole mechanism's future is subject to a business
// decision - CIDM/GOV.UK One Login may replace it with a "Send Invitation" capability instead.
// Until that decision is made, this always "succeeds" without creating any real external
// account or sending any real email - do not wire up a real SSO or email integration here.
public interface IExternalLoginService
{
    Task<bool> GenerateLoginAsync(Guid externalTestConsultantId, string name, string email, CancellationToken cancellationToken = default);
}

public sealed class StubExternalLoginService : IExternalLoginService
{
    public Task<bool> GenerateLoginAsync(Guid externalTestConsultantId, string name, string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);
}
