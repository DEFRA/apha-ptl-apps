using Microsoft.Extensions.Logging;
using PTL.Core.Lookup;
using PTL.Core.Viewer;

namespace PTL.Core.Scheme;

public sealed class SchemeService(
    ISchemeRepository schemeRepository,
    ILookupRepository lookupRepository,
    IViewerRepository viewerRepository,
    ILogger<SchemeService> logger) : ISchemeService
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
        LoadWithViewersAsync(schemeId, cancellationToken);

    private async Task<Scheme?> LoadWithViewersAsync(Guid schemeId, CancellationToken cancellationToken)
    {
        var scheme = await schemeRepository.GetByIdAsync(schemeId, cancellationToken);
        if (scheme is null)
        {
            return null;
        }

        var links = await viewerRepository.GetSchemeViewersAsync(schemeId, cancellationToken);
        scheme.ViewerIds = links.Select(link => link.ViewerId).ToList();
        return scheme;
    }

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

        await ValidateAsync(scheme, existing: null, cancellationToken);

        var created = await schemeRepository.CreateAsync(scheme, cancellationToken);
        await SyncViewersAsync(created.SchemeId, scheme.ViewerIds, cancellationToken);
        created.ViewerIds = scheme.ViewerIds;
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

        await ValidateAsync(updatedFields, existing, cancellationToken);

        var updated = await schemeRepository.UpdateAsync(updatedFields, cancellationToken);
        if (updated is null)
        {
            return null;
        }

        await SyncViewersAsync(schemeId, updatedFields.ViewerIds, cancellationToken);
        updated.ViewerIds = updatedFields.ViewerIds;
        LogUpdatedSchemeMessage(logger, schemeId, null);
        return updated;
    }

    // Legacy ViewerSchemeCollection.Update: delete the links that were removed, insert the ones
    // that were added, leave the rest alone. tlnkViewerScheme has no update procedure.
    private async Task SyncViewersAsync(Guid schemeId, IList<Guid> viewerIds, CancellationToken cancellationToken)
    {
        var existing = await viewerRepository.GetSchemeViewersAsync(schemeId, cancellationToken);
        var requested = viewerIds.ToHashSet();

        foreach (var link in existing.Where(link => !requested.Contains(link.ViewerId)))
        {
            await viewerRepository.RemoveSchemeViewerAsync(link.ViewerSchemeId, cancellationToken);
        }

        var alreadyLinked = existing.Select(link => link.ViewerId).ToHashSet();
        foreach (var viewerId in requested.Where(id => !alreadyLinked.Contains(id)))
        {
            await viewerRepository.AddSchemeViewerAsync(Guid.NewGuid(), viewerId, schemeId, cancellationToken);
        }
    }

    // Values legacy derives rather than asking the user for, applied before validation so the
    // saved record and the redisplayed form agree.
    private async Task ApplyDerivedFieldsAsync(Scheme scheme, Scheme? existing, CancellationToken cancellationToken)
    {
        scheme.Identifier = SchemeIdentifier.Normalise(scheme.Identifier);
        // CheckBoxCheckChangedShowRatings: ticking Show Ratings Table on any tabulation turns on
        // Score Samples for the scheme.
        if (scheme.Tabulations.Any(t => t.ShowRatings))
        {
            scheme.StoreRatings = true;
        }

        // Combined packaging schemes are despatched in week 1.
        if (scheme.CombinedPackaging)
        {
            scheme.WeekNumber = 1;
        }

        // RequiresAssessment setter: switching mode discards the staffing that no longer applies.
        if (scheme.RequiresAssessment)
        {
            scheme.TestConsultant1 = null;
            scheme.TestConsultant2 = null;
            scheme.TestConsultant3 = null;
        }
        else
        {
            scheme.Assessor1 = null;
            scheme.Assessor2 = null;
            scheme.Assessor3 = null;
            scheme.Assessor4 = null;

            // Categories and criteria only exist on assessment schemes (RemoveAllCategories).
            foreach (var test in scheme.Tests)
            {
                test.Categories.Clear();
            }
        }

        var settings = await lookupRepository.GetSystemSettingsAsync(cancellationToken);

        // Legacy's DistributionMonthX setters refuse an assignment while that month is locked, so a
        // locked month keeps whatever it already had no matter what was posted.
        var distributions = await lookupRepository.GetMonthlyDistributionsAsync(cancellationToken);
        var editability = SchemeDistributionMonths.CalculateEditability(scheme.YearId, settings.ContractStartDate.Month, distributions);
        SchemeDistributionMonths.RestoreLockedMonths(scheme, editability, existing);
        SchemeDistributionMonths.ApplyEditability(scheme, editability);

        scheme.StartDate = SchemeStartDate.Calculate(scheme, settings.ContractStartDate);
    }

    private async Task ValidateAsync(Scheme scheme, Scheme? existing, CancellationToken cancellationToken)
    {
        await ApplyDerivedFieldsAsync(scheme, existing, cancellationToken);

        var result = SchemeValidator.Validate(scheme);
        var errors = result.Errors.ToList();

        // Legacy UniquePTNumberValidator (Scheme.aspx.vb): the identifier must not already be in
        // use by a different scheme, in any year. The scheme's own row is excluded so re-saving
        // an unchanged identifier stays valid.
        if (!string.IsNullOrWhiteSpace(scheme.Identifier))
        {
            var existingIdentifiers = await lookupRepository.GetPTNumbersAsync(cancellationToken);
            if (existingIdentifiers.Any(pt =>
                    pt.SchemeId != scheme.SchemeId &&
                    string.Equals(pt.Identifier, scheme.Identifier, StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add(new SchemeValidationError(nameof(Scheme.Identifier), "This PT number already exists. Please choose a unique PT number"));
            }
        }

        // Legacy populates the Primary Test Consultant dropdown from internal users only; the
        // Deputy and Secondary lists also include external consultants.
        if (!scheme.RequiresAssessment && scheme.TestConsultant1 is { } primary && primary != Guid.Empty)
        {
            var consultants = await lookupRepository.GetTestConsultantsAsync(cancellationToken);
            if (consultants.Any(c => c.UserId == primary && c.IsExternal))
            {
                errors.Add(new SchemeValidationError(nameof(Scheme.TestConsultant1), "The Primary Test Consultant must be an internal Test Consultant"));
            }
        }

        if (errors.Count > 0)
        {
            LogSchemeValidationFailedMessage(logger, scheme.SchemeId, string.Join("; ", errors.Select(e => e.Message)), null);
            throw new SchemeValidationException(errors);
        }
    }
}
