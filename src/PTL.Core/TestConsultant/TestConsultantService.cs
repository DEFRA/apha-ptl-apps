namespace PTL.Core.TestConsultant;

public sealed class TestConsultantService(ITestConsultantRepository repository, IExternalLoginService externalLoginService) : ITestConsultantService
{
    public Task<IReadOnlyList<TestConsultant>> GetAllAsync(CancellationToken cancellationToken = default) =>
        repository.GetAllAsync(cancellationToken);

    public async Task<TestConsultant> CreateAsync(string name, string department, string email, CancellationToken cancellationToken = default)
    {
        Validate(name, email);

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

        await repository.CreateAsync(newConsultant, cancellationToken);
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

        existing.Name = name.Trim();
        existing.Department = department.Trim();
        existing.Email = email.Trim();

        await repository.UpdateAsync(existing, cancellationToken);
        return existing;
    }

    public async Task<TestConsultant?> SetStatusAsync(Guid externalTestConsultantId, bool isInactive, CancellationToken cancellationToken = default)
    {
        var existing = await FindAsync(externalTestConsultantId, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        existing.IsInactive = isInactive;
        existing.InactiveDate = isInactive ? DateTime.UtcNow : null;

        await repository.UpdateAsync(existing, cancellationToken);
        return existing;
    }

    public async Task<(bool Success, string? Message)> GenerateLoginAsync(Guid externalTestConsultantId, CancellationToken cancellationToken = default)
    {
        var existing = await FindAsync(externalTestConsultantId, cancellationToken);
        if (existing is null)
        {
            return (false, "Test consultant was not found.");
        }

        var success = await externalLoginService.GenerateLoginAsync(existing.ExternalTestConsultantId, existing.Name, existing.Email, cancellationToken);
        if (!success)
        {
            return (false, "The login could not be generated.");
        }

        existing.SsoId = Guid.NewGuid();
        await repository.UpdateAsync(existing, cancellationToken);
        return (true, null);
    }

    private async Task<TestConsultant?> FindAsync(Guid externalTestConsultantId, CancellationToken cancellationToken)
    {
        var all = await repository.GetAllAsync(cancellationToken);
        return all.FirstOrDefault(c => c.ExternalTestConsultantId == externalTestConsultantId);
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
