using PTL.Core.Audit;
using PTL.Core.Lookup;
using PTL.Core.Scheme;
using CoreContract = PTL.Core.Contract.Contract;
using CoreScheme = PTL.Core.Scheme.Scheme;

namespace PTL.Core.Contract.PendingOrder;

public interface IPendingOrderService
{
    // Legacy ReviewPendingOrders.aspx splits outstanding orders into the current and next contract
    // year, each sorted by submit date.
    Task<(IReadOnlyList<PendingOrderSummaryEntity> CurrentYear, IReadOnlyList<PendingOrderSummaryEntity> NextYear)> GetPendingOrdersAsync(CancellationToken cancellationToken = default);

    // Returns null when the pending order does not exist or is no longer outstanding (already
    // approved/declined, or never submitted) - legacy only ever loaded submitted, non-deleted orders.
    Task<PendingOrderDetail?> GetPendingOrderAsync(Guid pendingContractId, CancellationToken cancellationToken = default);

    // Per-row month / Import-Export Licence / Remove edit. Returns false when the scheme line is
    // not part of the given outstanding order.
    Task<bool> UpdateSchemeAsync(Guid pendingContractId, Guid pendingParticipantSchemeId, PendingOrderSchemeEdit edit, CancellationToken cancellationToken = default);

    // Creates the contract and its participant schemes, soft-deletes the pending order, then sends the
    // approval emails (a failed send never undoes the approval; the scheduler retries it). Throws PendingOrderValidationException for an over-long purchase order number/comments.
    Task<bool> ApprovePendingOrderAsync(Guid pendingContractId, string purchaseOrderNumber, string comments = "", CancellationToken cancellationToken = default);

    // Deletes every pending scheme line, soft-deletes the pending order, then sends the decline emails.
    // The live contract records are untouched.
    Task<bool> DeclinePendingOrderAsync(Guid pendingContractId, string comments = "", CancellationToken cancellationToken = default);
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
    IPendingOrderContractWriter contractWriter,
    ISchemeRepository schemeRepository,
    PTL.Core.Customer.ICustomerRepository customerRepository,
    ILookupRepository lookupRepository,
    IAuditActorResolver auditActor,
    IPendingOrderDecisionEmails decisionEmails) : IPendingOrderService
{
    // Contract.PurchaseOrderNumber's CSLA StringMaxLength rule and PendingContractOrder.aspx's comments regex.
    private const int PurchaseOrderNumberMaxLength = 255;
    private const int CommentsMaxLength = 2000;

    // Legacy only loads FetchPendingContractOrder(customerId, True, yearId) - submitted and not deleted.
    private async Task<PendingOrderEntity?> GetOutstandingOrderAsync(Guid pendingContractId, CancellationToken cancellationToken)
    {
        var order = await pendingOrderRepository.GetByIdAsync(pendingContractId, cancellationToken);
        return order is { IsSubmitted: true, IsDeleted: false } ? order : null;
    }

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
        var order = await GetOutstandingOrderAsync(pendingContractId, cancellationToken);
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
    //   Jan-Mar: chk.Enabled = scheme.DistributionMonthX And scheme.CanEditX And scheme.CanEditXParticipants
    //   Apr-Dec: chk.Enabled = scheme.DistributionMonthX And scheme.CanEditXParticipants   (no scheme.CanEditX)
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
            var schemeAllowsEdit = monthNumber > 3 || schemeCanEdit;
            var enabled = schemeOffersMonth && schemeAllowsEdit && canEditParticipants;

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
        if (await GetOutstandingOrderAsync(pendingContractId, cancellationToken) is null)
        {
            return false;
        }

        var schemes = await pendingOrderRepository.GetSchemesAsync(pendingContractId, cancellationToken);
        var scheme = schemes.FirstOrDefault(s => s.PendingParticipantSchemeId == pendingParticipantSchemeId);
        if (scheme is null)
        {
            return false;
        }

        // Legacy Check_Clicked sets only the one control the user changed, so a month the page shows locked keeps
        // its stored value; the whole row posts back here, so only months the legacy grid enables take the posted value.
        var detailed = await pendingOrderRepository.GetSchemeByIdAsync(pendingParticipantSchemeId, cancellationToken) ?? scheme;
        var realScheme = await schemeRepository.GetByIdAsync(scheme.SchemeId, cancellationToken);
        var editable = BuildMonthCells(detailed, realScheme).Where(m => m.Enabled).Select(m => m.MonthNumber).ToHashSet();
        bool Month(int monthNumber, bool posted, bool stored) => editable.Contains(monthNumber) ? posted : stored;

        scheme.DistributionMonthJan = Month(1, edit.DistributionMonthJan, scheme.DistributionMonthJan);
        scheme.DistributionMonthFeb = Month(2, edit.DistributionMonthFeb, scheme.DistributionMonthFeb);
        scheme.DistributionMonthMar = Month(3, edit.DistributionMonthMar, scheme.DistributionMonthMar);
        scheme.DistributionMonthApr = Month(4, edit.DistributionMonthApr, scheme.DistributionMonthApr);
        scheme.DistributionMonthMay = Month(5, edit.DistributionMonthMay, scheme.DistributionMonthMay);
        scheme.DistributionMonthJun = Month(6, edit.DistributionMonthJun, scheme.DistributionMonthJun);
        scheme.DistributionMonthJul = Month(7, edit.DistributionMonthJul, scheme.DistributionMonthJul);
        scheme.DistributionMonthAug = Month(8, edit.DistributionMonthAug, scheme.DistributionMonthAug);
        scheme.DistributionMonthSep = Month(9, edit.DistributionMonthSep, scheme.DistributionMonthSep);
        scheme.DistributionMonthOct = Month(10, edit.DistributionMonthOct, scheme.DistributionMonthOct);
        scheme.DistributionMonthNov = Month(11, edit.DistributionMonthNov, scheme.DistributionMonthNov);
        scheme.DistributionMonthDec = Month(12, edit.DistributionMonthDec, scheme.DistributionMonthDec);
        scheme.ImportExportLicenceRequired = edit.ImportExportLicenceRequired;
        scheme.IsRemoved = edit.IsRemoved;

        await pendingOrderRepository.UpdateSchemeAsync(scheme, scheme.DataConsentDeclarationGiven, cancellationToken);
        return true;
    }

    // Port of PendingContractOrder.aspx.vb's ButtonApprove_Click.
    public async Task<bool> ApprovePendingOrderAsync(Guid pendingContractId, string purchaseOrderNumber, string comments = "", CancellationToken cancellationToken = default)
    {
        var order = await GetOutstandingOrderAsync(pendingContractId, cancellationToken);
        if (order is null)
        {
            return false;
        }

        purchaseOrderNumber ??= string.Empty;
        comments ??= string.Empty;
        Validate(purchaseOrderNumber, comments);

        var schemes = await pendingOrderRepository.GetSchemesAsync(pendingContractId, cancellationToken);
        var context = await BuildPricingContextAsync(order, schemes, cancellationToken);

        await pendingOrderRepository.UpdatePurchaseOrderNumberAsync(pendingContractId, purchaseOrderNumber, cancellationToken);

        var contract = await BuildContractAsync(order, schemes, context, purchaseOrderNumber, await ApprovedByAsync(cancellationToken), cancellationToken);
        var created = await contractWriter.CreateAsync(contract, schemes, cancellationToken);

        var decided = await pendingOrderRepository.MarkDecidedAsync(order.CustomerId, pendingContractId, cancellationToken);
        if (decided)
        {
            await decisionEmails.QueueAndSendAsync(order, created.ContractId, comments, isApproved: true, cancellationToken);
        }

        return decided;
    }

    // Port of ButtonDecline_Click - every pending scheme line is deleted outright, then the order
    // is soft-deleted. Nothing is written to the live contract tables.
    public async Task<bool> DeclinePendingOrderAsync(Guid pendingContractId, string comments = "", CancellationToken cancellationToken = default)
    {
        var order = await GetOutstandingOrderAsync(pendingContractId, cancellationToken);
        if (order is null)
        {
            return false;
        }

        comments ??= string.Empty;
        Validate(string.Empty, comments);

        var schemes = await pendingOrderRepository.GetSchemesAsync(pendingContractId, cancellationToken);
        foreach (var scheme in schemes)
        {
            await pendingOrderRepository.DeleteSchemeAsync(scheme.PendingParticipantSchemeId, cancellationToken);
        }

        var decided = await pendingOrderRepository.MarkDecidedAsync(order.CustomerId, pendingContractId, cancellationToken);
        if (decided)
        {
            await decisionEmails.QueueAndSendAsync(order, Guid.Empty, comments, isApproved: false, cancellationToken);
        }

        return decided;
    }

    private static void Validate(string purchaseOrderNumber, string comments)
    {
        var errors = new Dictionary<string, string[]>();
        if (purchaseOrderNumber.Length > PurchaseOrderNumberMaxLength)
        {
            errors["PurchaseOrderNumber"] = [$"The purchase order number must be {PurchaseOrderNumberMaxLength} characters or fewer"];
        }

        if (comments.Length > CommentsMaxLength)
        {
            errors["Comments"] = ["The comments field allows maximum 2000 characters"];
        }

        if (errors.Count > 0)
        {
            throw new PendingOrderValidationException(errors);
        }
    }

    // Legacy GetCurrentUser(): the signed-in user's name lower-cased, "Guest" when nobody is signed in.
    private async Task<string> ApprovedByAsync(CancellationToken cancellationToken)
    {
        var name = await auditActor.GetAuditNameAsync(cancellationToken);
        return string.Equals(name, "Guest", StringComparison.Ordinal) ? name : name.ToLowerInvariant();
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
        var contract = new CoreContract
        {
            ContractId = Guid.NewGuid(),
            CustomerId = order.CustomerId,
            YearId = order.YearId,
            Suffix = await contractWriter.NextSuffixAsync(order.CustomerId, order.YearId, cancellationToken),
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

    private sealed record PricingContext(
        string QalNumber,
        string CountryType,
        IReadOnlyList<PostagePricingPlanEntity> PostagePlans,
        IReadOnlyDictionary<Guid, CoreScheme> RealSchemes);
}
