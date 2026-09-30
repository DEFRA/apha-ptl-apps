using PTL.Core.Lookup;
using PTL.Core.Participant;
using PTL.Core.Scheme;
using CoreContract = PTL.Core.Contract.Contract;
using CoreScheme = PTL.Core.Scheme.Scheme;

namespace PTL.Core.Contract.PendingOrder;

public interface IPendingOrderService
{
    // Legacy ReviewPendingOrders.aspx splits outstanding orders into the current and next contract
    // year, each sorted by submit date.
    Task<(IReadOnlyList<PendingOrderSummaryEntity> CurrentYear, IReadOnlyList<PendingOrderSummaryEntity> NextYear)> GetPendingOrdersAsync(CancellationToken cancellationToken = default);

    // Returns null when the pending order does not exist.
    Task<PendingOrderDetail?> GetPendingOrderAsync(Guid pendingContractId, CancellationToken cancellationToken = default);

    // Per-row month / Import-Export Licence / Remove edit. Returns false when the scheme line is
    // not part of the given order.
    Task<bool> UpdateSchemeAsync(Guid pendingContractId, Guid pendingParticipantSchemeId, PendingOrderSchemeEdit edit, CancellationToken cancellationToken = default);

    // Creates the contract and its participant schemes, then soft-deletes the pending order.
    Task<bool> ApprovePendingOrderAsync(Guid pendingContractId, string purchaseOrderNumber, string approvedBy, CancellationToken cancellationToken = default);

    // Deletes every pending scheme line, then soft-deletes the pending order. The live contract
    // records are untouched.
    Task<bool> DeclinePendingOrderAsync(Guid pendingContractId, CancellationToken cancellationToken = default);
}

// The fully-priced pending order the details screen renders.
public sealed record PendingOrderDetail(
    PendingOrderEntity Order,
    string QalNumber,
    string Year,
    IReadOnlyList<PendingOrderSchemeLine> Schemes,
    decimal TotalSchemePrice,
    decimal TotalPostagePrice)
{
    public decimal Total => TotalSchemePrice + TotalPostagePrice;
}

public sealed record PendingOrderSchemeLine(PendingOrderSchemeEntity Scheme, decimal PostagePrice, IReadOnlyList<PendingOrderMonthCell> Months)
{
    public decimal TotalPrice => Scheme.Price + PostagePrice;
}

// One month checkbox on the scheme grid, resolved exactly as legacy
// RepeaterPendingOrderSchemes_DataBound does.
public sealed record PendingOrderMonthCell(string Label, int MonthNumber, bool Selected, bool Enabled, bool AlreadyParticipating);

public sealed record PendingOrderSchemeEdit(
    bool DistributionMonthJan,
    bool DistributionMonthFeb,
    bool DistributionMonthMar,
    bool DistributionMonthApr,
    bool DistributionMonthMay,
    bool DistributionMonthJun,
    bool DistributionMonthJul,
    bool DistributionMonthAug,
    bool DistributionMonthSep,
    bool DistributionMonthOct,
    bool DistributionMonthNov,
    bool DistributionMonthDec,
    bool ImportExportLicenceRequired,
    bool IsRemoved);

public sealed class PendingOrderService(
    IPendingOrderRepository pendingOrderRepository,
    IContractRepository contractRepository,
    IParticipantSchemeRepository participantSchemeRepository,
    ISchemeRepository schemeRepository,
    PTL.Core.Customer.ICustomerRepository customerRepository,
    ILookupRepository lookupRepository) : IPendingOrderService
{
    // Legacy getNewContractSuffix supports at most 51 contracts per customer per year.
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public async Task<(IReadOnlyList<PendingOrderSummaryEntity> CurrentYear, IReadOnlyList<PendingOrderSummaryEntity> NextYear)> GetPendingOrdersAsync(CancellationToken cancellationToken = default)
    {
        var all = await pendingOrderRepository.GetSummariesAsync(cancellationToken);
        var settings = await lookupRepository.GetSystemSettingsAsync(cancellationToken);
        var years = (await lookupRepository.GetAllYearsAsync(cancellationToken)).ToDictionary(y => y.YearId, y => y.Year);

        // spgaPendingContracts has no WHERE clause, so submitted/deleted filtering happens here -
        // exactly where legacy ReviewPendingOrders.aspx.vb does it.
        var outstanding = all.Where(o => o.IsSubmitted && !o.IsDeleted).ToList();
        foreach (var order in outstanding)
        {
            order.Year = years.GetValueOrDefault(order.YearId, string.Empty);
        }

        return (
            outstanding.Where(o => o.YearId == settings.CurrentYearId).OrderBy(o => o.OrderSubmitDate).ToList(),
            outstanding.Where(o => o.YearId == settings.NextYearId).OrderBy(o => o.OrderSubmitDate).ToList());
    }

    public async Task<PendingOrderDetail?> GetPendingOrderAsync(Guid pendingContractId, CancellationToken cancellationToken = default)
    {
        var order = await pendingOrderRepository.GetByIdAsync(pendingContractId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        var schemes = await pendingOrderRepository.GetSchemesAsync(pendingContractId, cancellationToken);
        var context = await BuildPricingContextAsync(order, schemes, cancellationToken);

        var lines = new List<PendingOrderSchemeLine>(schemes.Count);
        foreach (var s in schemes)
        {
            var realScheme = context.RealSchemes.GetValueOrDefault(s.SchemeId);

            // Only the single-row procedure returns fldDistributionMonthXxxIsContracted, so the
            // grid needs one fetch per line - legacy is equally chatty, re-fetching the Scheme for
            // every row in RepeaterPendingOrderSchemes_DataBound.
            var detailed = await pendingOrderRepository.GetSchemeByIdAsync(s.PendingParticipantSchemeId, cancellationToken) ?? s;

            lines.Add(new PendingOrderSchemeLine(
                s,
                PendingOrderPricing.SchemePostagePrice(s, realScheme, context.PostagePlans, context.CountryType),
                BuildMonthCells(detailed, realScheme)));
        }

        var years = await lookupRepository.GetAllYearsAsync(cancellationToken);

        return new PendingOrderDetail(
            order,
            context.QalNumber,
            years.FirstOrDefault(y => y.YearId == order.YearId)?.Year ?? string.Empty,
            lines,
            PendingOrderPricing.TotalSchemePrice(schemes),
            PendingOrderPricing.TotalPostagePrice(schemes, context.RealSchemes, context.PostagePlans, context.CountryType));
    }

    // Mirrors legacy RepeaterPendingOrderSchemes_DataBound:
    //   chk.Enabled = scheme.DistributionMonthX And scheme.CanEditX And scheme.CanEditXParticipants
    //   chk.Checked = If(chk.Enabled, pendingScheme.DistributionMonthX, scheme.DistributionMonthX)
    //   If pendingScheme.DistributionMonthXIsContracted Then FlagCheckboxAsAlreadyParticipating(chk)
    // CanEditXParticipants is fnIsDistributionNotPosted, which the pending-scheme procedures also
    // return as fldCanEditX, so the pending row supplies it.
    private static List<PendingOrderMonthCell> BuildMonthCells(
        PendingOrderSchemeEntity pendingScheme,
        CoreScheme? realScheme)
    {
        var offered = SchemeMonths(realScheme);
        var cells = new List<PendingOrderMonthCell>(12);

        foreach (var (label, monthNumber, selected, canEditParticipants, isContracted) in pendingScheme.FinancialYearMonths())
        {
            if (isContracted)
            {
                cells.Add(new PendingOrderMonthCell(label, monthNumber, Selected: true, Enabled: false, AlreadyParticipating: true));
                continue;
            }

            var (schemeOffersMonth, schemeCanEdit) = offered.GetValueOrDefault(monthNumber);
            var enabled = schemeOffersMonth && schemeCanEdit && canEditParticipants;

            cells.Add(new PendingOrderMonthCell(label, monthNumber, enabled ? selected : schemeOffersMonth, enabled, AlreadyParticipating: false));
        }

        return cells;
    }

    private static Dictionary<int, (bool Offered, bool CanEdit)> SchemeMonths(CoreScheme? scheme) =>
        scheme is null
            ? []
            : new Dictionary<int, (bool, bool)>
            {
                [1] = (scheme.DistributionMonthJan, scheme.CanEditJan),
                [2] = (scheme.DistributionMonthFeb, scheme.CanEditFeb),
                [3] = (scheme.DistributionMonthMar, scheme.CanEditMar),
                [4] = (scheme.DistributionMonthApr, scheme.CanEditApr),
                [5] = (scheme.DistributionMonthMay, scheme.CanEditMay),
                [6] = (scheme.DistributionMonthJun, scheme.CanEditJun),
                [7] = (scheme.DistributionMonthJul, scheme.CanEditJul),
                [8] = (scheme.DistributionMonthAug, scheme.CanEditAug),
                [9] = (scheme.DistributionMonthSep, scheme.CanEditSep),
                [10] = (scheme.DistributionMonthOct, scheme.CanEditOct),
                [11] = (scheme.DistributionMonthNov, scheme.CanEditNov),
                [12] = (scheme.DistributionMonthDec, scheme.CanEditDec),
            };

    public async Task<bool> UpdateSchemeAsync(Guid pendingContractId, Guid pendingParticipantSchemeId, PendingOrderSchemeEdit edit, CancellationToken cancellationToken = default)
    {
        var schemes = await pendingOrderRepository.GetSchemesAsync(pendingContractId, cancellationToken);
        var scheme = schemes.FirstOrDefault(s => s.PendingParticipantSchemeId == pendingParticipantSchemeId);
        if (scheme is null)
        {
            return false;
        }

        scheme.DistributionMonthJan = edit.DistributionMonthJan;
        scheme.DistributionMonthFeb = edit.DistributionMonthFeb;
        scheme.DistributionMonthMar = edit.DistributionMonthMar;
        scheme.DistributionMonthApr = edit.DistributionMonthApr;
        scheme.DistributionMonthMay = edit.DistributionMonthMay;
        scheme.DistributionMonthJun = edit.DistributionMonthJun;
        scheme.DistributionMonthJul = edit.DistributionMonthJul;
        scheme.DistributionMonthAug = edit.DistributionMonthAug;
        scheme.DistributionMonthSep = edit.DistributionMonthSep;
        scheme.DistributionMonthOct = edit.DistributionMonthOct;
        scheme.DistributionMonthNov = edit.DistributionMonthNov;
        scheme.DistributionMonthDec = edit.DistributionMonthDec;
        scheme.ImportExportLicenceRequired = edit.ImportExportLicenceRequired;
        scheme.IsRemoved = edit.IsRemoved;

        await pendingOrderRepository.UpdateSchemeAsync(scheme, scheme.DataConsentDeclarationGiven, cancellationToken);
        return true;
    }

    // Port of PendingContractOrder.aspx.vb's ButtonApprove_Click.
    public async Task<bool> ApprovePendingOrderAsync(Guid pendingContractId, string purchaseOrderNumber, string approvedBy, CancellationToken cancellationToken = default)
    {
        var order = await pendingOrderRepository.GetByIdAsync(pendingContractId, cancellationToken);
        if (order is null)
        {
            return false;
        }

        var schemes = await pendingOrderRepository.GetSchemesAsync(pendingContractId, cancellationToken);
        var context = await BuildPricingContextAsync(order, schemes, cancellationToken);

        await pendingOrderRepository.UpdatePurchaseOrderNumberAsync(pendingContractId, purchaseOrderNumber, cancellationToken);

        var contract = await BuildContractAsync(order, schemes, context, purchaseOrderNumber, approvedBy, cancellationToken);

        // Created via the repository rather than ContractService: the latter force-sets
        // IsOnlineOrder to false and stamps CommencementDate, neither of which legacy's approval
        // path does. Same approach as RenewContractsService.
        var created = await contractRepository.CreateAsync(contract, cancellationToken);

        foreach (var scheme in schemes.Where(s => !s.IsRemoved))
        {
            await participantSchemeRepository.CreateAsync(ToParticipantScheme(created.ContractId, scheme), cancellationToken);
        }

        return await pendingOrderRepository.MarkDecidedAsync(order.CustomerId, pendingContractId, cancellationToken);
    }

    // Port of ButtonDecline_Click - every pending scheme line is deleted outright, then the order
    // is soft-deleted. Nothing is written to the live contract tables.
    public async Task<bool> DeclinePendingOrderAsync(Guid pendingContractId, CancellationToken cancellationToken = default)
    {
        var order = await pendingOrderRepository.GetByIdAsync(pendingContractId, cancellationToken);
        if (order is null)
        {
            return false;
        }

        var schemes = await pendingOrderRepository.GetSchemesAsync(pendingContractId, cancellationToken);
        foreach (var scheme in schemes)
        {
            await pendingOrderRepository.DeleteSchemeAsync(scheme.PendingParticipantSchemeId, cancellationToken);
        }

        return await pendingOrderRepository.MarkDecidedAsync(order.CustomerId, pendingContractId, cancellationToken);
    }

    private async Task<PricingContext> BuildPricingContextAsync(PendingOrderEntity order, IReadOnlyList<PendingOrderSchemeEntity> schemes, CancellationToken cancellationToken)
    {
        var customer = await customerRepository.GetByIdAsync(order.CustomerId, cancellationToken);
        var countries = await lookupRepository.GetCountriesAsync(cancellationToken);
        var postagePlans = await lookupRepository.GetPostagePricingPlansForYearAsync(order.YearId, cancellationToken);

        var realSchemes = new Dictionary<Guid, CoreScheme>();
        foreach (var schemeId in schemes.Select(s => s.SchemeId).Distinct())
        {
            var realScheme = await schemeRepository.GetByIdAsync(schemeId, cancellationToken);
            if (realScheme is not null)
            {
                realSchemes[schemeId] = realScheme;
            }
        }

        return new PricingContext(
            customer?.QalNumber ?? string.Empty,
            PendingOrderPricing.CountryTypeFor(countries, customer?.CountryId ?? Guid.Empty),
            postagePlans,
            realSchemes);
    }

    private async Task<CoreContract> BuildContractAsync(
        PendingOrderEntity order,
        IReadOnlyList<PendingOrderSchemeEntity> schemes,
        PricingContext context,
        string purchaseOrderNumber,
        string approvedBy,
        CancellationToken cancellationToken)
    {
        var existingContracts = await contractRepository.GetSummariesByYearAsync(order.CustomerId, order.YearId, cancellationToken);

        var contract = new CoreContract
        {
            ContractId = Guid.NewGuid(),
            CustomerId = order.CustomerId,
            YearId = order.YearId,
            Suffix = NextContractSuffix(existingContracts.Count),
            IsActive = true,
            PurchaseOrderNumber = purchaseOrderNumber,
            JobSheetPostedDate = DateTime.UtcNow,
            OptOutOfInvoiceGeneration = false,
            ApprovedBy = approvedBy,
            ApprovedDate = DateTime.UtcNow,

            // Legacy hard-codes both to 0 on the approval path.
            AdministrationCharge = 0m,
            DiscountRate = 0m,

            // Legacy sets this true here - the only place a contract is flagged as an online order.
            IsOnlineOrder = true
        };

        ApplyPostage(contract, schemes, context, "Courier", (c, price, count) => { c.CourierPrice = price; c.NumberCourier = count; });
        ApplyPostage(contract, schemes, context, "Biofreeze", (c, price, count) => { c.PostagePrice = price; c.NumberPostage = count; });
        ApplyPostage(contract, schemes, context, "Dry Ice", (c, price, count) => { c.SpecialDeliveryPrice = price; c.NumberSpecialDelivery = count; });

        return contract;
    }

    private static void ApplyPostage(
        CoreContract contract,
        IReadOnlyList<PendingOrderSchemeEntity> schemes,
        PricingContext context,
        string planName,
        Action<CoreContract, decimal, int> assign)
    {
        var plan = context.PostagePlans.FirstOrDefault(p => string.Equals(p.Name, planName, StringComparison.OrdinalIgnoreCase));
        if (plan is null)
        {
            // Legacy stores -1 when the plan lookup throws, which downstream reporting treats as
            // "not derivable" - preserved rather than silently defaulting to zero.
            assign(contract, -1m, -1);
            return;
        }

        var unitPrice = context.CountryType switch
        {
            "UK" => plan.UKPrice.GetValueOrDefault(),
            "EU" => plan.EUPrice.GetValueOrDefault(),
            _ => plan.NonEUPrice.GetValueOrDefault()
        };

        assign(contract, unitPrice, PendingOrderPricing.PostageTypeNumbers(schemes, context.RealSchemes, plan.PostageId));
    }

    private static ParticipantSchemeRecord ToParticipantScheme(Guid contractId, PendingOrderSchemeEntity scheme) => new()
    {
        ParticipantSchemeId = Guid.NewGuid(),
        ContractId = contractId,
        ParticipantId = scheme.ParticipantId,
        SchemeId = scheme.SchemeId,
        DistributionMonthJan = scheme.DistributionMonthJan,
        DistributionMonthFeb = scheme.DistributionMonthFeb,
        DistributionMonthMar = scheme.DistributionMonthMar,
        DistributionMonthApr = scheme.DistributionMonthApr,
        DistributionMonthMay = scheme.DistributionMonthMay,
        DistributionMonthJun = scheme.DistributionMonthJun,
        DistributionMonthJul = scheme.DistributionMonthJul,
        DistributionMonthAug = scheme.DistributionMonthAug,
        DistributionMonthSep = scheme.DistributionMonthSep,
        DistributionMonthOct = scheme.DistributionMonthOct,
        DistributionMonthNov = scheme.DistributionMonthNov,
        DistributionMonthDec = scheme.DistributionMonthDec,

        // Every value below is fixed by legacy's approval path, not carried from the pending row.
        NumberOfSetsRequired = 1,
        ExternalReference = string.Empty,
        Contact = string.Empty,
        ImportExportLicenceRequired = scheme.ImportExportLicenceRequired,
        CustomsCertificateRequired = true,
        NonFeePaying = false,
        IsWeightedPricing = true,
        DataConsentDeclarationGiven = scheme.DataConsentDeclarationGiven
    };

    // Legacy getNewContractSuffix: "" for the first contract, then A..Z, then AA..AZ/BA.. up to 51.
    private static string NextContractSuffix(int existingContractCount) => existingContractCount switch
    {
        0 => string.Empty,
        < 27 => Alphabet[existingContractCount - 1].ToString(),
        < 52 => $"{Alphabet[((existingContractCount - 1) / 26) - 1]}{Alphabet[(existingContractCount - 1) % 26]}",
        _ => string.Empty
    };

    private sealed record PricingContext(
        string QalNumber,
        string CountryType,
        IReadOnlyList<PostagePricingPlanEntity> PostagePlans,
        IReadOnlyDictionary<Guid, CoreScheme> RealSchemes);
}
