using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Api.BackgroundServices;
using PTL.Api.Tests.Distribution;
using PTL.Core.Configuration;
using PTL.Core.Distribution;
using PTL.Core.Notifications;
using PTL.Core.Participant;
using static PTL.Api.Tests.Distribution.PackagingTestData;
using ParticipantEntity = PTL.Core.Participant.Participant;

namespace PTL.Api.Tests.Notifications;

// The Distribution Posted email from the packaging Save to Notify, with the real PackagingService, notifier and background worker wired as
// Program.cs wires them and only the repository, the participant lookup and GOV.UK Notify faked.
public class DistributionPostedEmailFlowTests
{
    private const string TemplateId = "2bb0e2d0-fa23-472e-947c-4524d14ad288";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly DateTimeOffset Now = new(2026, 5, 13, 10, 30, 0, TimeSpan.Zero);

    // Holds Notify inside the call until released, so a test can look at what happened while the send was still running.
    private sealed class GatedNotifyClient(RecordingNotifyClient inner) : INotifyClient
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task SendEmailAsync(
            string templateId,
            string emailAddress,
            IReadOnlyDictionary<string, string>? personalisation = null,
            string? reference = null,
            CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            await inner.SendEmailAsync(templateId, emailAddress, personalisation, reference, cancellationToken);
        }
    }

    // Wraps the real notifier and reports each finished send, so a test can wait for the background worker without sleeping.
    private sealed class SignallingNotifier(DistributionPostedNotifier inner, Channel<DistributionPostedRunSummary> finished) : IDistributionPostedNotifier
    {
        public Task<IReadOnlyList<DistributionPostedEmailEntity>> BuildEmailsAsync(
            IReadOnlyList<PostedParticipant> posted, CancellationToken cancellationToken = default) =>
            inner.BuildEmailsAsync(posted, cancellationToken);

        public async Task<DistributionPostedRunSummary> SendAsync(
            IReadOnlyList<DistributionPostedEmailEntity> emails, CancellationToken cancellationToken = default)
        {
            var summary = await inner.SendAsync(emails, cancellationToken);
            finished.Writer.TryWrite(summary);
            return summary;
        }
    }

    private sealed class Flow : IDisposable
    {
        private readonly ServiceProvider _provider;

        public FakeDistributionRepository Repository { get; } = new();
        public FakePostedParticipantService Participants { get; } = new();
        public RecordingNotifyClient Notify { get; } = new();
        public GatedNotifyClient Gate { get; }
        public Channel<DistributionPostedRunSummary> Finished { get; } = Channel.CreateUnbounded<DistributionPostedRunSummary>();
        public MessageCapturingLogger<DistributionPostedNotifier> NotifierLogger { get; } = new();
        public MessageCapturingLogger<DistributionPostedEmailDispatcher> DispatcherLogger { get; } = new();
        public DistributionPostedEmailDispatcher Dispatcher { get; }

        public Flow(bool enabled = true)
        {
            Gate = new GatedNotifyClient(Notify);
            Repository.Schedules[(2026, 5)] = (MonthlyDistributionId, []);
            Posted(Dp1, Dp2);

            var services = new ServiceCollection();
            services.AddSingleton<IDistributionRepository>(Repository);
            services.AddSingleton<IParticipantService>(Participants);
            services.AddSingleton<INotifyClient>(Gate);
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
            services.AddSingleton(Options.Create(new DistributionPostedEmailOptions { Enabled = enabled }));
            services.AddSingleton(Options.Create(new ExternalOptions { AppUrl = "https://external.example.test" }));
            services.AddSingleton<ILogger<DistributionPostedNotifier>>(NotifierLogger);
            services.AddSingleton<ILogger<DistributionPostedEmailDispatcher>>(DispatcherLogger);
            services.AddSingleton(Finished);
            services.AddScoped<DistributionPostedNotifier>();
            services.AddScoped<IDistributionPostedNotifier>(sp =>
                new SignallingNotifier(sp.GetRequiredService<DistributionPostedNotifier>(), sp.GetRequiredService<Channel<DistributionPostedRunSummary>>()));
            services.AddSingleton<DistributionPostedEmailDispatcher>();
            services.AddSingleton<IDistributionPostedEmailDispatcher>(sp => sp.GetRequiredService<DistributionPostedEmailDispatcher>());
            services.AddScoped<IPackagingService, PackagingService>();
            _provider = services.BuildServiceProvider();

            Dispatcher = _provider.GetRequiredService<DistributionPostedEmailDispatcher>();
        }

        public IServiceScope CreateScope() => _provider.CreateScope();

        public void AddParticipant(Guid participantId, string email, string email2 = "") =>
            Participants.Participants[participantId] = new ParticipantEntity { ParticipantId = participantId, Email = email, Email2 = email2 };

        // The sheet as the database holds it: the given rows are still to be posted, every other row is posted already.
        public void Posted(params Guid[] stillToPost) => Repository.PackagingSheets[(MonthlyDistributionId, PackagingMode.GroupAddress, 1)] = Raw(
            groups: [Group(GroupA)],
            items: [Item(GroupA, Scheme1, sets: 2)],
            participants:
            [
                ParticipantRow(GroupA, Scheme1, distributionParticipantId: Dp1, participantId: Part1, packaged: Day1, posted: stillToPost.Contains(Dp1) ? null : Day1),
                ParticipantRow(GroupA, Scheme1, distributionParticipantId: Dp2, participantId: Part2, packaged: Day1, posted: stillToPost.Contains(Dp2) ? null : Day1),
            ]);

        public async Task<PackagingSaveOutcome> SaveAsync(Guid distributionParticipantId)
        {
            using var scope = CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IPackagingService>()
                .SavePackagingAsync(2026, 5, [new PackagingStageUpdate(PackagingMode.GroupAddress, 1, distributionParticipantId, true, true)])
                .WaitAsync(Timeout);
        }

        public Task<DistributionPostedRunSummary> NextFinishedAsync() => Finished.Reader.ReadAsync().AsTask().WaitAsync(Timeout);

        public void Dispose()
        {
            Dispatcher.Dispose();
            _provider.Dispose();
        }
    }

    [Fact]
    public async Task Post_TheSaveReturnsWhileTheSendIsRunning_ThenTheEmailGoesToThePrimaryAndSecondaryAddress()
    {
        using var flow = new Flow();
        flow.AddParticipant(Part1, "primary@example.test", "secondary@example.test");
        await flow.Dispatcher.StartAsync(CancellationToken.None);

        var outcome = await flow.SaveAsync(Dp1);

        Assert.Equal(new PackagingSaveOutcome(true), outcome);
        var committed = Assert.Single(flow.Repository.PackagingUpdates).Emails;
        Assert.Equal(["primary@example.test", "secondary@example.test"], committed.Select(e => e.Email));
        await flow.Gate.Entered.Task.WaitAsync(Timeout);
        Assert.Empty(flow.Notify.Sent);
        Assert.Empty(flow.Repository.MarkedSentEmails);

        flow.Gate.Release.SetResult();
        var summary = await flow.NextFinishedAsync();
        await flow.Dispatcher.StopAsync(CancellationToken.None);

        Assert.Equal(new DistributionPostedRunSummary(2, 2, 0, 0), summary);
        Assert.Equal(["primary@example.test", "secondary@example.test"], flow.Notify.Sent.Select(s => s.Email));
        Assert.All(flow.Notify.Sent, s => Assert.Equal(TemplateId, s.TemplateId));
        Assert.Equal(committed.Select(e => e.DistributionPostedEmailId.ToString()), flow.Notify.Sent.Select(s => s.Reference));
        Assert.All(flow.Notify.Sent, s => TabulationEmailTestData.AssertPersonalisation(
            s.Personalisation,
            ("scheme_name", "PT0060: Mastitis bovine"),
            ("distribution", "17152A/BA"),
            ("external_link", "https://external.example.test"),
            ("date", "13/05/2026")));
        Assert.Equal(committed.Select(e => e.DistributionPostedEmailId), flow.Repository.MarkedSentEmails.Select(m => m.Id));
        flow.NotifierLogger.AssertNoneContain("@");
        flow.DispatcherLogger.AssertNoneContain("@");
        Assert.Empty(flow.DispatcherLogger.Entries);
    }

    [Fact]
    public async Task Post_AnAlreadyPostedRowSavedAgain_SendsNothingMore()
    {
        using var flow = new Flow();
        flow.Gate.Release.SetResult();
        flow.AddParticipant(Part1, "primary@example.test", "secondary@example.test");
        flow.AddParticipant(Part2, "other@example.test");
        await flow.Dispatcher.StartAsync(CancellationToken.None);

        await flow.SaveAsync(Dp1);
        Assert.Equal(new DistributionPostedRunSummary(2, 2, 0, 0), await flow.NextFinishedAsync());

        // The database now has the posted date, so the second save of the same row posts nothing new.
        flow.Posted(Dp2);
        await flow.SaveAsync(Dp1);

        // Batches are sent in the order they were handed over: had the second save sent anything, it would be reported before this one.
        await flow.SaveAsync(Dp2);
        Assert.Equal(new DistributionPostedRunSummary(1, 1, 0, 0), await flow.NextFinishedAsync());
        await flow.Dispatcher.StopAsync(CancellationToken.None);

        Assert.Equal(2, flow.Repository.PackagingUpdates.Count);
        Assert.Equal(["primary@example.test", "secondary@example.test", "other@example.test"], flow.Notify.Sent.Select(s => s.Email));
    }

    [Fact]
    public async Task Post_NotifyFails_TheSaveStillSucceedsAndTheEmailsAreNotSentAgain()
    {
        using var flow = new Flow();
        flow.Gate.Release.SetResult();
        flow.AddParticipant(Part1, "primary@example.test", "secondary@example.test");
        flow.AddParticipant(Part2, "other@example.test");
        flow.Notify.FailFor = address => address == "other@example.test" ? null : new HttpRequestException("notify down");
        await flow.Dispatcher.StartAsync(CancellationToken.None);

        var outcome = await flow.SaveAsync(Dp1);
        var failed = await flow.NextFinishedAsync();
        var next = await SaveThenNextFinishedAsync(flow, Dp2);
        await flow.Dispatcher.StopAsync(CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.Equal(new DistributionPostedRunSummary(2, 0, 0, 2), failed);
        Assert.Equal(new DistributionPostedRunSummary(1, 1, 0, 0), next);
        Assert.Equal(["other@example.test"], flow.Notify.Sent.Select(s => s.Email));
        Assert.Equal(flow.Repository.PackagingUpdates[1].Emails.Select(e => e.DistributionPostedEmailId), flow.Repository.MarkedSentEmails.Select(m => m.Id));
        Assert.Equal(2, flow.NotifierLogger.At(LogLevel.Error).Count());
        flow.NotifierLogger.AssertNoneContain("@");
    }

    [Fact]
    public async Task Post_SwitchedOff_TheEmailsAreQueuedByTheSaveButNeverSent()
    {
        using var flow = new Flow(enabled: false);
        flow.AddParticipant(Part1, "primary@example.test", "secondary@example.test");
        await flow.Dispatcher.StartAsync(CancellationToken.None);

        var outcome = await flow.SaveAsync(Dp1);
        var summary = await flow.NextFinishedAsync();
        await flow.Dispatcher.StopAsync(CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.Equal(2, Assert.Single(flow.Repository.PackagingUpdates).Emails.Count);
        Assert.Equal(new DistributionPostedRunSummary(2, 0, 2, 0), summary);
        Assert.False(flow.Gate.Entered.Task.IsCompleted);
        Assert.Empty(flow.Notify.Sent);
        Assert.Empty(flow.Repository.MarkedSentEmails);
    }

    private static async Task<DistributionPostedRunSummary> SaveThenNextFinishedAsync(Flow flow, Guid distributionParticipantId)
    {
        flow.Posted(distributionParticipantId);
        await flow.SaveAsync(distributionParticipantId);
        return await flow.NextFinishedAsync();
    }
}
