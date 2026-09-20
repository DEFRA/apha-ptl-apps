using Microsoft.Extensions.Logging;

namespace PTL.Core.Scheme;

public sealed class SchemeService(ISchemeRepository schemeRepository, ILogger<SchemeService> logger) : ISchemeService
{
    private static readonly Action<ILogger, int, string?, int, int, Exception?> LogSchemeSearchMessage =
        LoggerMessage.Define<int, string?, int, int>(
            LogLevel.Information,
            new EventId(1, nameof(LogSchemeSearchMessage)),
            "Scheme search: yearId={YearId} searchTerm={SearchTerm} page={Page} totalResults={TotalCount}");

    private static readonly Action<ILogger, Guid, int, string, Exception?> LogCreatedSchemeMessage =
        LoggerMessage.Define<Guid, int, string>(
            LogLevel.Information,
            new EventId(2, nameof(LogCreatedSchemeMessage)),
            "Created scheme {SchemeId} for year {YearId} ({Identifier})");

    private static readonly Action<ILogger, Guid, Exception?> LogUpdateUnknownSchemeMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(3, nameof(LogUpdateUnknownSchemeMessage)),
            "Update requested for unknown scheme {SchemeId}");

    private static readonly Action<ILogger, Guid, Exception?> LogUpdatedSchemeMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(4, nameof(LogUpdatedSchemeMessage)),
            "Updated scheme {SchemeId}");

    private static readonly Action<ILogger, Guid, string, Exception?> LogSchemeValidationFailedMessage =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Warning,
            new EventId(5, nameof(LogSchemeValidationFailedMessage)),
            "Scheme validation failed for {SchemeId}: {Errors}");

    private static readonly Action<ILogger, Guid, Exception?> LogSchemeHistoryMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(6, nameof(LogSchemeHistoryMessage)),
            "Displayed scheme family history for {SharedId}");

    public Task<Scheme?> GetSchemeAsync(Guid schemeId, CancellationToken cancellationToken = default) =>
        schemeRepository.GetByIdAsync(schemeId, cancellationToken);

    public async Task<SchemeSearchResult> SearchSchemesAsync(int yearId, string? searchTerm, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 20 : pageSize;

        var all = await schemeRepository.GetSummariesByYearAsync(yearId, cancellationToken);

        // Search matches the current identifier/name, mirroring SchemeList.aspx's grid (no
        // dedicated search box in the legacy screen, but Identifier/Name are the natural free-text
        // fields to filter this domain by, consistent with ContractService's suffix-search approach).
        var filtered = string.IsNullOrWhiteSpace(searchTerm)
            ? all
            : all.Where(s =>
                (s.CurrentIdentifier is not null && s.CurrentIdentifier.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
                (s.CurrentName is not null && s.CurrentName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)))
                .ToList();

        var totalCount = filtered.Count;
        var items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        LogSchemeSearchMessage(logger, yearId, searchTerm, page, totalCount, null);

        return new SchemeSearchResult(items, totalCount);
    }

    public async Task<IReadOnlyList<SchemeHistoryEntity>> GetSchemeFamilyHistoryAsync(Guid sharedId, CancellationToken cancellationToken = default)
    {
        var history = await schemeRepository.GetHistoryAsync(sharedId, cancellationToken);
        LogSchemeHistoryMessage(logger, sharedId, null);
        return history;
    }

    public async Task<Scheme> CreateSchemeAsync(Scheme scheme, CancellationToken cancellationToken = default)
    {
        // SchemeId and SharedId are always server-generated on create - SharedId links this
        // scheme to its (currently single-member) multi-year family; LastModified is stamped
        // fresh, matching the legacy DataPortal_Insert behaviour.
        scheme.SchemeId = Guid.NewGuid();
        scheme.SharedId = Guid.NewGuid();
        scheme.LastModified = DateTime.UtcNow;

        Validate(scheme);

        var created = await schemeRepository.CreateAsync(scheme, cancellationToken);
        LogCreatedSchemeMessage(logger, created.SchemeId, created.YearId, created.Identifier, null);
        return created;
    }

    public async Task<Scheme?> UpdateSchemeAsync(Guid schemeId, Scheme updatedFields, CancellationToken cancellationToken = default)
    {
        var existing = await schemeRepository.GetByIdAsync(schemeId, cancellationToken);
        if (existing is null)
        {
            LogUpdateUnknownSchemeMessage(logger, schemeId, null);
            return null;
        }

        if (existing.IsReadOnly)
        {
            // Preserves the legacy Scheme.aspx.vb SetReadonly() intent as an explicit server-side
            // rule: schemes belonging to a closed year (YearId < current year) cannot be edited.
            throw new SchemeValidationException([new SchemeValidationError(string.Empty, "This scheme belongs to a closed year and cannot be edited.")]);
        }

        // SchemeId and SharedId are immutable via this API; RequiresAssessment can only be set at
        // creation (Scheme.aspx.vb disables the checkbox once a SchemeId exists) - so the existing
        // value always wins here regardless of what was posted.
        updatedFields.SchemeId = existing.SchemeId;
        updatedFields.SharedId = existing.SharedId;
        updatedFields.RequiresAssessment = existing.RequiresAssessment;
        updatedFields.LastModified = DateTime.UtcNow;

        Validate(updatedFields);

        var updated = await schemeRepository.UpdateAsync(updatedFields, cancellationToken);
        LogUpdatedSchemeMessage(logger, schemeId, null);
        return updated;
    }

    private void Validate(Scheme scheme)
    {
        var result = SchemeValidator.Validate(scheme);
        if (!result.IsValid)
        {
            LogSchemeValidationFailedMessage(logger, scheme.SchemeId, string.Join("; ", result.Errors.Select(e => e.Message)), null);
            throw new SchemeValidationException(result.Errors);
        }
    }
}
