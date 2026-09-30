namespace PTL.Core.Contract.PendingOrder;

// Row shape returned by spgaPendingContracts - every pending contract order joined with its
// customer (legacy ReviewPendingOrders.aspx, which filters by year and submitted/deleted in memory).
public class PendingOrderSummaryEntity
{
    public Guid PendingContractId { get; set; }
    public Guid CustomerId { get; set; }
    public int YearId { get; set; }
    public bool IsSubmitted { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? OrderSubmitDate { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string QalNumber { get; set; } = string.Empty;

    // Not a result column - resolved from tblYear by PendingOrderService for display.
    public string Year { get; set; } = string.Empty;
}

// Row shape returned by spgPendingContractByPendingContractId (tblPendingContract joined with
// tblCustomer and tblCurrency).
public class PendingOrderEntity
{
    public Guid PendingContractId { get; set; }
    public Guid CustomerId { get; set; }
    public int YearId { get; set; }
    public bool IsSubmitted { get; set; }
    public bool IsDeleted { get; set; }
    public string PurchaseOrderNumber { get; set; } = string.Empty;
    public DateTime? OrderSubmitDate { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CurrencySymbol { get; set; } = string.Empty;
}

// Row shape returned by spgPendingParticipantSchemeByPendingContractId. fldPrice comes from the
// SQL function fnGetPendingParticipantSchemePrice, so scheme pricing needs no C# equivalent.
public class PendingOrderSchemeEntity
{
    public Guid PendingParticipantSchemeId { get; set; }
    public Guid PendingContractId { get; set; }
    public Guid ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public Guid SchemeId { get; set; }
    public string SchemeName { get; set; } = string.Empty;
    public string SchemeIdentifier { get; set; } = string.Empty;
    public bool DistributionMonthJan { get; set; }
    public bool DistributionMonthFeb { get; set; }
    public bool DistributionMonthMar { get; set; }
    public bool DistributionMonthApr { get; set; }
    public bool DistributionMonthMay { get; set; }
    public bool DistributionMonthJun { get; set; }
    public bool DistributionMonthJul { get; set; }
    public bool DistributionMonthAug { get; set; }
    public bool DistributionMonthSep { get; set; }
    public bool DistributionMonthOct { get; set; }
    public bool DistributionMonthNov { get; set; }
    public bool DistributionMonthDec { get; set; }
    public bool ImportExportLicenceRequired { get; set; }
    public bool IsSelected { get; set; }
    public bool IsRemoved { get; set; }
    public bool DataConsentDeclarationGiven { get; set; }
    public decimal Price { get; set; }

    // fldCanEditJan..Dec (dbo.fnIsDistributionNotPosted) - legacy's scheme.CanEditXParticipants.
    public bool CanEditJan { get; set; } = true;
    public bool CanEditFeb { get; set; } = true;
    public bool CanEditMar { get; set; } = true;
    public bool CanEditApr { get; set; } = true;
    public bool CanEditMay { get; set; } = true;
    public bool CanEditJun { get; set; } = true;
    public bool CanEditJul { get; set; } = true;
    public bool CanEditAug { get; set; } = true;
    public bool CanEditSep { get; set; } = true;
    public bool CanEditOct { get; set; } = true;
    public bool CanEditNov { get; set; } = true;
    public bool CanEditDec { get; set; } = true;

    // fldDistributionMonthXxxIsContracted - the participant is already contracted for that month,
    // so legacy shows it ticked, locked and highlighted. Only the single-row fetch returns these.
    public bool IsContractedJan { get; set; }
    public bool IsContractedFeb { get; set; }
    public bool IsContractedMar { get; set; }
    public bool IsContractedApr { get; set; }
    public bool IsContractedMay { get; set; }
    public bool IsContractedJun { get; set; }
    public bool IsContractedJul { get; set; }
    public bool IsContractedAug { get; set; }
    public bool IsContractedSep { get; set; }
    public bool IsContractedOct { get; set; }
    public bool IsContractedNov { get; set; }
    public bool IsContractedDec { get; set; }

    public IEnumerable<(int MonthNumber, bool Selected)> Months() =>
    [
        (1, DistributionMonthJan), (2, DistributionMonthFeb), (3, DistributionMonthMar),
        (4, DistributionMonthApr), (5, DistributionMonthMay), (6, DistributionMonthJun),
        (7, DistributionMonthJul), (8, DistributionMonthAug), (9, DistributionMonthSep),
        (10, DistributionMonthOct), (11, DistributionMonthNov), (12, DistributionMonthDec)
    ];

    // Financial-year order (Apr..Mar) - the column order of the legacy scheme grid.
    public IEnumerable<(string Label, int MonthNumber, bool Selected, bool CanEdit, bool IsContracted)> FinancialYearMonths() =>
    [
        ("Apr", 4, DistributionMonthApr, CanEditApr, IsContractedApr),
        ("May", 5, DistributionMonthMay, CanEditMay, IsContractedMay),
        ("Jun", 6, DistributionMonthJun, CanEditJun, IsContractedJun),
        ("Jul", 7, DistributionMonthJul, CanEditJul, IsContractedJul),
        ("Aug", 8, DistributionMonthAug, CanEditAug, IsContractedAug),
        ("Sep", 9, DistributionMonthSep, CanEditSep, IsContractedSep),
        ("Oct", 10, DistributionMonthOct, CanEditOct, IsContractedOct),
        ("Nov", 11, DistributionMonthNov, CanEditNov, IsContractedNov),
        ("Dec", 12, DistributionMonthDec, CanEditDec, IsContractedDec),
        ("Jan", 1, DistributionMonthJan, CanEditJan, IsContractedJan),
        ("Feb", 2, DistributionMonthFeb, CanEditFeb, IsContractedFeb),
        ("Mar", 3, DistributionMonthMar, CanEditMar, IsContractedMar)
    ];
}
