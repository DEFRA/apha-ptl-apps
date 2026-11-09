using PTL.Core.Audit;

namespace PTL.Core.TestConsultant;

public sealed class TestConsultantService(ITestConsultantRepository repository, IActingUserAccessor actingUser) : ITestConsultantService
{
    private const string EntityType = "ExternalTestConsultant";

    private AuditEntry Audit(string action, Guid id, string? details) =>
        new(actingUser.ActingUserId, action, EntityType, id.ToString(), details);

    public Task<IReadOnlyList<TestConsultant>> GetAllAsync(CancellationToken cancellationToken = default) =>
        repository.GetAllAsync(cancellationToken);

    public async Task<TestConsultant> CreateAsync(string name, string department, string email, CancellationToken cancellationToken = default)
    {
        Validate(name, email);
        await EnsureNameNotDuplicatedAsync(name, excludingId: null, cancellationToken);

        var newConsultant = new TestConsultant
        {
            ExternalTestConsultantId = Guid.NewGuid(),
            Name = name.Trim(),
            Department = department.Trim(),
            Email = email.Trim(),
            SsoId = Guid.Empty,
            IsInactive = false,
            InactiveDate = null
        };

        await repository.CreateAsync(newConsultant, Audit("CreateExternalTestConsultant", newConsultant.ExternalTestConsultantId, $"Name: {newConsultant.Name}"), cancellationToken);
        return newConsultant;
    }

    public async Task<TestConsultant?> UpdateAsync(Guid externalTestConsultantId, string name, string department, string email, CancellationToken cancellationToken = default)
    {
        var existing = await FindAsync(externalTestConsultantId, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        Validate(name, email);
        await EnsureNameNotDuplicatedAsync(name, existing.ExternalTestConsultantId, cancellationToken);

        existing.Name = name.Trim();
        existing.Department = department.Trim();
        existing.Email = email.Trim();

        await repository.UpdateAsync(existing, Audit("UpdateExternalTestConsultant", existing.ExternalTestConsultantId, $"Name: {existing.Name}"), cancellationToken);
        return existing;
    }

    public async Task<TestConsultant?> SetStatusAsync(Guid externalTestConsultantId, bool isInactive, CancellationToken cancellationToken = default)
    {
        var existing = await FindAsync(externalTestConsultantId, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        // Already in the requested state: nothing to change, and re-stamping InactiveDate would
        // restart the retention clock.
        if (existing.IsInactive == isInactive)
        {
            return existing;
        }

        // Matches legacy, which stops an allocated consultant being deactivated (legacy only did so
        // client-side). Reactivating is never blocked.
        if (isInactive && existing.AllocationCount > 0)
        {
            throw new ExternalTestConsultantValidationException(
                [new ExternalTestConsultantValidationError(string.Empty, InUseMessage(existing, "To change this external test consultant's Active/Inactive status"))]);
        }

        existing.IsInactive = isInactive;
        existing.InactiveDate = isInactive ? DateTime.UtcNow : null;

        await repository.UpdateAsync(existing, Audit("SetExternalTestConsultantStatus", existing.ExternalTestConsultantId, $"Inactive: {isInactive}"), cancellationToken);
        return existing;
    }

    public async Task<(bool Success, string? Message)> DeleteAsync(Guid externalTestConsultantId, CancellationToken cancellationToken = default)
    {
        var existing = await FindAsync(externalTestConsultantId, cancellationToken);
        if (existing is null)
        {
            return (false, "Test consultant was not found.");
        }

        if (existing.AllocationCount > 0)
        {
            return (false, InUseMessage(existing, "To delete this external test consultant"));
        }

        var deleted = await repository.DeleteAsync(externalTestConsultantId, Audit("DeleteExternalTestConsultant", externalTestConsultantId, $"Name: {existing.Name}"), cancellationToken);
        return deleted ? (true, null) : (false, "Test consultant was not found.");
    }

    private static string InUseMessage(TestConsultant consultant, string action) =>
        $"This external test consultant is being used on: {consultant.AllocationCount} scheme(s). {action} first remove it from the following schemes: {consultant.AllocationString}";

    private async Task<TestConsultant?> FindAsync(Guid externalTestConsultantId, CancellationToken cancellationToken)
    {
        var all = await repository.GetAllAsync(cancellationToken);
        return all.FirstOrDefault(c => c.ExternalTestConsultantId == externalTestConsultantId);
    }

    // Case-insensitive on the trimmed name; a consultant keeping its own name on update is not a duplicate.
    private async Task EnsureNameNotDuplicatedAsync(string name, Guid? excludingId, CancellationToken cancellationToken)
    {
        var trimmed = name.Trim();
        var all = await repository.GetAllAsync(cancellationToken);
        if (all.Any(c => c.ExternalTestConsultantId != excludingId && string.Equals(c.Name.Trim(), trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ExternalTestConsultantValidationException(
                [new ExternalTestConsultantValidationError(nameof(TestConsultant.Name), ExternalTestConsultantValidator.DuplicateNameMessage)]);
        }
    }

    private static void Validate(string name, string email)
    {
        var result = ExternalTestConsultantValidator.Validate(name, email);
        if (!result.IsValid)
        {
            throw new ExternalTestConsultantValidationException(result.Errors);
        }
    }
}
