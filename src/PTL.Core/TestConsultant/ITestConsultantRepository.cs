namespace PTL.Core.TestConsultant;

public interface ITestConsultantRepository
{
    /// <summary>Reads the test consultant row matching a CIDM contact id via <c>spgTestConsultantBySsoId</c>.</summary>
    Task<TestConsultant?> GetBySsoIdExtAsync(Guid ssoIdExt, CancellationToken cancellationToken = default);

    /// <summary>Reads the test consultant row matching an email address via <c>spgTestConsultantByEmail</c>, used only as a CIDM fallback.</summary>
    Task<TestConsultant?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<TestConsultant> CreateAsync(TestConsultant testConsultant, CancellationToken cancellationToken = default);
    Task<TestConsultant?> UpdateAsync(TestConsultant testConsultant, CancellationToken cancellationToken = default);
}
