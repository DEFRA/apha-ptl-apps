using Microsoft.Extensions.Logging;
using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

// Logger that keeps the formatted message and the structured state values of every entry, so a test can prove
// what was logged and that no email address reached the logs.
internal sealed class MessageCapturingLogger<T> : ILogger<T>
{
    public sealed record Entry(LogLevel Level, string Message, IReadOnlyList<KeyValuePair<string, object?>> State, Exception? Exception);

    public List<Entry> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var values = state is IEnumerable<KeyValuePair<string, object?>> pairs ? pairs.ToList() : [];
        lock (Entries)
        {
            Entries.Add(new Entry(logLevel, formatter(state, exception), values, exception));
        }
    }

    public IEnumerable<Entry> At(LogLevel level) => Entries.Where(e => e.Level == level);

    // Message, every structured value and any exception message must be free of the addresses.
    public void AssertNoneContain(params string[] forbidden)
    {
        foreach (var entry in Entries)
        {
            var haystack = new List<string> { entry.Message, entry.Exception?.Message ?? string.Empty };
            haystack.AddRange(entry.State.Select(kv => $"{kv.Key}={kv.Value}"));
            foreach (var text in haystack)
            {
                foreach (var value in forbidden)
                {
                    Assert.DoesNotContain(value, text, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
    }
}

internal sealed class SettableTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

// Notify client that yields before recording, so two unsynchronised callers would interleave.
internal sealed class SlowNotifyClient : PTL.Core.Notifications.INotifyClient
{
    private readonly object _gate = new();

    public List<(string TemplateId, string Email, string? Reference)> Sent { get; } = [];

    public async Task SendEmailAsync(
        string templateId,
        string emailAddress,
        IReadOnlyDictionary<string, string>? personalisation = null,
        string? reference = null,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(25, CancellationToken.None);
        lock (_gate)
        {
            Sent.Add((templateId, emailAddress, reference));
        }
    }
}

internal static class TabulationEmailTestData
{
    public static readonly Guid Mds1 = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    public static readonly Guid Mds2 = Guid.Parse("a1000000-0000-0000-0000-000000000002");
    public static readonly Guid Mds3 = Guid.Parse("a1000000-0000-0000-0000-000000000003");
    public static readonly Guid Mds4 = Guid.Parse("a1000000-0000-0000-0000-000000000004");
    public static readonly Guid Tc1 = Guid.Parse("b2000000-0000-0000-0000-000000000001");
    public static readonly Guid Tc2 = Guid.Parse("b2000000-0000-0000-0000-000000000002");
    public static readonly Guid Tc3 = Guid.Parse("b2000000-0000-0000-0000-000000000003");

    public const string Tc1Email = "tc1.internal@example.test";
    public const string Tc2Email = "tc2.external@example.test";
    public const string Tc3Email = "tc3.internal@example.test";

    // 12 May 2026 12:00 UTC is 13:00 in Europe/London.
    public static readonly DateTimeOffset NowUtc = new(2026, 5, 12, 12, 0, 0, TimeSpan.Zero);

    public static DateTime Today => new(2026, 5, 12);

    public static TabulationEmailCandidateEntity Candidate(
        Guid id,
        int daysSinceReady = 0,
        int emailState = 0,
        bool commentSubmitted = false,
        Guid? tc1 = null,
        Guid? tc2 = null,
        Guid? tc3 = null) =>
        new()
        {
            MonthlyDistributionSchemeId = id,
            SchemeId = Guid.NewGuid(),
            SchemeName = "PT0060: Mastitis bovine",
            DistributionReference = "17152",
            DistributionReferenceSuffix = "A",
            ScheduleCode = "BA",
            EmailState = emailState,
            IsCommentSubmitted = commentSubmitted,
            IsReadyForTestConsultant = Today.AddDays(-daysSinceReady).AddHours(15),
            TestConsultant1Id = tc1 ?? Tc1,
            TestConsultant2Id = tc2,
            TestConsultant3Id = tc3,
        };

    public static TabulationConsultantEntity Consultant(Guid id, string name, string? department, string? email, bool external = false) =>
        new() { UserId = id, FriendlyName = name, Department = department, Email = email, IsExternal = external };

    // The personalisation must hold exactly the expected keys and values.
    public static void AssertPersonalisation(IReadOnlyDictionary<string, string>? actual, params (string Key, string Value)[] expected)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.Select(e => e.Key).Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
        foreach (var (key, value) in expected)
        {
            Assert.Equal(value, actual[key]);
        }
    }
}
