using PTL.Core.Participant;
using CoreContract = PTL.Core.Contract.Contract;

namespace PTL.Core.Contract.PendingOrder;

// Persists the contract (and its participant schemes) that approving a pending order creates.
public interface IPendingOrderContractWriter
{
    // Legacy getNewContractSuffix: "" for the first contract, then A..Z, then AA..AZ/BA.. up to 51.
    Task<string> NextSuffixAsync(Guid customerId, int yearId, CancellationToken cancellationToken = default);

    // Created via the repository rather than ContractService: the latter force-sets IsOnlineOrder to false and stamps
    // CommencementDate, neither of which legacy's approval path does. Removed lines are not carried over.
    Task<CoreContract> CreateAsync(CoreContract contract, IReadOnlyList<PendingOrderSchemeEntity> schemes, CancellationToken cancellationToken = default);
}

public sealed class PendingOrderContractWriter(
    IContractRepository contractRepository,
    IParticipantSchemeRepository participantSchemeRepository) : IPendingOrderContractWriter
{
    // Legacy getNewContractSuffix supports at most 51 contracts per customer per year.
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public async Task<string> NextSuffixAsync(Guid customerId, int yearId, CancellationToken cancellationToken = default)
    {
        var existingContracts = await contractRepository.GetSummariesByYearAsync(customerId, yearId, cancellationToken);
        return NextContractSuffix(existingContracts.Count);
    }

    public async Task<CoreContract> CreateAsync(CoreContract contract, IReadOnlyList<PendingOrderSchemeEntity> schemes, CancellationToken cancellationToken = default)
    {
        var created = await contractRepository.CreateAsync(contract, cancellationToken);

        foreach (var scheme in schemes.Where(s => !s.IsRemoved))
        {
            await participantSchemeRepository.CreateAsync(ToParticipantScheme(created.ContractId, scheme), cancellationToken);
        }

        return created;
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

    private static string NextContractSuffix(int existingContractCount) => existingContractCount switch
    {
        0 => string.Empty,
        < 27 => Alphabet[existingContractCount - 1].ToString(),
        < 52 => $"{Alphabet[((existingContractCount - 1) / 26) - 1]}{Alphabet[(existingContractCount - 1) % 26]}",
        _ => string.Empty
    };
}
