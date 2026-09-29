using PTL.Contracts.Contract;
using PTL.Core.Lookup;
using PTL.Core.Participant;
using PTL.Core.Scheme;

namespace PTL.Core.Contract.Renew;

// Domain projection for one current-year contract eligible for renewal - mirrors legacy
// PtaBusinessObjects.BusinessObjects.Contracts.ContractMergeInfo (spgContractMerge result set 1),
// backing the screen users know as "Renew Contracts" (internally MergeContracts.aspx).
public sealed class RenewableContractEntity
{
    public Guid ContractId { get; set; }
    public string Suffix { get; set; } = string.Empty;
    public string ContractSignatory { get; set; } = string.Empty;
    public string RenewalInformation { get; set; } = string.Empty;
    public string ActionsRequired { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int NoOfItems { get; set; }
}

// Mirrors legacy ParticipantSchemeMergeInfo (spgContractMerge result set 2) - one participant-scheme
// row on a renewable contract, with its next-year scheme (if any).
public sealed class RenewableContractItemEntity
{
    public Guid ContractId { get; set; }
    public string Suffix { get; set; } = string.Empty;
    public Guid ParticipantSchemeId { get; set; }
    public string LabCode { get; set; } = string.Empty;
    public string LabName { get; set; } = string.Empty;
    public string OldSchemeIdentifier { get; set; } = string.Empty;
    public string OldSchemeName { get; set; } = string.Empty;
    public string? NewSchemeIdentifier { get; set; }
    public string? NewSchemeName { get; set; }

    // Legacy MergeContractsViewModel: isNewSchemeAvailable = NewSchemeIdentifier <> String.Empty.
    public bool IsRenewable => !string.IsNullOrEmpty(NewSchemeIdentifier);

    // Legacy ParticpiantSchemeViewModel.Identifier - duplicate identifiers across merged contracts
    // collapse into a single display row.
    public string Identifier => LabCode + OldSchemeIdentifier;
}

public sealed record RenewEligibility(bool IsAllowed, string? BlockedReason);

public sealed record RenewableContractsResult(
    RenewEligibility Eligibility,
    IReadOnlyList<string> ExistingSignatories,
    IReadOnlyList<RenewableContractEntity> Contracts);

public sealed record RenewContractsResult(bool Success, Guid? NewContractId, string? ErrorMessage);

public interface IRenewContractsService
{
    Task<RenewableContractsResult> GetRenewableContractsAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RenewableContractItemEntity>> GetRenewableItemsAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task<RenewContractsResult> RenewContractsAsync(
        Guid customerId,
        IReadOnlyList<Guid> contractIds,
        IReadOnlyList<Guid> participantSchemeIds,
        string? newContractSignatory,
        CancellationToken cancellationToken = default);
}

// Legacy: PtaBusinessObjects.Services.Contracts.ContractMergeService (IsMergeAllowed/MergeContracts/
// Execute) together with Contracts_Admin_MergeContracts.Page_Load.
public sealed class RenewContractsService(
    IContractMergeRepository contractMergeRepository,
    IContractRepository contractRepository,
    IParticipantSchemeRepository participantSchemeRepository,
    ISchemeRepository schemeRepository,
    ILookupRepository lookupRepository) : IRenewContractsService
{
    // Exact legacy panel text (PnlNoContracts/PnlNotAllowed/PnlNoParticipantSchemes) - preserve verbatim.
    public const string NoCurrentYearContractsMessage = "There are no Contracts for the current year.";
    public const string NextYearContractsExistMessage = "The merging of existing Contracts is not allowed as Contracts already exist for next year.";
    public const string NoItemsMessage = "There are no Items on the selected Contracts.";
    public const string WeightedPricingUnavailableMessage = "This contract can not be renewed because one or more of the selected contract items have weighted pricing plan and the weighted pricing plan for the next year does not exist.";

    public async Task<RenewableContractsResult> GetRenewableContractsAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        // Legacy Page_Load evaluates IsMergeAllowed BEFORE the contract-count check.
        if (!await IsMergeAllowedAsync(customerId, cancellationToken))
        {
            return new RenewableContractsResult(new RenewEligibility(false, NextYearContractsExistMessage), [], []);
        }

        var merge = await contractMergeRepository.GetContractMergeInfoAsync(customerId, cancellationToken);
        if (merge.Contracts.Count == 0)
        {
            return new RenewableContractsResult(new RenewEligibility(false, NoCurrentYearContractsMessage), [], []);
        }

        // Legacy LoadDropDownSignatory: one item per distinct signatory, in contract order. An empty
        // signatory matches the pre-existing "Not Specified" (value "") item and is never added.
        var existingSignatories = merge.Contracts
            .Select(c => c.ContractSignatory)
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return new RenewableContractsResult(new RenewEligibility(true, null), existingSignatories, merge.Contracts);
    }

    public async Task<IReadOnlyList<RenewableContractItemEntity>> GetRenewableItemsAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var merge = await contractMergeRepository.GetContractMergeInfoAsync(customerId, cancellationToken);
        return merge.ParticipantSchemes;
    }

    public async Task<RenewContractsResult> RenewContractsAsync(
        Guid customerId,
        IReadOnlyList<Guid> contractIds,
        IReadOnlyList<Guid> participantSchemeIds,
        string? newContractSignatory,
        CancellationToken cancellationToken = default)
    {
        var merge = await contractMergeRepository.GetContractMergeInfoAsync(customerId, cancellationToken);
        var contractById = merge.Contracts.ToDictionary(c => c.ContractId);
        var suffixByParticipantSchemeId = merge.ParticipantSchemes.ToDictionary(p => p.ParticipantSchemeId, p => p.Suffix);

        // Ordered by suffix so that newer contracts overwrite older ones where the participant
        // ordered the same scheme more than once (legacy ContractMergeService.MergeContracts).
        var orderedParticipantSchemeIds = participantSchemeIds
            .Where(suffixByParticipantSchemeId.ContainsKey)
            .OrderBy(id => suffixByParticipantSchemeId[id], StringComparer.Ordinal)
            .ToList();

        var actionsRequired = string.Empty;
        var renewalInformation = string.Empty;
        foreach (var contractId in contractIds.Where(contractById.ContainsKey))
        {
            actionsRequired += contractById[contractId].ActionsRequired + Environment.NewLine;
            renewalInformation += contractById[contractId].RenewalInformation + Environment.NewLine;
        }

        var systemSettings = await lookupRepository.GetSystemSettingsAsync(cancellationToken);
        var yearId = systemSettings.NextYearWithDelayId;

        var newRecords = new List<ParticipantSchemeRecord>();
        foreach (var participantSchemeId in orderedParticipantSchemeIds)
        {
            var oldRecord = await participantSchemeRepository.GetByIdAsync(participantSchemeId, cancellationToken);
            if (oldRecord is null)
            {
                continue;
            }

            // Legacy SchemeInfoCollection.FetchSchemeInfoCollectionBySchemeId(...)(0).NextSchemeId.
            var schemeInfo = await schemeRepository.GetSummariesBySchemeIdAsync(oldRecord.SchemeId, cancellationToken);
            var newSchemeId = schemeInfo.Count > 0 ? schemeInfo[0].NextSchemeId : null;
            if (newSchemeId is null)
            {
                continue;
            }

            var newScheme = await schemeRepository.GetByIdAsync(newSchemeId.Value, cancellationToken);
            if (newScheme is null)
            {
                continue;
            }

            var existing = newRecords.SingleOrDefault(r => r.SchemeId == newScheme.SchemeId && r.ParticipantId == oldRecord.ParticipantId);
            if (existing is not null)
            {
                UpdateParticipantScheme(existing, oldRecord, newScheme);
            }
            else
            {
                newRecords.Add(CreateNewParticipantScheme(oldRecord, newScheme));
            }
        }

        // Legacy checks weighted pricing once the full set of new participant schemes is built,
        // immediately before saving anything.
        var weightedPricingYears = await lookupRepository.GetWeightedPricingYearsAsync(cancellationToken);
        var isWeightedPricingAvailable = weightedPricingYears.Any(y => y.YearId == yearId);
        if (newRecords.Any(r => r.IsWeightedPricing) && !isWeightedPricingAvailable)
        {
            return new RenewContractsResult(false, null, WeightedPricingUnavailableMessage);
        }

        var newContract = new Contract
        {
            ContractId = Guid.NewGuid(),
            CustomerId = customerId,
            YearId = yearId,
            UTNumber = systemSettings.UTNumber,
            ContractSignatory = newContractSignatory ?? string.Empty,
            ActionsRequired = actionsRequired,
            RenewalInformation = renewalInformation,
            CommencementDate = new DateTime(yearId, 4, 1),
            IsActive = true
        };

        // Legacy Execute inserts the contract directly (Contract.Insert), bypassing the interactive
        // Contract.aspx validation path - so ContractService/ContractValidator are not involved here.
        var createdContract = await contractRepository.CreateAsync(newContract, cancellationToken);

        foreach (var record in newRecords)
        {
            record.ContractId = createdContract.ContractId;
            await participantSchemeRepository.CreateAsync(record, cancellationToken);
        }

        return new RenewContractsResult(true, createdContract.ContractId, null);
    }

    // Legacy ContractMergeService.IsMergeAllowed.
    private async Task<bool> IsMergeAllowedAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var systemSettings = await lookupRepository.GetSystemSettingsAsync(cancellationToken);
        var allContracts = await contractRepository.GetSummariesAsync(customerId, ContractPeriodFilter.All, cancellationToken);
        return !allContracts.Any(c => c.YearId >= systemSettings.NextYearWithDelayId);
    }

    // Legacy ContractMergeService.CreateNewParticipantScheme - each distribution month is the old
    // month AND the new scheme's month; every other field is copied verbatim.
    private static ParticipantSchemeRecord CreateNewParticipantScheme(ParticipantSchemeRecord oldRecord, Scheme.Scheme newScheme) => new()
    {
        ParticipantId = oldRecord.ParticipantId,
        SchemeId = newScheme.SchemeId,
        GroupAddressId = oldRecord.GroupAddressId,
        DistributionMonthJan = oldRecord.DistributionMonthJan && newScheme.DistributionMonthJan,
        DistributionMonthFeb = oldRecord.DistributionMonthFeb && newScheme.DistributionMonthFeb,
        DistributionMonthMar = oldRecord.DistributionMonthMar && newScheme.DistributionMonthMar,
        DistributionMonthApr = oldRecord.DistributionMonthApr && newScheme.DistributionMonthApr,
        DistributionMonthMay = oldRecord.DistributionMonthMay && newScheme.DistributionMonthMay,
        DistributionMonthJun = oldRecord.DistributionMonthJun && newScheme.DistributionMonthJun,
        DistributionMonthJul = oldRecord.DistributionMonthJul && newScheme.DistributionMonthJul,
        DistributionMonthAug = oldRecord.DistributionMonthAug && newScheme.DistributionMonthAug,
        DistributionMonthSep = oldRecord.DistributionMonthSep && newScheme.DistributionMonthSep,
        DistributionMonthOct = oldRecord.DistributionMonthOct && newScheme.DistributionMonthOct,
        DistributionMonthNov = oldRecord.DistributionMonthNov && newScheme.DistributionMonthNov,
        DistributionMonthDec = oldRecord.DistributionMonthDec && newScheme.DistributionMonthDec,
        ExternalReference = oldRecord.ExternalReference,
        NumberOfSetsRequired = oldRecord.NumberOfSetsRequired,
        CustomsCertificateRequired = oldRecord.CustomsCertificateRequired,
        ImportExportLicenceRequired = oldRecord.ImportExportLicenceRequired,
        NonFeePaying = oldRecord.NonFeePaying,
        PackingInstructions = oldRecord.PackingInstructions,
        Contact = oldRecord.Contact,
        IsWeightedPricing = oldRecord.IsWeightedPricing
    };

    // Legacy ContractMergeService.UpdateParticipantScheme - a month is only ever turned ON, never
    // cleared; every other field is overwritten by the later (higher-suffix) contract's values.
    private static void UpdateParticipantScheme(ParticipantSchemeRecord target, ParticipantSchemeRecord oldRecord, Scheme.Scheme newScheme)
    {
        target.DistributionMonthJan |= oldRecord.DistributionMonthJan && newScheme.DistributionMonthJan;
        target.DistributionMonthFeb |= oldRecord.DistributionMonthFeb && newScheme.DistributionMonthFeb;
        target.DistributionMonthMar |= oldRecord.DistributionMonthMar && newScheme.DistributionMonthMar;
        target.DistributionMonthApr |= oldRecord.DistributionMonthApr && newScheme.DistributionMonthApr;
        target.DistributionMonthMay |= oldRecord.DistributionMonthMay && newScheme.DistributionMonthMay;
        target.DistributionMonthJun |= oldRecord.DistributionMonthJun && newScheme.DistributionMonthJun;
        target.DistributionMonthJul |= oldRecord.DistributionMonthJul && newScheme.DistributionMonthJul;
        target.DistributionMonthAug |= oldRecord.DistributionMonthAug && newScheme.DistributionMonthAug;
        target.DistributionMonthSep |= oldRecord.DistributionMonthSep && newScheme.DistributionMonthSep;
        target.DistributionMonthOct |= oldRecord.DistributionMonthOct && newScheme.DistributionMonthOct;
        target.DistributionMonthNov |= oldRecord.DistributionMonthNov && newScheme.DistributionMonthNov;
        target.DistributionMonthDec |= oldRecord.DistributionMonthDec && newScheme.DistributionMonthDec;

        target.ExternalReference = oldRecord.ExternalReference;
        target.NumberOfSetsRequired = oldRecord.NumberOfSetsRequired;
        target.CustomsCertificateRequired = oldRecord.CustomsCertificateRequired;
        target.ImportExportLicenceRequired = oldRecord.ImportExportLicenceRequired;
        target.NonFeePaying = oldRecord.NonFeePaying;
        target.PackingInstructions = oldRecord.PackingInstructions;
        target.Contact = oldRecord.Contact;
        target.IsWeightedPricing = oldRecord.IsWeightedPricing;
        target.GroupAddressId = oldRecord.GroupAddressId;
    }
}
