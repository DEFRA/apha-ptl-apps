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

    private static readonly Action<ILogger, Guid, Guid, int, Exception?> LogRenewedSchemeMessage =
        LoggerMessage.Define<Guid, Guid, int>(
            LogLevel.Information,
            new EventId(7, nameof(LogRenewedSchemeMessage)),
            "Built renewal draft from scheme {OldSchemeId} for family {SharedId}, year {YearId}");

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

    public async Task<SchemeSearchResult> GetSchemeFamiliesAsync(int page, int pageSize, string? searchTerm, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        // 20 matches the rest of the Core layer's search/list services (CustomerService,
        // ParticipantService) - InternalWeb's own default (PaginationModel.DefaultPageSize) is what
        // callers actually see when a page size isn't supplied.
        pageSize = pageSize is < 1 or > 200 ? 20 : pageSize;

        var all = await schemeRepository.GetAllSummariesAsync(cancellationToken);

        // Legacy Search.aspx's "Use the search box below to identify the required Scheme by Scheme
        // Name" - a plain substring match, no other filtering behaviour.
        var filtered = string.IsNullOrWhiteSpace(searchTerm)
            ? all
            : all.Where(s => s.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();

        var items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        LogSchemeSearchMessage(logger, 0, searchTerm, page, filtered.Count, null);

        return new SchemeSearchResult(items, filtered.Count);
    }

    public async Task<IReadOnlyList<SchemeHistoryEntity>> GetSchemeFamilyHistoryAsync(Guid sharedId, CancellationToken cancellationToken = default)
    {
        var history = await schemeRepository.GetHistoryAsync(sharedId, cancellationToken);
        LogSchemeHistoryMessage(logger, sharedId, null);
        return history;
    }

    public async Task<Scheme?> RenewSchemeAsync(Guid schemeId, CancellationToken cancellationToken = default)
    {
        var oldScheme = await schemeRepository.GetByIdAsync(schemeId, cancellationToken);
        if (oldScheme is null)
        {
            return null;
        }

        var newScheme = new Scheme
        {
            SharedId = oldScheme.SharedId,
            YearId = oldScheme.YearId + 1,
            Identifier = oldScheme.Identifier,
            Name = oldScheme.Name,
            ScheduleId = oldScheme.ScheduleId,
            ScheduleCodeId = oldScheme.ScheduleCodeId,
            DistributionMonthJan = oldScheme.DistributionMonthJan,
            DistributionMonthFeb = oldScheme.DistributionMonthFeb,
            DistributionMonthMar = oldScheme.DistributionMonthMar,
            DistributionMonthApr = oldScheme.DistributionMonthApr,
            DistributionMonthMay = oldScheme.DistributionMonthMay,
            DistributionMonthJun = oldScheme.DistributionMonthJun,
            DistributionMonthJul = oldScheme.DistributionMonthJul,
            DistributionMonthAug = oldScheme.DistributionMonthAug,
            DistributionMonthSep = oldScheme.DistributionMonthSep,
            DistributionMonthOct = oldScheme.DistributionMonthOct,
            DistributionMonthNov = oldScheme.DistributionMonthNov,
            DistributionMonthDec = oldScheme.DistributionMonthDec,
            DistributionAsAvailable = oldScheme.DistributionAsAvailable,
            WeekNumber = oldScheme.WeekNumber,
            DayOfWeekId = oldScheme.DayOfWeekId,
            NumberOfSamples = oldScheme.NumberOfSamples,
            SampleOrigin = oldScheme.SampleOrigin,
            Deadline = oldScheme.Deadline,
            Subcontractor = oldScheme.Subcontractor,
            CombinedPackaging = oldScheme.CombinedPackaging,
            CustomsVolume = oldScheme.CustomsVolume,
            SamplePackingInstructions = oldScheme.SamplePackingInstructions,
            RequiresAssessment = oldScheme.RequiresAssessment,
            CommentsRequired = oldScheme.CommentsRequired,
            Pilot = oldScheme.Pilot,
            LimitedSampleAvailability = oldScheme.LimitedSampleAvailability,
            Accredited = oldScheme.Accredited,
            NoVLALabs = oldScheme.NoVLALabs,
            ComerciallyAvailable = oldScheme.ComerciallyAvailable,
            CustomsDescription = oldScheme.CustomsDescription,
            DataConsentDeclarationActive = oldScheme.DataConsentDeclarationActive,
            DataConsentDeclarationText = oldScheme.DataConsentDeclarationText,
            Instructions = oldScheme.Instructions,
            DateOfReceipt = oldScheme.DateOfReceipt,
            StorageConditions = oldScheme.StorageConditions,
            ConditionOnReceipt = oldScheme.ConditionOnReceipt,
            TestConsultant1 = oldScheme.TestConsultant1,
            TestConsultant2 = oldScheme.TestConsultant2,
            TestConsultant3 = oldScheme.TestConsultant3,
            UseExternalReference = oldScheme.UseExternalReference,
            StoreRatings = oldScheme.StoreRatings,
            Assessor1 = oldScheme.Assessor1,
            Assessor2 = oldScheme.Assessor2,
            Assessor3 = oldScheme.Assessor3,
            Assessor4 = oldScheme.Assessor4,
            StandardTabulationText = oldScheme.StandardTabulationText,
        };

        newScheme.Postage = await ResolveRenewedPostageAsync(oldScheme.Postage, oldScheme.YearId, cancellationToken);

        newScheme.Prices = [.. oldScheme.Prices.Select(price => new SchemeCurrencyPrice
        {
            CurrencyId = price.CurrencyId,
            Price = price.Price,
            CurrencyName = price.CurrencyName,
            CurrencySymbol = price.CurrencySymbol,
        })];

        newScheme.ViewerIds = [.. (await viewerRepository.GetSchemeViewersAsync(schemeId, cancellationToken)).Select(link => link.ViewerId)];

        // Legacy CopyScheme: every Test/Tabulation (and their child items) get fresh ids, tracked
        // in one dictionary so the Tabulations' item references and the Test Consultant tabulation
        // selection can be remapped onto the new ids afterwards.
        var idMap = new Dictionary<Guid, Guid>();

        newScheme.Tests = [.. oldScheme.Tests.Select(test =>
        {
            var newTestId = Guid.NewGuid();
            idMap[test.TestId] = newTestId;

            return new SchemeTest
            {
                TestId = newTestId,
                TestTypeId = test.TestTypeId,
                TestType = test.TestType,
                Order = test.Order,
                ResultItems = [.. test.ResultItems.Select(item =>
                {
                    var newId = Guid.NewGuid();
                    idMap[item.TestResultItemId] = newId;
                    return new SchemeTestResultItem { TestResultItemId = newId, TestResultItemTypeId = item.TestResultItemTypeId, TestResultItemType = item.TestResultItemType, TestId = newTestId, Order = item.Order };
                })],
                MethodItems = [.. test.MethodItems.Select(item =>
                {
                    var newId = Guid.NewGuid();
                    idMap[item.TestMethodItemId] = newId;
                    return new SchemeTestMethodItem { TestMethodItemId = newId, TestMethodItemTypeId = item.TestMethodItemTypeId, TestMethodItemType = item.TestMethodItemType, TestId = newTestId, Order = item.Order };
                })],
                Categories = [.. test.Categories.Select(category =>
                {
                    var newCategoryId = Guid.NewGuid();
                    return new SchemeCategoryItem
                    {
                        CategoryItemId = newCategoryId,
                        CategoryItemTypeId = category.CategoryItemTypeId,
                        Name = category.Name,
                        TestId = newTestId,
                        Order = category.Order,
                        Criteria = [.. category.Criteria.Select(criterion => new SchemeCriterionItem
                        {
                            CriterionItemId = Guid.NewGuid(),
                            CriterionItemTypeId = criterion.CriterionItemTypeId,
                            Name = criterion.Name,
                            CategoryItemId = newCategoryId,
                            Order = criterion.Order,
                        })],
                    };
                })],
            };
        })];

        newScheme.Tabulations = [.. oldScheme.Tabulations.Select(tabulation =>
        {
            var newTabulationId = Guid.NewGuid();
            idMap[tabulation.TabulationId] = newTabulationId;
            return new SchemeTabulation
            {
                TabulationId = newTabulationId,
                Name = tabulation.Name,
                IntendedResultsOnly = tabulation.IntendedResultsOnly,
                SingleParticipantTabulation = tabulation.SingleParticipantTabulation,
                ShowRatings = tabulation.ShowRatings,
                AvailableToParticipants = tabulation.AvailableToParticipants,
                AvailableToViewers = tabulation.AvailableToViewers,
                ResultItemIds = [.. tabulation.ResultItemIds.Select(id => idMap.GetValueOrDefault(id, id))],
                MethodItemIds = [.. tabulation.MethodItemIds.Select(id => idMap.GetValueOrDefault(id, id))],
            };
        })];

        // CopyScheme: TestConsultantTabulationId only follows the new tabulation ids while the
        // scheme doesn't require assessment - otherwise the Test Consultants tab (and this id)
        // aren't in use, so legacy leaves it unmapped.
        newScheme.TestConsultantTabulationId = !newScheme.RequiresAssessment && oldScheme.TestConsultantTabulationId is { } oldTabulationId
            ? idMap.GetValueOrDefault(oldTabulationId, oldTabulationId)
            : oldScheme.TestConsultantTabulationId;

        // Legacy SetEditPermissions(), run immediately by RenewScheme so the redisplayed form
        // already reflects the new year's locked months and derived start date.
        var (settings, editability) = await LoadSchedulingContextAsync(newScheme.YearId, cancellationToken);
        SchemeDistributionMonths.ApplyEditability(newScheme, editability);
        newScheme.StartDate = SchemeStartDate.Calculate(newScheme, settings.ContractStartDate);

        LogRenewedSchemeMessage(logger, schemeId, newScheme.SharedId, newScheme.YearId, null);
        return newScheme;
    }

    // Legacy getNextYearsPostagePlan: looks up the old plan's name for the scheme's current year,
    // then the plan with that same name for the following year. SafeDataReader leaves every field
    // at its default when no row matches, so a missing next-year plan silently resolves to null -
    // there is no exception to preserve.
    private async Task<Guid?> ResolveRenewedPostageAsync(Guid? oldPostageId, int oldYearId, CancellationToken cancellationToken)
    {
        if (oldPostageId is not { } postageId || postageId == Guid.Empty)
        {
            return oldPostageId;
        }

        var oldYearPlans = await lookupRepository.GetPostagePricingPlansForYearAsync(oldYearId, cancellationToken);
        var oldPlanName = oldYearPlans.FirstOrDefault(plan => plan.YearId == oldYearId && plan.PostageId == postageId)?.Name;
        if (oldPlanName is null)
        {
            return null;
        }

        var nextYearPlans = await lookupRepository.GetPostagePricingPlansForYearAsync(oldYearId + 1, cancellationToken);
        return nextYearPlans.FirstOrDefault(plan => plan.YearId == oldYearId + 1 && string.Equals(plan.Name, oldPlanName, StringComparison.OrdinalIgnoreCase))?.PostageId;
    }

    private async Task<(SystemSettingsEntity Settings, SchemeMonthEditability Editability)> LoadSchedulingContextAsync(int yearId, CancellationToken cancellationToken)
    {
        var settings = await lookupRepository.GetSystemSettingsAsync(cancellationToken);
        var distributions = await lookupRepository.GetMonthlyDistributionsAsync(cancellationToken);
        var editability = SchemeDistributionMonths.CalculateEditability(yearId, settings.ContractStartDate.Month, distributions);
        return (settings, editability);
    }

    public async Task<Scheme> CreateSchemeAsync(Scheme scheme, CancellationToken cancellationToken = default)
    {
        // A populated SharedId means this draft continues an existing family (Renew) rather than
        // starting a new one. Its most recent sibling scheme is the baseline RestoreLockedMonths
        // needs to defend locked distribution months with - legacy's DistributionMonthX setters
        // simply refuse a locked assignment, leaving whatever CopyScheme had already inherited from
        // that same sibling in place, rather than discarding it for lack of a persisted row.
        Scheme? renewedFrom = null;
        if (scheme.SharedId != Guid.Empty)
        {
            var family = await schemeRepository.GetHistoryAsync(scheme.SharedId, cancellationToken);
            var mostRecentSchemeId = family.OrderByDescending(h => h.YearId).FirstOrDefault()?.SchemeId;
            if (mostRecentSchemeId is { } priorSchemeId)
            {
                renewedFrom = await schemeRepository.GetByIdAsync(priorSchemeId, cancellationToken);
            }
        }

        // SchemeId is always server-generated on create. SharedId links this scheme to its
        // family: a plain Create has no family yet so one is minted, but a Renew draft already
        // carries the family's SharedId forward and that must be preserved rather than replaced.
        scheme.SchemeId = Guid.NewGuid();
        scheme.SharedId = scheme.SharedId == Guid.Empty ? Guid.NewGuid() : scheme.SharedId;
        scheme.LastModified = DateTime.UtcNow;

        await ValidateAsync(scheme, renewedFrom, cancellationToken);

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

        // Legacy sanitised the instructions markup in the editor's Text setter, so whatever was
        // posted was cleaned before it reached the business object.
        scheme.Instructions = SchemeInstructions.Sanitise(scheme.Instructions);

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

        // Legacy UniquePTNumberValidator_ServerValidate (Scheme.aspx.vb): invalid only when the
        // posted identifier already exists AND differs from the scheme's own baseline identifier
        // (mScheme.Identifier, as loaded before this edit) - so leaving the PT number unchanged
        // never trips the check, however many of this scheme's own family rows already hold it,
        // but changing it to a value already used elsewhere always does. `existing` is that
        // baseline: the current DB row on Update, or the family's most recent scheme on a Renew
        // Create (see CreateSchemeAsync) - null (no baseline) on a genuinely new scheme.
        if (!string.IsNullOrWhiteSpace(scheme.Identifier) &&
            !string.Equals(scheme.Identifier, existing?.Identifier, StringComparison.OrdinalIgnoreCase))
        {
            var existingIdentifiers = await lookupRepository.GetPTNumbersAsync(cancellationToken);
            if (existingIdentifiers.Any(pt => string.Equals(pt.Identifier, scheme.Identifier, StringComparison.OrdinalIgnoreCase)))
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
