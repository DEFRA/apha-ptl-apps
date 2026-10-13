namespace PTL.Core.TestConsultant;

public interface ITestConsultantService
{
    Task<IReadOnlyList<TestConsultant>> GetAllAsync(CancellationToken cancellationToken = default);

    // Throws ExternalTestConsultantValidationException when Name/Email are missing or Email is
    // malformed (Department is optional, per the story's Business Rule).
    Task<TestConsultant> CreateAsync(string name, string department, string email, CancellationToken cancellationToken = default);

    // Returns null if externalTestConsultantId doesn't exist. Preserves the existing
    // SsoId/IsInactive/InactiveDate - this endpoint only ever changes Name/Department/Email.
    Task<TestConsultant?> UpdateAsync(Guid externalTestConsultantId, string name, string department, string email, CancellationToken cancellationToken = default);

    // Toggles Active/Inactive. Stamps InactiveDate with now when becoming inactive; clears it
    // when becoming active. Returns null if externalTestConsultantId doesn't exist.
    Task<TestConsultant?> SetStatusAsync(Guid externalTestConsultantId, bool isInactive, CancellationToken cancellationToken = default);

    // [NEEDS INVESTIGATION] See IExternalLoginService remarks. Returns (false, message) if the
    // consultant doesn't exist or the stub reports failure - a legitimate outcome, not an error.
    Task<(bool Success, string? Message)> GenerateLoginAsync(Guid externalTestConsultantId, CancellationToken cancellationToken = default);
}
