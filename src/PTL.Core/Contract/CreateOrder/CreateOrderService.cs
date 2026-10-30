using Microsoft.Extensions.Options;
using PTL.Core.Contract.PendingOrder;
using PTL.Core.Customer;
using PTL.Core.Lookup;
using PTL.Core.Participant;
using CoreCustomer = PTL.Core.Customer.Customer;
using CoreParticipant = PTL.Core.Participant.Participant;
using CoreScheme = PTL.Core.Scheme.Scheme;

namespace PTL.Core.Contract.CreateOrder;

public interface ICreateOrderService
{
    // Null when the participant does not exist.
    Task<CreateOrderEligibility?> GetEligibilityAsync(Guid participantId, CancellationToken cancellationToken = default);

    // Legacy PendingContractOrder.aspx Page_Load (stage 1): creates the draft pending order if the customer has none,
    // renews next year's schemes, and returns the priced scheme grid.
    Task<CreateOrderResult> GetOrderAsync(Guid participantId, OrderYear year, CancellationToken cancellationToken = default);

    // Legacy Check_Clicked: applies one checkbox change to the draft, then returns the re-priced page.
    // Throws ArgumentException for an unknown field.
    Task<CreateOrderResult> ApplyChangeAsync(Guid participantId, OrderYear year, CreateOrderLineChange change, CancellationToken cancellationToken = default);

    // Legacy "Clear Form" confirmation: deletes the draft's scheme lines and the draft order itself.
    Task<CreateOrderAccess> ClearAsync(Guid participantId, OrderYear year, CancellationToken cancellationToken = default);
}

public sealed class CreateOrderService(
    ICreateOrderRepository createOrderRepository,
    IPendingOrderRepository pendingOrderRepository,
    IParticipantRepository participantRepository,
    ICustomerRepository customerRepository,
    ILookupRepository lookupRepository,
    IOptions<CreateOrderOptions> options) : ICreateOrderService
{
    private const string SelectField = "select";
    private const string ImportPermitField = "importexport";

    private static readonly string[] MonthFields = ["jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec"];
    private static readonly string[] MonthLabels = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    // Financial-year order (Apr..Mar) - the column order of the legacy month strip.
    private static readonly int[] FinancialYearMonths = [4, 5, 6, 7, 8, 9, 10, 11, 12, 1, 2, 3];

    private sealed record OrderContext(CoreParticipant Participant, CoreCustomer Customer, OrderYear Year, int YearId, SystemSettingsEntity Settings);

    private sealed class OrderState(OrderContext context, PendingOrderEntity order, List<AvailableSchemeEntity> schemes, List<ParticipatingSchemeEntity> participating, List<PendingOrderSchemeEntity> lines)
    {
        public OrderContext Context { get; } = context;
        public PendingOrderEntity Order { get; } = order;
        public List<AvailableSchemeEntity> Schemes { get; } = schemes;
        public List<ParticipatingSchemeEntity> Participating { get; set; } = participating;
        public List<PendingOrderSchemeEntity> Lines { get; set; } = lines;
    }

    public async Task<CreateOrderEligibility?> GetEligibilityAsync(Guid participantId, CancellationToken cancellationToken = default)
    {
        var participant = await participantRepository.GetByIdAsync(participantId, cancellationToken);
        if (participant is null)
        {
            return null;
        }

        var customer = await customerRepository.GetByIdAsync(participant.CustomerId, cancellationToken);
        if (!await OnlineOrderingEligibility.CanOrderOnlineAsync(participantRepository, participant, customer, cancellationToken))
        {
            return new CreateOrderEligibility(false, false, false);
        }

        var settings = await lookupRepository.GetSystemSettingsAsync(cancellationToken);
        return new CreateOrderEligibility(
            true,
            await NoSubmittedOrderAsync(participant.CustomerId, settings.CurrentYearId, cancellationToken),
            await NoSubmittedOrderAsync(participant.CustomerId, settings.NextYearId, cancellationToken));
    }

    public async Task<CreateOrderResult> GetOrderAsync(Guid participantId, OrderYear year, CancellationToken cancellationToken = default)
    {
        var (access, context) = await ResolveContextAsync(participantId, year, cancellationToken);
        if (context is null)
        {
            return new CreateOrderResult(access);
        }

        var state = await LoadStateAsync(context, cancellationToken);
        return new CreateOrderResult(CreateOrderAccess.Allowed, await BuildPageAsync(state, cancellationToken));
    }

    public async Task<CreateOrderResult> ApplyChangeAsync(Guid participantId, OrderYear year, CreateOrderLineChange change, CancellationToken cancellationToken = default)
    {
        var field = change.Field.Trim().ToLowerInvariant();
        if (field != SelectField && field != ImportPermitField && Array.IndexOf(MonthFields, field) < 0)
        {
            throw new ArgumentException($"Unknown order field '{change.Field}'.", nameof(change));
        }

        var (access, context) = await ResolveContextAsync(participantId, year, cancellationToken);
        if (context is null)
        {
            return new CreateOrderResult(access);
        }

        var state = await LoadStateAsync(context, cancellationToken);
        if (await ApplyAsync(state, change.SchemeId, field, change.Checked, cancellationToken))
        {
            state.Lines = (await pendingOrderRepository.GetSchemesAsync(state.Order.PendingContractId, cancellationToken)).ToList();
        }

        return new CreateOrderResult(CreateOrderAccess.Allowed, await BuildPageAsync(state, cancellationToken));
    }

    public async Task<CreateOrderAccess> ClearAsync(Guid participantId, OrderYear year, CancellationToken cancellationToken = default)
    {
        var (access, context) = await ResolveContextAsync(participantId, year, cancellationToken);
        if (context is null)
        {
            return access;
        }

        var order = await createOrderRepository.GetPendingOrderAsync(context.Customer.CustomerId, context.YearId, isSubmitted: false, cancellationToken);
        if (order is null)
        {
            return CreateOrderAccess.Allowed;
        }

        foreach (var line in await pendingOrderRepository.GetSchemesAsync(order.PendingContractId, cancellationToken))
        {
            await pendingOrderRepository.DeleteSchemeAsync(line.PendingParticipantSchemeId, cancellationToken);
        }

        await pendingOrderRepository.MarkDecidedAsync(context.Customer.CustomerId, order.PendingContractId, cancellationToken);
        return CreateOrderAccess.Allowed;
    }

    // Legacy CreateCurrentOrderPageGuard / CreateNextOrderPageGuard: CanOrderOnline plus no submitted order for the year.
    private async Task<(CreateOrderAccess Access, OrderContext? Context)> ResolveContextAsync(Guid participantId, OrderYear year, CancellationToken cancellationToken)
    {
        var participant = await participantRepository.GetByIdAsync(participantId, cancellationToken);
        var customer = participant is null ? null : await customerRepository.GetByIdAsync(participant.CustomerId, cancellationToken);
        if (participant is null || customer is null)
        {
            return (CreateOrderAccess.NotFound, null);
        }

        if (!await OnlineOrderingEligibility.CanOrderOnlineAsync(participantRepository, participant, customer, cancellationToken))
        {
            return (CreateOrderAccess.NotEligible, null);
        }

        var settings = await lookupRepository.GetSystemSettingsAsync(cancellationToken);
        var yearId = year == OrderYear.Current ? settings.CurrentYearId : settings.NextYearId;
        if (!await NoSubmittedOrderAsync(customer.CustomerId, yearId, cancellationToken))
        {
            return (CreateOrderAccess.NotEligible, null);
        }

        return (CreateOrderAccess.Allowed, new OrderContext(participant, customer, year, yearId, settings));
    }

    private async Task<bool> NoSubmittedOrderAsync(Guid customerId, int yearId, CancellationToken cancellationToken) =>
        await createOrderRepository.GetPendingOrderAsync(customerId, yearId, isSubmitted: true, cancellationToken) is null;

    private async Task<OrderState> LoadStateAsync(OrderContext context, CancellationToken cancellationToken)
    {
        var customerId = context.Customer.CustomerId;
        var order = await createOrderRepository.GetPendingOrderAsync(customerId, context.YearId, isSubmitted: false, cancellationToken);
        if (order is null)
        {
            await createOrderRepository.CreatePendingOrderAsync(Guid.NewGuid(), customerId, context.YearId, cancellationToken);
            order = await createOrderRepository.GetPendingOrderAsync(customerId, context.YearId, isSubmitted: false, cancellationToken)
                ?? throw new InvalidOperationException("The pending order could not be created.");
        }

        var schemes = (await createOrderRepository.GetAvailableSchemesAsync(context.YearId, cancellationToken)).ToList();
        var participating = await GetMergedParticipatingAsync(context.Participant.ParticipantId, context.YearId, cancellationToken);
        var lines = (await pendingOrderRepository.GetSchemesAsync(order.PendingContractId, cancellationToken)).ToList();
        var state = new OrderState(context, order, schemes, participating, lines);

        if (context.Year == OrderYear.Next && participating.Count == 0)
        {
            await RenewCurrentYearSchemesAsync(state, cancellationToken);
        }

        await DeselectNoLongerAvailableMonthsAsync(state, cancellationToken);
        return state;
    }

    // Legacy GetCurrentlyParticipatingSchemes: ordered by name, with a scheme ordered twice merged into one row.
    private async Task<List<ParticipatingSchemeEntity>> GetMergedParticipatingAsync(Guid participantId, int yearId, CancellationToken cancellationToken)
    {
        var rows = (await createOrderRepository.GetParticipatingSchemesAsync(participantId, yearId, yearId, cancellationToken))
            .OrderBy(p => p.Name, StringComparer.Ordinal);

        var merged = new List<ParticipatingSchemeEntity>();
        foreach (var row in rows)
        {
            var existing = merged.FirstOrDefault(m => m.SchemeId == row.SchemeId);
            if (existing is null)
            {
                merged.Add(row);
            }
            else
            {
                existing.MergeMonthsFrom(row);
            }
        }

        return merged;
    }

    // Legacy RenewCurrentYearsCurrentlyParticipatingSchemes: with nothing ordered for next year yet, the schemes the participant
    // holds this year are pre-selected (with every editable month) and listed in the top table even though not yet purchased.
    private async Task RenewCurrentYearSchemesAsync(OrderState state, CancellationToken cancellationToken)
    {
        var context = state.Context;
        var currentYearSchemes = await GetMergedParticipatingAsync(context.Participant.ParticipantId, context.Settings.CurrentYearId, cancellationToken);
        var seedLines = state.Lines.Count == 0;
        var renewed = new List<ParticipatingSchemeEntity>();

        foreach (var held in currentYearSchemes)
        {
            var nextYearScheme = state.Schemes.FirstOrDefault(s => string.Equals(s.Identifier, held.Identifier, StringComparison.Ordinal));
            if (nextYearScheme is null)
            {
                continue;
            }

            if (seedLines)
            {
                var line = NewLine(state.Order.PendingContractId, context.Participant.ParticipantId, nextYearScheme, participating: null);
                await createOrderRepository.InsertSchemeAsync(line, context.Customer.PaymentNonUK, cancellationToken);
            }

            renewed.Add(new ParticipatingSchemeEntity
            {
                ParticipantId = context.Participant.ParticipantId,
                SchemeId = nextYearScheme.SchemeId,
                Identifier = nextYearScheme.Identifier,
                Name = nextYearScheme.Name,
                Schedule = nextYearScheme.Schedule
            });
        }

        state.Participating = renewed;
        if (seedLines && renewed.Count > 0)
        {
            state.Lines = (await pendingOrderRepository.GetSchemesAsync(state.Order.PendingContractId, cancellationToken)).ToList();
        }
    }

    // Legacy DeselectNoLongerAvailableDistributions: a pending month whose distribution has since been posted, or that the
    // participant now already holds, is dropped.
    private async Task DeselectNoLongerAvailableMonthsAsync(OrderState state, CancellationToken cancellationToken)
    {
        var changed = false;
        foreach (var line in state.Lines)
        {
            var scheme = state.Schemes.FirstOrDefault(s => s.SchemeId == line.SchemeId);
            if (scheme is null)
            {
                continue;
            }

            var held = state.Participating.FirstOrDefault(p => p.SchemeId == line.SchemeId);
            var lineChanged = false;
            for (var month = 1; month <= 12; month++)
            {
                if (line.GetMonth(month) && ((scheme.Offers(month) && !scheme.CanEdit(month)) || (held?.Holds(month) ?? false)))
                {
                    line.SetMonth(month, false);
                    lineChanged = true;
                }
            }

            if (lineChanged)
            {
                await pendingOrderRepository.UpdateSchemeAsync(line, line.DataConsentDeclarationGiven, cancellationToken);
                changed = true;
            }
        }

        if (changed)
        {
            state.Lines = (await pendingOrderRepository.GetSchemesAsync(state.Order.PendingContractId, cancellationToken)).ToList();
        }
    }

    // A month the participant may tick: the scheme offers it, it has not been posted, and they do not already hold it.
    private static bool IsMonthEditable(AvailableSchemeEntity scheme, ParticipatingSchemeEntity? held, int month) =>
        scheme.Offers(month) && scheme.CanEdit(month) && !(held?.Holds(month) ?? false);

    private static PendingOrderSchemeEntity NewLine(Guid pendingContractId, Guid participantId, AvailableSchemeEntity scheme, ParticipatingSchemeEntity? participating)
    {
        var line = new PendingOrderSchemeEntity
        {
            PendingParticipantSchemeId = Guid.NewGuid(),
            PendingContractId = pendingContractId,
            ParticipantId = participantId,
            SchemeId = scheme.SchemeId,
            IsSelected = true,
            ImportExportLicenceRequired = false
        };

        for (var month = 1; month <= 12; month++)
        {
            if (IsMonthEditable(scheme, participating, month))
            {
                line.SetMonth(month, true);
            }
        }

        return line;
    }

    private bool IsListed(OrderState state, AvailableSchemeEntity scheme) =>
        state.Participating.Any(p => p.SchemeId == scheme.SchemeId)
        || scheme.ComerciallyAvailable
        || state.Context.Customer.CustomerTypeId == options.Value.AphaCustomerTypeId;

    // Returns true when the draft changed. Changes the page would not have allowed (a locked month, a scheme that is not listed)
    // are ignored rather than trusted from the caller.
    private async Task<bool> ApplyAsync(OrderState state, Guid schemeId, string field, bool isChecked, CancellationToken cancellationToken)
    {
        var scheme = state.Schemes.FirstOrDefault(s => s.SchemeId == schemeId);
        if (scheme is null || !IsListed(state, scheme))
        {
            return false;
        }

        var held = state.Participating.FirstOrDefault(p => p.SchemeId == schemeId);
        var line = state.Lines.FirstOrDefault(l => l.SchemeId == schemeId);
        if (line is null)
        {
            return await InsertFirstLineAsync(state, scheme, held, field, isChecked, cancellationToken);
        }

        if (!ApplyToLine(line, scheme, held, field, isChecked))
        {
            return false;
        }

        await pendingOrderRepository.UpdateSchemeAsync(line, line.DataConsentDeclarationGiven, cancellationToken);
        return true;
    }

    // Legacy creates the line on the first click, pre-ticking every editable month.
    private async Task<bool> InsertFirstLineAsync(
        OrderState state, AvailableSchemeEntity scheme, ParticipatingSchemeEntity? held, string field, bool isChecked, CancellationToken cancellationToken)
    {
        if (field != SelectField || !isChecked)
        {
            return false;
        }

        var context = state.Context;
        await createOrderRepository.InsertSchemeAsync(
            NewLine(state.Order.PendingContractId, context.Participant.ParticipantId, scheme, held),
            context.Customer.PaymentNonUK,
            cancellationToken);
        return true;
    }

    private static bool ApplyToLine(PendingOrderSchemeEntity line, AvailableSchemeEntity scheme, ParticipatingSchemeEntity? held, string field, bool isChecked)
    {
        if (field == SelectField)
        {
            line.IsSelected = isChecked;
            return true;
        }

        if (!line.IsSelected)
        {
            return false;
        }

        return field == ImportPermitField
            ? ApplyImportPermit(line, scheme, held, isChecked)
            : ApplyMonth(line, scheme, held, field, isChecked);
    }

    private static bool ApplyImportPermit(PendingOrderSchemeEntity line, AvailableSchemeEntity scheme, ParticipatingSchemeEntity? held, bool isChecked)
    {
        if (!Enumerable.Range(1, 12).Any(m => IsMonthEditable(scheme, held, m)))
        {
            return false;
        }

        line.ImportExportLicenceRequired = isChecked;
        return true;
    }

    private static bool ApplyMonth(PendingOrderSchemeEntity line, AvailableSchemeEntity scheme, ParticipatingSchemeEntity? held, string field, bool isChecked)
    {
        var month = Array.IndexOf(MonthFields, field) + 1;
        if (!IsMonthEditable(scheme, held, month))
        {
            return false;
        }

        line.SetMonth(month, isChecked);

        // Legacy SetImportExportChecked: an import permit only stays required while an editable month is ticked.
        if (line.ImportExportLicenceRequired)
        {
            line.ImportExportLicenceRequired = Enumerable.Range(1, 12).Any(m => IsMonthEditable(scheme, held, m) && line.GetMonth(m));
        }

        return true;
    }

    private async Task<CreateOrderPage> BuildPageAsync(OrderState state, CancellationToken cancellationToken)
    {
        var context = state.Context;
        var customer = context.Customer;
        var years = await lookupRepository.GetAllYearsAsync(cancellationToken);
        var countries = await lookupRepository.GetCountriesAsync(cancellationToken);
        var plans = await lookupRepository.GetPostagePricingPlansForYearAsync(context.YearId, cancellationToken);
        var annualPrices = (await lookupRepository.GetSchemeCurrenciesAsync(cancellationToken))
            .Where(p => p.CurrencyId == customer.CurrencyId)
            .GroupBy(p => p.SchemeId)
            .ToDictionary(g => g.Key, g => g.First().Price);

        var countryType = PendingOrderPricing.CountryTypeFor(countries, customer.CountryId);
        var pricingSchemes = new Dictionary<Guid, CoreScheme>();
        foreach (var scheme in state.Schemes)
        {
            pricingSchemes[scheme.SchemeId] = scheme.ToPricingScheme();
        }

        // Legacy TotalPriceItems / getTotalPostagePrice only count selected lines.
        var selected = state.Lines.Where(l => l.IsSelected).ToList();
        var schemeTotal = selected.Sum(l => l.Price);
        var postageTotal = PendingOrderPricing.TotalPostagePrice(selected, pricingSchemes, plans, countryType);

        CreateOrderScheme? BuildRow(AvailableSchemeEntity scheme, ParticipatingSchemeEntity? held)
        {
            if (!annualPrices.TryGetValue(scheme.SchemeId, out var annualPrice))
            {
                return null;
            }

            var line = state.Lines.FirstOrDefault(l => l.SchemeId == scheme.SchemeId);
            var isSelected = line?.IsSelected ?? false;
            var months = FinancialYearMonths.Select(month =>
            {
                var holds = held?.Holds(month) ?? false;
                var enabled = IsMonthEditable(scheme, held, month);
                var isTicked = held is null || enabled ? line?.GetMonth(month) ?? false : holds;
                return new CreateOrderMonth(MonthLabels[month - 1], month, scheme.Offers(month), isTicked, enabled, holds);
            }).ToList();

            return new CreateOrderScheme(
                scheme.SchemeId,
                scheme.Identifier,
                scheme.Name,
                scheme.Schedule,
                annualPrice,
                isSelected,
                line?.ImportExportLicenceRequired ?? false,
                months.Any(m => m.Enabled),
                isSelected ? PendingOrderPricing.SchemePostagePrice(line!, pricingSchemes[scheme.SchemeId], plans, countryType) : 0m,
                isSelected ? line!.Price : 0m,
                months);
        }

        var participatingRows = state.Participating
            .Select(held => (Scheme: state.Schemes.FirstOrDefault(s => s.SchemeId == held.SchemeId), Held: held))
            .Where(x => x.Scheme is not null)
            .Select(x => BuildRow(x.Scheme!, x.Held))
            .OfType<CreateOrderScheme>()
            .ToList();

        var isApha = customer.CustomerTypeId == options.Value.AphaCustomerTypeId;
        var availableGroups = state.Schemes
            .Where(s => (s.ComerciallyAvailable || isApha) && state.Participating.All(p => p.SchemeId != s.SchemeId))
            .OrderBy(s => s.Name, StringComparer.Ordinal)
            .Select(s => BuildRow(s, held: null))
            .OfType<CreateOrderScheme>()
            .GroupBy(r => r.Schedule)
            .Select(g => new CreateOrderScheduleGroup(g.Key, g.ToList()))
            .ToList();

        return new CreateOrderPage(
            context.Year,
            years.FirstOrDefault(y => y.YearId == context.YearId)?.Year ?? string.Empty,
            state.Order.CurrencySymbol,
            customer.QalNumber,
            context.Participant.LabCode,
            isApha,
            participatingRows,
            availableGroups,
            schemeTotal,
            postageTotal,
            selected.Any(l => l.HasAnyMonth()));
    }
}
