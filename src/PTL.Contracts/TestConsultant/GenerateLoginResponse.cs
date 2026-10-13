namespace PTL.Contracts.TestConsultant;

// [NEEDS INVESTIGATION] See IExternalLoginService remarks - Generate Login's real behaviour is
// subject to a business decision (CIDM/GOV.UK One Login may replace it with a "Send Invitation"
// capability). Success = false (with Message populated) is a legitimate outcome, not a malformed
// request, so this is returned with 200 OK rather than 400/500.
public sealed record GenerateLoginResponse(bool Success, string? Message);
