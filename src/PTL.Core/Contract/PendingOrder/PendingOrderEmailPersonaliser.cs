using System.Globalization;
using PTL.Core.Lookup;
using PTL.Core.Participant;
using PTL.Core.Scheme;
using ICustomerRepository = PTL.Core.Customer.ICustomerRepository;

namespace PTL.Core.Contract.PendingOrder;

// Builds the GOV.UK Notify personalisation of one online contract order email.
public interface IPendingOrderEmailPersonaliser
{
    Task<Dictionary<string, string>> PersonaliseAsync(OnlineOrderEmailEntity email, CancellationToken cancellationToken = default);
}

public sealed class PendingOrderEmailPersonaliser(
    IPendingOrderRepository pendingOrderRepository,
    IContractRepository contractRepository,
    ISchemeRepository schemeRepository,
    ICustomerRepository customerRepository,
    IParticipantRepository participantRepository,
    ILookupRepository lookupRepository) : IPendingOrderEmailPersonaliser
{
    // Legacy getSchemeMonths lists the contract year from April; index 0 of ContractItemLine.Months is January.
    private static readonly int[] FinancialYearOrder = [3, 4, 5, 6, 7, 8, 9, 10, 11, 0, 1, 2];
    private static readonly string[] MonthNames = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    // The tokens of the legacy templates: <<ParticipantName>>, <<ApprovedSchemeOrders>>, <<Notes>> and the subject's
    // <<ContractYear>> / <<CustomerName>> (which legacy filled with the QAL number).
    public async Task<Dictionary<string, string>> PersonaliseAsync(OnlineOrderEmailEntity email, CancellationToken cancellationToken = default)
    {
        // A decided order stays in tblPendingContract (soft-deleted), so this also serves the declined path, where no contract exists.
        var order = await pendingOrderRepository.GetByIdAsync(email.PendingContractId, cancellationToken);
        var customer = order is null ? null : await customerRepository.GetByIdAsync(order.CustomerId, cancellationToken);
        var years = await lookupRepository.GetAllYearsAsync(cancellationToken);

        var personalisation = new Dictionary<string, string>
        {
            ["ParticipantName"] = order is null ? string.Empty : await FirstActiveLabCodeAsync(order.CustomerId, cancellationToken),
            ["CustomerName"] = customer?.QalNumber ?? string.Empty,
            ["status"] = email.IsApproved ? "Approved" : "Declined",
            ["ContractYear"] = years.FirstOrDefault(y => y.YearId == email.YearId)?.Year ?? "####",
            ["Notes"] = email.Notes,
        };

        if (email.IsApproved)
        {
            personalisation["ApprovedSchemeOrders"] = await ApprovedSchemeOrdersAsync(email.ContractId, cancellationToken);
        }

        return personalisation;
    }

    // Legacy intended the first active participant's lab code for both outcomes; its declined path never found one
    // (the pending schemes it searched had already been deleted).
    private async Task<string> FirstActiveLabCodeAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var participants = await participantRepository.GetSummariesAsync(customerId, false, cancellationToken);
        return participants.Count > 0 ? participants[0].LabCode : string.Empty;
    }

    // The legacy HTML table (PT Schemes / Month / Week) as the bullet list GOV.UK Notify can render.
    private async Task<string> ApprovedSchemeOrdersAsync(Guid contractId, CancellationToken cancellationToken)
    {
        var items = await contractRepository.GetContractItemsAsync(contractId, cancellationToken);
        if (items is null)
        {
            return string.Empty;
        }

        var lines = new List<string>(items.Schemes.Count);
        foreach (var group in items.Schemes)
        {
            var scheme = await schemeRepository.GetByIdAsync(group.SchemeId, cancellationToken);
            var months = FinancialYearOrder.Where(m => group.Participants.Any(p => p.Months[m])).Select(m => MonthNames[m]);
            var week = (scheme?.WeekNumber ?? 0).ToString(CultureInfo.InvariantCulture);
            lines.Add($"* {group.SchemeIdentifier}:{group.SchemeName} - Month: {string.Join(' ', months)} - Week: {week}");
        }

        return string.Join('\n', lines);
    }
}
