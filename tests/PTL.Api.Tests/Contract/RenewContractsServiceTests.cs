using PTL.Core.Contract;
using PTL.Core.Contract.Renew;
using PTL.Core.Lookup;
using PTL.Core.Participant;
using PTL.Core.Scheme;
using ContractPeriod = PTL.Contracts.Contract.ContractPeriodFilter;
using CoreContract = PTL.Core.Contract.Contract;
using CoreScheme = PTL.Core.Scheme.Scheme;

namespace PTL.Api.Tests.Contract;

public sealed class RenewContractsServiceTests
{
    [Fact]
    public async Task GetRenewableContractsAsync_WhenContractsExist_ReturnsAllowedEligibilityAndDistinctSignatories()
    {
        var customerId = Guid.NewGuid();
        var contractSummaries = new[]
        {
            new ContractSummaryEntity { ContractId = Guid.NewGuid(), CustomerId = customerId, YearId = 2026, IsActive = true, Suffix = "A" },
            new ContractSummaryEntity { ContractId = Guid.NewGuid(), CustomerId = customerId, YearId = 2026, IsActive = true, Suffix = "B" }
        };

        var merge = new ContractMergeData(
        [
            new RenewableContractEntity { ContractId = contractSummaries[0].ContractId, ContractSignatory = "Alice Example", IsActive = true, NoOfItems = 2 },
            new RenewableContractEntity { ContractId = contractSummaries[1].ContractId, ContractSignatory = "Alice Example", IsActive = true, NoOfItems = 1 },
            new RenewableContractEntity { ContractId = Guid.NewGuid(), ContractSignatory = string.Empty, IsActive = true, NoOfItems = 3 }
        ],
        []);

        var (service, _) = CreateService(merge, contractSummaries, settings: new SystemSettingsEntity { NextYearWithDelayId = 2027 });

        var result = await service.GetRenewableContractsAsync(customerId);

        Assert.True(result.Eligibility.IsAllowed);
        Assert.Equal(["Alice Example"], result.ExistingSignatories);
        Assert.Equal(3, result.Contracts.Count);
    }

    [Fact]
    public async Task GetRenewableContractsAsync_WhenNextYearContractExists_ReturnsBlockedReason()
    {
        var customerId = Guid.NewGuid();
        var contractSummaries = new[]
        {
            new ContractSummaryEntity { ContractId = Guid.NewGuid(), CustomerId = customerId, YearId = 2027, IsActive = true, Suffix = "A" }
        };

        var (service, _) = CreateService(
            contractSummaries: contractSummaries,
            settings: new SystemSettingsEntity { NextYearWithDelayId = 2027 });

        var result = await service.GetRenewableContractsAsync(customerId);

        Assert.False(result.Eligibility.IsAllowed);
        Assert.Equal(RenewContractsService.NextYearContractsExistMessage, result.Eligibility.BlockedReason);
    }

    [Fact]
    public async Task GetRenewableContractsAsync_WhenMergeAllowedButNoCurrentYearContracts_ReturnsNoContractsBlockedReason()
    {
        var customerId = Guid.NewGuid();
        var merge = new ContractMergeData([], []);

        var (service, _) = CreateService(
            merge,
            contractSummaries: [],
            settings: new SystemSettingsEntity { NextYearWithDelayId = 2027 });

        var result = await service.GetRenewableContractsAsync(customerId);

        Assert.False(result.Eligibility.IsAllowed);
        Assert.Equal(RenewContractsService.NoCurrentYearContractsMessage, result.Eligibility.BlockedReason);
        Assert.Empty(result.Contracts);
    }

    [Fact]
    public async Task RenewContractsAsync_WhenDuplicateSelectionIsMerged_UsesMergedDistributionMonthsAndCreatesOneNewParticipantScheme()
    {
        var customerId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var oldSchemeId = Guid.NewGuid();
        var nextSchemeId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var participantSchemeId1 = Guid.NewGuid();
        var participantSchemeId2 = Guid.NewGuid();

        var oldScheme = new CoreScheme
        {
            SchemeId = oldSchemeId,
            SharedId = Guid.NewGuid(),
            YearId = 2026,
            Identifier = "OLD-001",
            Name = "Old Scheme",
            DistributionMonthJan = true,
            DistributionMonthFeb = false,
            DistributionMonthMar = true,
            DistributionMonthApr = false,
            DistributionMonthMay = false,
            DistributionMonthJun = false,
            DistributionMonthJul = false,
            DistributionMonthAug = false,
            DistributionMonthSep = false,
            DistributionMonthOct = false,
            DistributionMonthNov = false,
            DistributionMonthDec = false
        };

        var nextScheme = new CoreScheme
        {
            SchemeId = nextSchemeId,
            SharedId = oldScheme.SharedId,
            YearId = 2027,
            Identifier = "NEXT-001",
            Name = "Next Scheme",
            DistributionMonthJan = true,
            DistributionMonthFeb = true,
            DistributionMonthMar = true,
            DistributionMonthApr = false,
            DistributionMonthMay = false,
            DistributionMonthJun = false,
            DistributionMonthJul = false,
            DistributionMonthAug = false,
            DistributionMonthSep = false,
            DistributionMonthOct = false,
            DistributionMonthNov = false,
            DistributionMonthDec = false
        };

        var merge = new ContractMergeData(
        [
            new RenewableContractEntity
            {
                ContractId = contractId,
                ContractSignatory = "Alice Example",
                ActionsRequired = "Review",
                RenewalInformation = "Renewal note",
                IsActive = true,
                NoOfItems = 2
            }
        ],
        [
            new RenewableContractItemEntity { ContractId = contractId, Suffix = "A", ParticipantSchemeId = participantSchemeId1, LabCode = "LAB", OldSchemeIdentifier = "OLD-001", OldSchemeName = "Old Scheme", NewSchemeIdentifier = nextScheme.Identifier, NewSchemeName = nextScheme.Name },
            new RenewableContractItemEntity { ContractId = contractId, Suffix = "B", ParticipantSchemeId = participantSchemeId2, LabCode = "LAB", OldSchemeIdentifier = "OLD-001", OldSchemeName = "Old Scheme", NewSchemeIdentifier = nextScheme.Identifier, NewSchemeName = nextScheme.Name }
        ]);

        var participantRecords = new[]
        {
            new ParticipantSchemeRecord
            {
                ParticipantSchemeId = participantSchemeId1,
                ContractId = contractId,
                ParticipantId = participantId,
                SchemeId = oldSchemeId,
                DistributionMonthJan = true,
                DistributionMonthFeb = false,
                DistributionMonthMar = true,
                GroupAddressId = Guid.NewGuid(),
                IsWeightedPricing = false,
                Contact = "A contact"
            },
            new ParticipantSchemeRecord
            {
                ParticipantSchemeId = participantSchemeId2,
                ContractId = contractId,
                ParticipantId = participantId,
                SchemeId = oldSchemeId,
                DistributionMonthJan = false,
                DistributionMonthFeb = true,
                DistributionMonthMar = false,
                GroupAddressId = Guid.NewGuid(),
                IsWeightedPricing = false,
                Contact = "B contact"
            }
        };

        var (service, participantRepository) = CreateService(
            merge,
            [new ContractSummaryEntity { ContractId = contractId, CustomerId = customerId, YearId = 2026, IsActive = true, Suffix = "A" }],
            participantRecords,
            [oldScheme, nextScheme],
            new SystemSettingsEntity { NextYearWithDelayId = 2027, UTNumber = "UT3/306" });

        var result = await service.RenewContractsAsync(customerId, [contractId], [participantSchemeId1, participantSchemeId2], "Alice Example");

        Assert.True(result.Success);
        Assert.NotNull(result.NewContractId);

        var createdParticipantSchemes = participantRepository.CreatedRecords;
        Assert.Single(createdParticipantSchemes);
        Assert.NotEqual(Guid.Empty, createdParticipantSchemes[0].ParticipantSchemeId);
        Assert.DoesNotContain(createdParticipantSchemes[0].ParticipantSchemeId, new[] { participantSchemeId1, participantSchemeId2 });
        Assert.Equal(result.NewContractId, createdParticipantSchemes[0].ContractId);
        Assert.Equal(nextSchemeId, createdParticipantSchemes[0].SchemeId);
        Assert.Equal(participantId, createdParticipantSchemes[0].ParticipantId);
        Assert.True(createdParticipantSchemes[0].DistributionMonthJan);
        Assert.True(createdParticipantSchemes[0].DistributionMonthFeb);
        Assert.True(createdParticipantSchemes[0].DistributionMonthMar);
    }

    // Each renewed item is a new tlnkParticipantScheme row, so each needs its own new primary key.
    [Fact]
    public async Task RenewContractsAsync_RenewingSeveralItems_GivesEachNewRecordItsOwnId()
    {
        var customerId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var oldScheme = new CoreScheme { SchemeId = Guid.NewGuid(), SharedId = Guid.NewGuid(), YearId = 2026, Identifier = "OLD-010", Name = "Old" };
        var nextScheme = new CoreScheme { SchemeId = Guid.NewGuid(), SharedId = oldScheme.SharedId, YearId = 2027, Identifier = "NEXT-010", Name = "Next" };
        var first = new ParticipantSchemeRecord { ParticipantSchemeId = Guid.NewGuid(), ContractId = contractId, ParticipantId = Guid.NewGuid(), SchemeId = oldScheme.SchemeId };
        var second = new ParticipantSchemeRecord { ParticipantSchemeId = Guid.NewGuid(), ContractId = contractId, ParticipantId = Guid.NewGuid(), SchemeId = oldScheme.SchemeId };
        var merge = new ContractMergeData(
        [
            new RenewableContractEntity { ContractId = contractId, Suffix = "A" }
        ],
        [
            new RenewableContractItemEntity { ContractId = contractId, Suffix = "A", ParticipantSchemeId = first.ParticipantSchemeId, LabCode = "L1", OldSchemeIdentifier = "OLD-010", NewSchemeIdentifier = "NEXT-010" },
            new RenewableContractItemEntity { ContractId = contractId, Suffix = "A", ParticipantSchemeId = second.ParticipantSchemeId, LabCode = "L2", OldSchemeIdentifier = "OLD-010", NewSchemeIdentifier = "NEXT-010" }
        ]);
        var (service, participantRepository) = CreateService(
            merge,
            [new ContractSummaryEntity { ContractId = contractId, CustomerId = customerId, YearId = 2026, IsActive = true, Suffix = "A" }],
            [first, second],
            [oldScheme, nextScheme],
            new SystemSettingsEntity { NextYearWithDelayId = 2027, UTNumber = "UT3/306" });

        var result = await service.RenewContractsAsync(customerId, [contractId], [first.ParticipantSchemeId, second.ParticipantSchemeId], null);

        Assert.True(result.Success);
        var ids = participantRepository.CreatedRecords.Select(r => r.ParticipantSchemeId).ToList();
        Assert.Equal(2, ids.Count);
        Assert.DoesNotContain(Guid.Empty, ids);
        Assert.Equal(2, ids.Distinct().Count());
    }

    [Fact]
    public async Task RenewContractsAsync_WhenWeightedPricingIsUnavailable_ReturnsBlockedError()
    {
        var customerId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var oldSchemeId = Guid.NewGuid();
        var nextSchemeId = Guid.NewGuid();
        var participantSchemeId = Guid.NewGuid();

        var oldScheme = new CoreScheme { SchemeId = oldSchemeId, SharedId = Guid.NewGuid(), YearId = 2026, Identifier = "OLD-002", Name = "Old Weighted" };
        var nextScheme = new CoreScheme { SchemeId = nextSchemeId, SharedId = oldScheme.SharedId, YearId = 2027, Identifier = "NEXT-002", Name = "Next Weighted" };

        var merge = new ContractMergeData(
        [
            new RenewableContractEntity { ContractId = contractId, ContractSignatory = "Alice Example", ActionsRequired = "Review", RenewalInformation = "Renewal note" }
        ],
        [
            new RenewableContractItemEntity { ContractId = contractId, Suffix = "A", ParticipantSchemeId = participantSchemeId, LabCode = "LAB", OldSchemeIdentifier = "OLD-002", OldSchemeName = "Old Weighted", NewSchemeIdentifier = nextScheme.Identifier, NewSchemeName = nextScheme.Name }
        ]);

        var (service, _) = CreateService(
            merge,
            [new ContractSummaryEntity { ContractId = contractId, CustomerId = customerId, YearId = 2026, IsActive = true, Suffix = "A" }],
            [
                new ParticipantSchemeRecord
                {
                    ParticipantSchemeId = participantSchemeId,
                    ContractId = contractId,
                    ParticipantId = Guid.NewGuid(),
                    SchemeId = oldSchemeId,
                    DistributionMonthJan = true,
                    IsWeightedPricing = true,
                    GroupAddressId = Guid.NewGuid()
                }
            ],
            [oldScheme, nextScheme],
            new SystemSettingsEntity { NextYearWithDelayId = 2027, UTNumber = "UT3/306" },
            []);

        var result = await service.RenewContractsAsync(customerId, [contractId], [participantSchemeId], "Alice Example");

        Assert.False(result.Success);
        Assert.Null(result.NewContractId);
        Assert.Equal(RenewContractsService.WeightedPricingUnavailableMessage, result.ErrorMessage);
    }

    [Fact]
    public async Task RenewContractsAsync_WhenSelectedParticipantSchemeRecordIsMissing_SkipsItAndStillSucceeds()
    {
        var customerId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var missingParticipantSchemeId = Guid.NewGuid();

        var merge = new ContractMergeData(
        [
            new RenewableContractEntity { ContractId = contractId, ContractSignatory = "Alice Example" }
        ],
        [
            new RenewableContractItemEntity { ContractId = contractId, Suffix = "A", ParticipantSchemeId = missingParticipantSchemeId, LabCode = "LAB", OldSchemeIdentifier = "OLD-003", OldSchemeName = "Old Scheme" }
        ]);

        // No ParticipantSchemeRecord seeded for missingParticipantSchemeId - GetByIdAsync returns null.
        var (service, participantRepository) = CreateService(
            merge,
            [new ContractSummaryEntity { ContractId = contractId, CustomerId = customerId, YearId = 2026, IsActive = true, Suffix = "A" }],
            [],
            [],
            new SystemSettingsEntity { NextYearWithDelayId = 2027, UTNumber = "UT3/306" });

        var result = await service.RenewContractsAsync(customerId, [contractId], [missingParticipantSchemeId], "Alice Example");

        Assert.True(result.Success);
        Assert.Empty(participantRepository.CreatedRecords);
    }

    [Fact]
    public async Task RenewContractsAsync_WhenNoNextYearSchemeExists_SkipsThatSelectionAndStillSucceeds()
    {
        var customerId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var oldSchemeId = Guid.NewGuid();
        var participantSchemeId = Guid.NewGuid();

        // Only the old scheme exists - GetSummariesBySchemeIdAsync resolves no NextSchemeId.
        var oldScheme = new CoreScheme { SchemeId = oldSchemeId, SharedId = Guid.NewGuid(), YearId = 2026, Identifier = "OLD-004", Name = "Old Scheme" };

        var merge = new ContractMergeData(
        [
            new RenewableContractEntity { ContractId = contractId, ContractSignatory = "Alice Example" }
        ],
        [
            new RenewableContractItemEntity { ContractId = contractId, Suffix = "A", ParticipantSchemeId = participantSchemeId, LabCode = "LAB", OldSchemeIdentifier = "OLD-004", OldSchemeName = "Old Scheme" }
        ]);

        var (service, participantRepository) = CreateService(
            merge,
            [new ContractSummaryEntity { ContractId = contractId, CustomerId = customerId, YearId = 2026, IsActive = true, Suffix = "A" }],
            [new ParticipantSchemeRecord { ParticipantSchemeId = participantSchemeId, ContractId = contractId, ParticipantId = Guid.NewGuid(), SchemeId = oldSchemeId }],
            [oldScheme],
            new SystemSettingsEntity { NextYearWithDelayId = 2027, UTNumber = "UT3/306" });

        var result = await service.RenewContractsAsync(customerId, [contractId], [participantSchemeId], "Alice Example");

        Assert.True(result.Success);
        Assert.Empty(participantRepository.CreatedRecords);
    }

    [Fact]
    public async Task RenewContractsAsync_WhenNextSchemeIdPointsToAMissingScheme_SkipsThatSelectionAndStillSucceeds()
    {
        var customerId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var oldSchemeId = Guid.NewGuid();
        var missingNextSchemeId = Guid.NewGuid();
        var participantSchemeId = Guid.NewGuid();

        var oldScheme = new CoreScheme { SchemeId = oldSchemeId, SharedId = Guid.NewGuid(), YearId = 2026, Identifier = "OLD-005", Name = "Old Scheme" };

        var merge = new ContractMergeData(
        [
            new RenewableContractEntity { ContractId = contractId, ContractSignatory = "Alice Example" }
        ],
        [
            new RenewableContractItemEntity { ContractId = contractId, Suffix = "A", ParticipantSchemeId = participantSchemeId, LabCode = "LAB", OldSchemeIdentifier = "OLD-005", OldSchemeName = "Old Scheme" }
        ]);

        var contractRepository = new TestContractRepository([new ContractSummaryEntity { ContractId = contractId, CustomerId = customerId, YearId = 2026, IsActive = true, Suffix = "A" }]);
        var (participantSchemeRepository, lookupRepository) = (
            new TestParticipantSchemeRepository([new ParticipantSchemeRecord { ParticipantSchemeId = participantSchemeId, ContractId = contractId, ParticipantId = Guid.NewGuid(), SchemeId = oldSchemeId }]),
            new TestLookupRepository(new SystemSettingsEntity { NextYearWithDelayId = 2027, UTNumber = "UT3/306" }, []));

        var service = new RenewContractsService(
            new TestContractMergeRepository(merge),
            contractRepository,
            participantSchemeRepository,
            new SchemeRepositoryWithMissingNextScheme(oldScheme, missingNextSchemeId),
            lookupRepository);

        var result = await service.RenewContractsAsync(customerId, [contractId], [participantSchemeId], "Alice Example");

        Assert.True(result.Success);
        Assert.Empty(participantSchemeRepository.CreatedRecords);
    }

    // Legacy disabled Save while nothing was ticked; ids that are not this customer's renewable items count as nothing.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RenewContractsAsync_WhenNoItemOfTheCustomerIsSelected_IsRefusedAndNothingIsSaved(bool postsAnUnknownId)
    {
        var customerId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var merge = new ContractMergeData(
        [
            new RenewableContractEntity { ContractId = contractId, ContractSignatory = "Alice Example" }
        ],
        [
            new RenewableContractItemEntity { ContractId = contractId, Suffix = "A", ParticipantSchemeId = Guid.NewGuid(), LabCode = "LAB", OldSchemeIdentifier = "OLD-006", OldSchemeName = "Old Scheme", NewSchemeIdentifier = "NEW-006", NewSchemeName = "New Scheme" }
        ]);
        var contractRepository = new TestContractRepository([new ContractSummaryEntity { ContractId = contractId, CustomerId = customerId, YearId = 2026, IsActive = true, Suffix = "A" }]);
        var participantSchemeRepository = new TestParticipantSchemeRepository([]);
        var service = new RenewContractsService(
            new TestContractMergeRepository(merge),
            contractRepository,
            participantSchemeRepository,
            new TestSchemeRepository([]),
            new TestLookupRepository(new SystemSettingsEntity { NextYearWithDelayId = 2027, UTNumber = "UT3/306" }, []));

        var result = await service.RenewContractsAsync(customerId, [contractId], postsAnUnknownId ? [Guid.NewGuid()] : [], "Alice Example");

        Assert.False(result.Success);
        Assert.Null(result.NewContractId);
        Assert.Equal(RenewContractsService.NoItemsSelectedMessage, result.ErrorMessage);
        Assert.Empty(contractRepository.CreatedContracts);
        Assert.Empty(participantSchemeRepository.CreatedRecords);
    }

    // Legacy kept Save enabled when no item had a next-year scheme on first load, and saving created a contract with no items.
    [Fact]
    public async Task RenewContractsAsync_WhenNoItemHasANextYearSchemeAndEveryContractIsSelected_CreatesAnEmptyContract()
    {
        var (service, contractRepository, participantSchemeRepository, contractA, contractB) = CreateWithNoRenewableItems();

        var result = await service.RenewContractsAsync(Guid.NewGuid(), [contractA, contractB], [], "Alice Example");

        Assert.True(result.Success);
        var created = Assert.Single(contractRepository.CreatedContracts);
        Assert.Equal(result.NewContractId, created.ContractId);
        Assert.Equal("Alice Example", created.ContractSignatory);
        Assert.Empty(participantSchemeRepository.CreatedRecords);
    }

    // Changing a contract tick disabled legacy Save, so with a contract left out nothing is renewed.
    [Fact]
    public async Task RenewContractsAsync_WhenNoItemHasANextYearSchemeButAContractIsLeftOut_IsRefused()
    {
        var (service, contractRepository, _, contractA, _) = CreateWithNoRenewableItems();

        var result = await service.RenewContractsAsync(Guid.NewGuid(), [contractA], [], null);

        Assert.False(result.Success);
        Assert.Equal(RenewContractsService.NoItemsSelectedMessage, result.ErrorMessage);
        Assert.Empty(contractRepository.CreatedContracts);
    }

    [Fact]
    public async Task RenewContractsAsync_WhenTheCustomerHasNoItems_IsRefused()
    {
        var contractId = Guid.NewGuid();
        var contractRepository = new TestContractRepository([]);
        var service = new RenewContractsService(
            new TestContractMergeRepository(new ContractMergeData([new RenewableContractEntity { ContractId = contractId }], [])),
            contractRepository,
            new TestParticipantSchemeRepository([]),
            new TestSchemeRepository([]),
            new TestLookupRepository(new SystemSettingsEntity { NextYearWithDelayId = 2027 }, []));

        var result = await service.RenewContractsAsync(Guid.NewGuid(), [contractId], [], null);

        Assert.False(result.Success);
        Assert.Empty(contractRepository.CreatedContracts);
    }

    private static (RenewContractsService Service, TestContractRepository Contracts, TestParticipantSchemeRepository ParticipantSchemes, Guid ContractA, Guid ContractB) CreateWithNoRenewableItems()
    {
        var contractA = Guid.NewGuid();
        var contractB = Guid.NewGuid();
        var merge = new ContractMergeData(
        [
            new RenewableContractEntity { ContractId = contractA, Suffix = "A" },
            new RenewableContractEntity { ContractId = contractB, Suffix = "B" }
        ],
        [
            new RenewableContractItemEntity { ContractId = contractA, Suffix = "A", ParticipantSchemeId = Guid.NewGuid(), LabCode = "LAB", OldSchemeIdentifier = "OLD-007" },
            new RenewableContractItemEntity { ContractId = contractB, Suffix = "B", ParticipantSchemeId = Guid.NewGuid(), LabCode = "LAB", OldSchemeIdentifier = "OLD-008" }
        ]);
        var contractRepository = new TestContractRepository([]);
        var participantSchemeRepository = new TestParticipantSchemeRepository([]);
        var service = new RenewContractsService(
            new TestContractMergeRepository(merge),
            contractRepository,
            participantSchemeRepository,
            new TestSchemeRepository([]),
            new TestLookupRepository(new SystemSettingsEntity { NextYearWithDelayId = 2027, UTNumber = "UT3/306" }, []));
        return (service, contractRepository, participantSchemeRepository, contractA, contractB);
    }

    private static (RenewContractsService Service, TestParticipantSchemeRepository ParticipantRepository) CreateService(
        ContractMergeData? mergeData = null,
        IReadOnlyList<ContractSummaryEntity>? contractSummaries = null,
        IReadOnlyList<ParticipantSchemeRecord>? participantSchemes = null,
        IReadOnlyList<CoreScheme>? schemes = null,
        SystemSettingsEntity? settings = null,
        IReadOnlyList<YearEntity>? weightedPricingYears = null)
    {
        var merge = mergeData ?? new ContractMergeData([], []);
        var contractRepository = new TestContractRepository(contractSummaries ?? []);
        var participantSchemeRepository = new TestParticipantSchemeRepository(participantSchemes ?? []);
        var schemeRepository = new TestSchemeRepository(schemes ?? []);
        var lookupRepository = new TestLookupRepository(settings ?? new SystemSettingsEntity { NextYearWithDelayId = 2027 }, weightedPricingYears ?? []);

        var service = new RenewContractsService(
            new TestContractMergeRepository(merge),
            contractRepository,
            participantSchemeRepository,
            schemeRepository,
            lookupRepository);

        return (service, participantSchemeRepository);
    }

    private sealed class TestContractMergeRepository(ContractMergeData data) : IContractMergeRepository
    {
        public Task<ContractMergeData> GetContractMergeInfoAsync(Guid customerId, CancellationToken cancellationToken = default) =>
            Task.FromResult(data);
    }

    private sealed class TestContractRepository(IReadOnlyList<ContractSummaryEntity> summaries) : IContractRepository
    {
        public Task<CoreContract?> GetByIdAsync(Guid contractId, CancellationToken cancellationToken = default) => Task.FromResult<CoreContract?>(null);

        public Task<IReadOnlyList<ContractSummaryEntity>> GetSummariesAsync(Guid customerId, ContractPeriod period, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ContractSummaryEntity>>(summaries.Where(c => c.CustomerId == customerId).ToList());

        public Task<IReadOnlyList<ContractSummaryEntity>> GetSummariesByYearAsync(Guid customerId, int yearId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ContractSummaryEntity>>(summaries.Where(c => c.CustomerId == customerId && c.YearId == yearId).ToList());

        public Task<CoreContract> CreateAsync(CoreContract contract, CancellationToken cancellationToken = default)
        {
            contract.ContractId = contract.ContractId == Guid.Empty ? Guid.NewGuid() : contract.ContractId;
            CreatedContracts.Add(contract);
            return Task.FromResult(contract);
        }

        public List<CoreContract> CreatedContracts { get; } = [];

        public Task<CoreContract?> UpdateAsync(CoreContract contract, CancellationToken cancellationToken = default) => Task.FromResult<CoreContract?>(contract);

        public Task<ContractItemsAggregate?> GetContractItemsAsync(Guid contractId, CancellationToken cancellationToken = default) => Task.FromResult<ContractItemsAggregate?>(null);
    }

    private sealed class TestParticipantSchemeRepository(IReadOnlyList<ParticipantSchemeRecord> participantSchemes) : IParticipantSchemeRepository
    {
        private readonly Dictionary<Guid, ParticipantSchemeRecord> _records = participantSchemes.ToDictionary(r => r.ParticipantSchemeId);

        public List<ParticipantSchemeRecord> CreatedRecords { get; } = [];

        public Task<ParticipantSchemeRecord?> GetByIdAsync(Guid participantSchemeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_records.TryGetValue(participantSchemeId, out var record) ? record : null);

        public Task<ParticipantSchemeRecord> CreateAsync(ParticipantSchemeRecord record, CancellationToken cancellationToken = default)
        {
            _records[record.ParticipantSchemeId] = record;
            CreatedRecords.Add(record);
            return Task.FromResult(record);
        }

        public Task<bool> RemoveAsync(ParticipantSchemeRecord record, ParticipantSchemeAudit audit, CancellationToken cancellationToken = default) =>
            UpdateAsync(record, cancellationToken);

        public Task<bool> UpdateAsync(ParticipantSchemeRecord record, CancellationToken cancellationToken = default)
        {
            _records[record.ParticipantSchemeId] = record;
            return Task.FromResult(true);
        }

        public Task<ParticipantSchemeContext> GetSchemeContextAsync(Guid schemeId, Guid contractId, bool nonUk, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ParticipantSchemeContext());

        public Task<IReadOnlyList<OtherParticipantScheme>> GetByParticipantAndSchemeAsync(Guid participantId, Guid schemeId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<OtherParticipantScheme>>([]);
    }

    private sealed class TestSchemeRepository(IReadOnlyList<CoreScheme> schemes) : ISchemeRepository
    {
        private readonly Dictionary<Guid, CoreScheme> _schemes = schemes.ToDictionary(s => s.SchemeId);

        public Task<CoreScheme?> GetByIdAsync(Guid schemeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_schemes.TryGetValue(schemeId, out var scheme) ? scheme : null);

        public Task<IReadOnlyList<SchemeSummaryEntity>> GetAllSummariesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SchemeSummaryEntity>>([]);

        public Task<IReadOnlyList<SchemeSummaryEntity>> GetSummariesByYearAsync(int yearId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SchemeSummaryEntity>>(_schemes.Values.Where(s => s.YearId == yearId).Select(s => new SchemeSummaryEntity { SharedId = s.SharedId, YearId = s.YearId, CurrentSchemeId = s.SchemeId, CurrentIdentifier = s.Identifier, CurrentName = s.Name }).ToList());

        public Task<IReadOnlyList<SchemeSummaryEntity>> GetSummariesBySchemeIdAsync(Guid schemeId, CancellationToken cancellationToken = default)
        {
            if (!_schemes.TryGetValue(schemeId, out var scheme))
            {
                return Task.FromResult<IReadOnlyList<SchemeSummaryEntity>>([]);
            }

            var nextScheme = _schemes.Values.FirstOrDefault(s => s.SharedId == scheme.SharedId && s.YearId == scheme.YearId + 1);
            return Task.FromResult<IReadOnlyList<SchemeSummaryEntity>>([
                new SchemeSummaryEntity
                {
                    SharedId = scheme.SharedId,
                    YearId = scheme.YearId,
                    CurrentSchemeId = scheme.SchemeId,
                    CurrentIdentifier = scheme.Identifier,
                    CurrentName = scheme.Name,
                    NextSchemeId = nextScheme?.SchemeId,
                    NextIdentifier = nextScheme?.Identifier,
                    NextName = nextScheme?.Name
                }
            ]);
        }

        public Task<IReadOnlyList<SchemeHistoryEntity>> GetHistoryAsync(Guid sharedId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SchemeHistoryEntity>>(_schemes.Values.Where(s => s.SharedId == sharedId).OrderByDescending(s => s.YearId).Select(s => new SchemeHistoryEntity { SchemeId = s.SchemeId, SharedId = s.SharedId, YearId = s.YearId, Identifier = s.Identifier, Name = s.Name }).ToList());

        public Task<IReadOnlyList<ParticipantSchemeEntity>> GetCommercialSchemesBySsoIdAsync(Guid ssoId, int currentFinancialYear, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ParticipantSchemeEntity>>([]);

        public Task<IReadOnlyList<ParticipantSchemeEntity>> GetNonCommercialSchemesBySsoIdAsync(Guid ssoId, int currentFinancialYear, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ParticipantSchemeEntity>>([]);

        public Task<CoreScheme> CreateAsync(CoreScheme scheme, CancellationToken cancellationToken = default)
        {
            _schemes[scheme.SchemeId] = scheme;
            return Task.FromResult(scheme);
        }

        public Task<CoreScheme?> UpdateAsync(CoreScheme scheme, SchemeAuditWrite? audit = null, CancellationToken cancellationToken = default)
        {
            _schemes[scheme.SchemeId] = scheme;
            return Task.FromResult<CoreScheme?>(scheme);
        }
    }

    // Simulates SchemeInfoCollection resolving a NextSchemeId that no longer has a matching Scheme
    // row (e.g. the next-year scheme was subsequently deleted) - GetByIdAsync(newSchemeId) is null.
    private sealed class SchemeRepositoryWithMissingNextScheme(CoreScheme oldScheme, Guid missingNextSchemeId) : ISchemeRepository
    {
        public Task<CoreScheme?> GetByIdAsync(Guid schemeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(schemeId == oldScheme.SchemeId ? oldScheme : null);

        public Task<IReadOnlyList<SchemeSummaryEntity>> GetAllSummariesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SchemeSummaryEntity>>([]);

        public Task<IReadOnlyList<SchemeSummaryEntity>> GetSummariesByYearAsync(int yearId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SchemeSummaryEntity>>([]);

        public Task<IReadOnlyList<SchemeSummaryEntity>> GetSummariesBySchemeIdAsync(Guid schemeId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SchemeSummaryEntity>>(schemeId == oldScheme.SchemeId
                ?
                [
                    new SchemeSummaryEntity
                    {
                        SharedId = oldScheme.SharedId,
                        YearId = oldScheme.YearId,
                        CurrentSchemeId = oldScheme.SchemeId,
                        CurrentIdentifier = oldScheme.Identifier,
                        CurrentName = oldScheme.Name,
                        NextSchemeId = missingNextSchemeId,
                        NextIdentifier = "MISSING",
                        NextName = "Missing Next Scheme"
                    }
                ]
                : []);

        public Task<IReadOnlyList<SchemeHistoryEntity>> GetHistoryAsync(Guid sharedId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SchemeHistoryEntity>>([]);

        public Task<IReadOnlyList<ParticipantSchemeEntity>> GetCommercialSchemesBySsoIdAsync(Guid ssoId, int currentFinancialYear, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ParticipantSchemeEntity>>([]);

        public Task<IReadOnlyList<ParticipantSchemeEntity>> GetNonCommercialSchemesBySsoIdAsync(Guid ssoId, int currentFinancialYear, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ParticipantSchemeEntity>>([]);

        public Task<CoreScheme> CreateAsync(CoreScheme scheme, CancellationToken cancellationToken = default) => Task.FromResult(scheme);

        public Task<CoreScheme?> UpdateAsync(CoreScheme scheme, SchemeAuditWrite? audit = null, CancellationToken cancellationToken = default) => Task.FromResult<CoreScheme?>(scheme);
    }

    private sealed class TestLookupRepository(SystemSettingsEntity settings, IReadOnlyList<YearEntity> weightedPricingYears) : ILookupRepository
    {
        public Task<IReadOnlyList<CountryEntity>> GetCountriesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CountryEntity>>([]);
        public Task<IReadOnlyList<CurrencyEntity>> GetCurrenciesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CurrencyEntity>>([]);
        public Task<IReadOnlyList<CustomerTypeEntity>> GetCustomerTypesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CustomerTypeEntity>>([]);
        public Task<IReadOnlyList<VatRatingEntity>> GetVatRatingsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<VatRatingEntity>>([]);
        public Task<IReadOnlyList<LabTypeEntity>> GetLabTypesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<LabTypeEntity>>([]);
        public Task<IReadOnlyList<YearEntity>> GetCurrentYearsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<YearEntity>>([]);
        public Task<IReadOnlyList<YearEntity>> GetAllYearsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<YearEntity>>([]);
        public Task<IReadOnlyList<SchemeCurrencyEntity>> GetSchemeCurrenciesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SchemeCurrencyEntity>>([]);
        public Task<IReadOnlyList<PostagePricingPlanEntity>> GetPostagePricingPlansForYearAsync(int yearId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PostagePricingPlanEntity>>([]);
        public Task<IReadOnlyList<TestReturnValueTypeEntity>> GetTestReturnValueTypesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TestReturnValueTypeEntity>>([]);
        public Task<SystemSettingsEntity> GetSystemSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(settings);
        public Task<IReadOnlyList<YearEntity>> GetWeightedPricingYearsAsync(CancellationToken cancellationToken = default) => Task.FromResult(weightedPricingYears);
        public Task<IReadOnlyList<GroupAddressEntity>> GetGroupAddressesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<GroupAddressEntity>>([]);

        public Task<IReadOnlyList<ScheduleEntity>> GetSchedulesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ScheduleEntity>>([]);

        public Task<IReadOnlyList<ScheduleCodeEntity>> GetScheduleCodesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ScheduleCodeEntity>>([]);

        public Task<IReadOnlyList<DayEntity>> GetDaysAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DayEntity>>([]);

        public Task<IReadOnlyList<MonthlyDistributionEntity>> GetMonthlyDistributionsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MonthlyDistributionEntity>>([]);

        public Task<IReadOnlyList<PTNumberEntity>> GetPTNumbersAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PTNumberEntity>>([]);

        public Task<IReadOnlyList<SchemeUserEntity>> GetTestConsultantsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SchemeUserEntity>>([]);

        public Task<IReadOnlyList<SchemeUserEntity>> GetAssessorsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SchemeUserEntity>>([]);

        public Task<IReadOnlyList<SchemeItemTypeEntity>> GetSchemeItemTypesAsync(PTL.Contracts.Lookup.SchemeItemTypeKind kind, int yearId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SchemeItemTypeEntity>>([]);
    }
}
