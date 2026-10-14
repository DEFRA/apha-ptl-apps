using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

// A small but representative distribution for the Actual Results screen:
//   Culture (Order 2)   - Sample 1 + Sample 2 applicable; Growth (dropdown) + Count (regex) result items;
//                         Medium (dropdown, Order 1) + Incubation (regex, Order 2) method items
//   Serology (Order 1)  - only Sample 1 applicable; Titre (regex) result item
//   Unused (Order 3)    - NO applicable sample, so legacy omits it (and its method item) entirely
// Every Sample/Test pair has its SampleResultItem rows, applicable or not, as in the real structure.
internal sealed class ActualResultsScenario
{
    public static readonly DateTime Deadline = new(2026, 6, 30);

    public Guid ParticipantId { get; } = Guid.NewGuid();
    public Guid SchemeId { get; } = Guid.NewGuid();

    public Guid Sample1 { get; } = Guid.NewGuid();
    public Guid Sample2 { get; } = Guid.NewGuid();

    public Guid CultureTest { get; } = Guid.NewGuid();
    public Guid SerologyTest { get; } = Guid.NewGuid();
    public Guid UnusedTest { get; } = Guid.NewGuid();

    public Guid GrowthItem { get; } = Guid.NewGuid();
    public Guid CountItem { get; } = Guid.NewGuid();
    public Guid TitreItem { get; } = Guid.NewGuid();
    public Guid UnusedItem { get; } = Guid.NewGuid();

    public Guid CultureS1Growth { get; } = Guid.NewGuid();
    public Guid CultureS1Count { get; } = Guid.NewGuid();
    public Guid CultureS2Growth { get; } = Guid.NewGuid();
    public Guid CultureS2Count { get; } = Guid.NewGuid();
    public Guid SerologyS1Titre { get; } = Guid.NewGuid();
    public Guid SerologyS2Titre { get; } = Guid.NewGuid();
    public Guid UnusedS1 { get; } = Guid.NewGuid();
    public Guid UnusedS2 { get; } = Guid.NewGuid();

    public Guid MediumMethod { get; } = Guid.NewGuid();
    public Guid IncubationMethod { get; } = Guid.NewGuid();
    public Guid UnusedMethod { get; } = Guid.NewGuid();

    public IReadOnlyList<Guid> AllSampleResultItemIds =>
        [CultureS1Growth, CultureS1Count, CultureS2Growth, CultureS2Count, SerologyS1Titre, SerologyS2Titre, UnusedS1, UnusedS2];

    public IReadOnlyList<Guid> AllMethodItemIds => [MediumMethod, IncubationMethod, UnusedMethod];

    public IntendedResultsData Structure(bool storeRatings = false)
    {
        var cultureS1 = Guid.NewGuid();
        var cultureS2 = Guid.NewGuid();
        var serologyS1 = Guid.NewGuid();
        var serologyS2 = Guid.NewGuid();
        var unusedS1 = Guid.NewGuid();
        var unusedS2 = Guid.NewGuid();

        return new IntendedResultsData(
            new IntendedResultsHeaderEntity { MonthlyDistributionSchemeId = SchemeId, StoreRatings = storeRatings },
            [
                new DistributionSampleEntity { SampleId = Sample1, SampleNumber = "S001" },
                new DistributionSampleEntity { SampleId = Sample2, SampleNumber = "S002" },
            ],
            [
                new DistributionSampleTestEntity { SampleTestId = cultureS1, SampleId = Sample1, TestId = CultureTest, IsApplicable = true },
                new DistributionSampleTestEntity { SampleTestId = cultureS2, SampleId = Sample2, TestId = CultureTest, IsApplicable = true },
                new DistributionSampleTestEntity { SampleTestId = serologyS1, SampleId = Sample1, TestId = SerologyTest, IsApplicable = true },
                new DistributionSampleTestEntity { SampleTestId = serologyS2, SampleId = Sample2, TestId = SerologyTest, IsApplicable = false },
                new DistributionSampleTestEntity { SampleTestId = unusedS1, SampleId = Sample1, TestId = UnusedTest, IsApplicable = false },
                new DistributionSampleTestEntity { SampleTestId = unusedS2, SampleId = Sample2, TestId = UnusedTest, IsApplicable = false },
            ],
            [
                new DistributionTestEntity { TestId = CultureTest, TestName = "Culture", Order = 2 },
                new DistributionTestEntity { TestId = SerologyTest, TestName = "Serology", Order = 1 },
                new DistributionTestEntity { TestId = UnusedTest, TestName = "Unused", Order = 3 },
            ],
            [
                new DistributionTestMethodItemEntity
                {
                    TestMethodItemId = IncubationMethod, TestId = CultureTest, Name = "Incubation", Order = 2, FormatString = "^.+$", ReturnValueType = "Text",
                },
                new DistributionTestMethodItemEntity
                {
                    TestMethodItemId = MediumMethod, TestId = CultureTest, Name = "Medium", Order = 1, FormatString = "Agar|Broth", ReturnValueType = "Agar or Broth",
                },
                new DistributionTestMethodItemEntity
                {
                    TestMethodItemId = UnusedMethod, TestId = UnusedTest, Name = "Hidden", Order = 1, FormatString = "^.+$", ReturnValueType = "Text",
                },
            ],
            [
                new DistributionTestResultItemEntity
                {
                    TestResultItemId = CountItem, TestId = CultureTest, Name = "Count", Order = 2, FormatString = "^\\d+$", ReturnValueType = "Whole number",
                },
                new DistributionTestResultItemEntity
                {
                    TestResultItemId = GrowthItem, TestId = CultureTest, Name = "Growth", Order = 1, FormatString = "Positive|Negative", ReturnValueType = "Positive or Negative",
                },
                new DistributionTestResultItemEntity
                {
                    TestResultItemId = TitreItem, TestId = SerologyTest, Name = "Titre", Order = 1, FormatString = "^\\d{1,3}$", ReturnValueType = "Up to 3 digits",
                },
                new DistributionTestResultItemEntity
                {
                    TestResultItemId = UnusedItem, TestId = UnusedTest, Name = "Ignored", Order = 1, FormatString = "^.+$", ReturnValueType = "Text",
                },
            ],
            [],
            [],
            [
                new DistributionSampleResultItemEntity { SampleResultItemId = CultureS1Growth, SampleId = Sample1, SampleTestId = cultureS1, TestResultItemId = GrowthItem },
                new DistributionSampleResultItemEntity { SampleResultItemId = CultureS1Count, SampleId = Sample1, SampleTestId = cultureS1, TestResultItemId = CountItem },
                new DistributionSampleResultItemEntity { SampleResultItemId = CultureS2Growth, SampleId = Sample2, SampleTestId = cultureS2, TestResultItemId = GrowthItem },
                new DistributionSampleResultItemEntity { SampleResultItemId = CultureS2Count, SampleId = Sample2, SampleTestId = cultureS2, TestResultItemId = CountItem },
                new DistributionSampleResultItemEntity { SampleResultItemId = SerologyS1Titre, SampleId = Sample1, SampleTestId = serologyS1, TestResultItemId = TitreItem },
                new DistributionSampleResultItemEntity { SampleResultItemId = SerologyS2Titre, SampleId = Sample2, SampleTestId = serologyS2, TestResultItemId = TitreItem },
                new DistributionSampleResultItemEntity { SampleResultItemId = UnusedS1, SampleId = Sample1, SampleTestId = unusedS1, TestResultItemId = UnusedItem },
                new DistributionSampleResultItemEntity { SampleResultItemId = UnusedS2, SampleId = Sample2, SampleTestId = unusedS2, TestResultItemId = UnusedItem },
            ]);
    }

    // The value each row holds once a full set of results has been stored.
    public IReadOnlyDictionary<Guid, string> StoredSampleValues => new Dictionary<Guid, string>
    {
        [CultureS1Growth] = "Positive",
        [CultureS1Count] = "12",
        [CultureS2Growth] = "Negative",
        [CultureS2Count] = "7",
        [SerologyS1Titre] = "5",
        [SerologyS2Titre] = string.Empty,
        [UnusedS1] = string.Empty,
        [UnusedS2] = string.Empty,
    };

    public IReadOnlyDictionary<Guid, string> StoredMethodValues => new Dictionary<Guid, string>
    {
        [MediumMethod] = "Broth",
        [IncubationMethod] = "37C",
        [UnusedMethod] = string.Empty,
    };

    public static ActualResultsHeaderEntity Header(DateTime? firstEditDate, DateTime? deadline = null) =>
        new()
        {
            FirstEditDate = firstEditDate,
            LastEditDate = firstEditDate,
            Distribution = "D1/S1",
            SchemeIdentifier = "BT",
            SchemeName = "Brucella",
            DeadlineDate = deadline ?? Deadline,
        };

    // A participant whose results were saved before: every structure row has a stored value.
    public ActualResultsData FullyStored(DateTime? firstEditDate = null, DateTime? deadline = null) =>
        new(
            Header(firstEditDate ?? new DateTime(2026, 6, 1), deadline),
            [.. StoredSampleValues.Select(kv => new ActualSampleResultEntity { SampleResultItemId = kv.Key, Result = kv.Value })],
            [.. StoredMethodValues.Select(kv => new ActualMethodItemResultEntity { TestMethodItemId = kv.Key, Result = kv.Value })]);

    // A participant that never saved: header only, FirstEditDate null.
    public static ActualResultsData Untouched(DateTime? deadline = null) => new(Header(null, deadline), [], []);

    // The posted form as legacy rendered it: only applicable samples (and every method item of a shown test).
    public ActualResultsUpdate PostStored(string auditWho = "tester", string auditWhy = "", Action<Dictionary<Guid, string>, Dictionary<Guid, string>>? edit = null)
    {
        var samples = new Dictionary<Guid, string>
        {
            [CultureS1Growth] = "Positive",
            [CultureS1Count] = "12",
            [CultureS2Growth] = "Negative",
            [CultureS2Count] = "7",
            [SerologyS1Titre] = "5",
        };
        var methods = new Dictionary<Guid, string>
        {
            [MediumMethod] = "Broth",
            [IncubationMethod] = "37C",
        };
        edit?.Invoke(samples, methods);

        return new ActualResultsUpdate(
            [.. samples.Select(kv => new ActualResultsSampleValueUpdate(kv.Key, kv.Value))],
            [.. methods.Select(kv => new ActualResultsMethodItemUpdate(kv.Key, kv.Value))],
            auditWho,
            auditWhy);
    }

    public FakeDistributionRepository Seed(ActualResultsData actual, bool storeRatings = false, FakeDistributionRepository? repository = null)
    {
        repository ??= new FakeDistributionRepository();
        repository.ActualResults[ParticipantId] = actual;
        repository.IntendedResults[SchemeId] = Structure(storeRatings);
        return repository;
    }
}

// TimeProvider whose local time zone is UTC, so GetLocalNow() (what the service reads) equals the supplied clock.
internal sealed class LocalClock(DateTime now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(now, DateTimeKind.Utc));

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}
