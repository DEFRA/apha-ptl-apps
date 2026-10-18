namespace PTL.Core.Distribution;

public partial interface IDistributionRepository
{
    // spgMonthlyDistributionSchemeForAmendResults. Null when the scheme is not returned.
    Task<AmendResultsSource?> GetAmendResultsAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default);

    // spgaRating with the rating text, in the procedure's order.
    Task<IReadOnlyList<AmendRatingOption>> GetRatingOptionsAsync(CancellationToken cancellationToken = default);

    // The legacy MonthlyDistributionScheme.DataPortal_Update chain (spiAudit* before each audited update).
    Task SaveAmendResultsAsync(AmendResultsWrite write, CancellationToken cancellationToken = default);
}
