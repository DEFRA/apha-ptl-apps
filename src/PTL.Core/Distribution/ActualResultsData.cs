namespace PTL.Core.Distribution;

// spgActualResults result set 1 - legacy ActualResultsSet.DataPortal_Fetch's header row. A null
// FirstEditDate means no results have ever been saved for the participant (legacy MarkNew).
public sealed class ActualResultsHeaderEntity
{
    public DateTime? FirstEditDate { get; set; }
    public DateTime? LastEditDate { get; set; }
    public string Distribution { get; set; } = string.Empty;
    public string SchemeIdentifier { get; set; } = string.Empty;
    public string SchemeName { get; set; } = string.Empty;
    public DateTime DeadlineDate { get; set; }
}

// spgActualResults result set 2 (tlnkActualResultItemResult).
public sealed class ActualSampleResultEntity
{
    public Guid SampleResultItemId { get; set; }
    public string? Result { get; set; }
}

// spgActualResults result set 3 (tlnkActualMethodItemResult).
public sealed class ActualMethodItemResultEntity
{
    public Guid TestMethodItemId { get; set; }
    public string? Result { get; set; }
}

public sealed record ActualResultsData(
    ActualResultsHeaderEntity Header,
    IReadOnlyList<ActualSampleResultEntity> SampleResults,
    IReadOnlyList<ActualMethodItemResultEntity> MethodItemResults);

// One result row to write. IsInsert = no stored row yet (legacy IsNew child -> spiActual*Result);
// otherwise a changed value (legacy IsDirty child -> spiAuditActual* then spuActual*Result).
public sealed record ActualResultWriteItem(Guid ItemId, string Result, bool IsInsert);

// Everything one legacy ActualResultsSet.Save() writes. Audit rows are only written for changed
// existing rows when AuditWhy is supplied (legacy Audit.aspx runs only for a non-new set).
public sealed record ActualResultsWrite(
    Guid DistributionParticipantId,
    bool IsNew,
    DateTime FirstEditDate,
    DateTime LastEditDate,
    IReadOnlyList<ActualResultWriteItem> MethodItems,
    IReadOnlyList<ActualResultWriteItem> SampleItems,
    IReadOnlyList<Guid> RatingSampleIds,
    Guid? DefaultRatingId,
    string AuditWho,
    string AuditWhy);
