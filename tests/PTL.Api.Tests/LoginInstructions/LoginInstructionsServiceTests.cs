using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PTL.Api.Tests.Audit;
using PTL.Api.Tests.ExternalUser;
using PTL.Api.Tests.LabUsers;
using PTL.Api.Tests.Participant;
using PTL.Core.Audit;
using PTL.Core.Configuration;
using PTL.Core.LabUsers;
using PTL.Core.LoginInstructions;
using PTL.Core.Notifications;
using PTL.Core.Viewer;
using CoreParticipant = PTL.Core.Participant.Participant;
using CoreTestConsultant = PTL.Core.TestConsultant.TestConsultant;

namespace PTL.Api.Tests.LoginInstructions;

public class LoginInstructionsServiceTests
{
    private const string ExternalAppUrl = "https://external.example/";
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 9, 30, 0, TimeSpan.Zero);

    private sealed class RecordingNotifyClient : INotifyClient
    {
        public List<(string TemplateId, string EmailAddress, IReadOnlyDictionary<string, string>? Personalisation, string? Reference)> Sent { get; } = [];

        public Exception? Failure { get; set; }

        public Task SendEmailAsync(string templateId, string emailAddress, IReadOnlyDictionary<string, string>? personalisation = null, string? reference = null, CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
            {
                return Task.FromException(Failure);
            }

            Sent.Add((templateId, emailAddress, personalisation, reference));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingLoginInstructionsRepository : ILoginInstructionsRepository
    {
        public List<(LoginInstructionUserType UserType, Guid Id, DateTime SentDate, AuditEntry Audit, CancellationToken Token)> Marked { get; } = [];

        public Task MarkSentAsync(LoginInstructionUserType userType, Guid id, DateTime sentDate, AuditEntry auditEntry, CancellationToken cancellationToken = default)
        {
            Marked.Add((userType, id, sentDate, auditEntry, cancellationToken));
            return Task.CompletedTask;
        }
    }

    private sealed class Harness
    {
        public FakeParticipantRepository Participants { get; } = new();
        public FakeViewerRepository Viewers { get; } = new();
        public FakeTestConsultantRepository TestConsultants { get; } = new();
        public FakeLabUserRepository LabUsers { get; } = new();
        public RecordingLoginInstructionsRepository Repository { get; } = new();
        public RecordingNotifyClient Notify { get; } = new();
        public FakeActingUserAccessor ActingUser { get; } = new() { ActingUserId = Guid.NewGuid() };

        public LoginInstructionsService Service => new(
            new LoginInstructionRecipientResolver(
                Participants,
                Viewers,
                TestConsultants,
                LabUsers,
                Options.Create(new ExternalOptions { AppUrl = ExternalAppUrl })),
            Repository,
            Notify,
            ActingUser,
            new FixedTimeProvider(Now),
            NullLogger<LoginInstructionsService>.Instance);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public async Task SendAsync_Viewer_SendsTheViewerTemplateWithTheViewerPlaceholders()
    {
        var harness = new Harness();
        var viewer = new ViewerEntity { ViewerId = Guid.NewGuid(), Name = "Vera Viewer", Email = "vera@example.org" };
        harness.Viewers.Viewers.Add(viewer);

        var result = await harness.Service.SendAsync(LoginInstructionUserType.Viewer, viewer.ViewerId);

        Assert.True(result.Success);
        var sent = Assert.Single(harness.Notify.Sent);
        Assert.Equal("03d26290-c022-4d96-93bb-416cd64e806d", sent.TemplateId);
        Assert.Equal("vera@example.org", sent.EmailAddress);
        Assert.Equal(viewer.ViewerId.ToString(), sent.Reference);
        Assert.Equal(new Dictionary<string, string> { ["ViewerName"] = "Vera Viewer", ["ExternalAppLink"] = ExternalAppUrl }, sent.Personalisation);
    }

    [Fact]
    public async Task SendAsync_ExternalTestConsultant_SendsTheConsultantTemplateWithTheConsultantPlaceholders()
    {
        var harness = new Harness();
        var consultant = new CoreTestConsultant { ExternalTestConsultantId = Guid.NewGuid(), Name = "Tom Consultant", Email = "tom@example.org" };
        harness.TestConsultants.Seed(consultant);

        var result = await harness.Service.SendAsync(LoginInstructionUserType.ExternalTestConsultant, consultant.ExternalTestConsultantId);

        Assert.True(result.Success);
        var sent = Assert.Single(harness.Notify.Sent);
        Assert.Equal("d1f17c97-f031-4f9b-a537-d6916f3b2d50", sent.TemplateId);
        Assert.Equal("tom@example.org", sent.EmailAddress);
        Assert.Equal(new Dictionary<string, string> { ["ConsultantName"] = "Tom Consultant", ["ExternalAppLink"] = ExternalAppUrl }, sent.Personalisation);
    }

    [Fact]
    public async Task SendAsync_Participant_SendsTheParticipantTemplateWithNameLabCodeLabNameAndLink()
    {
        var harness = new Harness();
        var participant = new CoreParticipant
        {
            ParticipantId = Guid.NewGuid(),
            ContactName = "Pat Participant",
            LabCode = "0123",
            LabName = "North Lab",
            Email = "pat@example.org",
            Email2 = "other@example.org",
        };
        await harness.Participants.CreateAsync(participant);

        var result = await harness.Service.SendAsync(LoginInstructionUserType.Participant, participant.ParticipantId);

        Assert.True(result.Success);
        var sent = Assert.Single(harness.Notify.Sent);
        Assert.Equal("64091cdb-eaf5-4c55-8680-84a727b87c91", sent.TemplateId);
        Assert.Equal("pat@example.org", sent.EmailAddress);
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["ParticipantName"] = "Pat Participant",
                ["LabCode"] = "0123",
                ["LabName"] = "North Lab",
                ["ExternalAppLink"] = ExternalAppUrl,
            },
            sent.Personalisation);
    }

    [Fact]
    public async Task SendAsync_InactiveParticipant_SendsNothingAndRecordsNothing()
    {
        var harness = new Harness();
        var participant = new CoreParticipant { ParticipantId = Guid.NewGuid(), ContactName = "Pat Participant", Email = "pat@example.org", IsActive = false };
        await harness.Participants.CreateAsync(participant);

        var result = await harness.Service.SendAsync(LoginInstructionUserType.Participant, participant.ParticipantId);

        Assert.Equal(LoginInstructionsOutcome.NotActive, result.Outcome);
        Assert.False(result.Success);
        Assert.Empty(harness.Notify.Sent);
        Assert.Empty(harness.Repository.Marked);
    }

    [Fact]
    public async Task SendAsync_InactiveExternalTestConsultant_SendsNothingAndRecordsNothing()
    {
        var harness = new Harness();
        var consultant = new CoreTestConsultant { ExternalTestConsultantId = Guid.NewGuid(), Name = "Tom Consultant", Email = "tom@example.org", IsInactive = true };
        harness.TestConsultants.Seed(consultant);

        var result = await harness.Service.SendAsync(LoginInstructionUserType.ExternalTestConsultant, consultant.ExternalTestConsultantId);

        Assert.Equal(LoginInstructionsOutcome.NotActive, result.Outcome);
        Assert.Empty(harness.Notify.Sent);
        Assert.Empty(harness.Repository.Marked);
    }

    [Fact]
    public async Task SendAsync_AfterNotifyAcceptsTheEmail_PersistsTheSentStateWithDateAndAudit()
    {
        var harness = new Harness();
        var viewer = new ViewerEntity { ViewerId = Guid.NewGuid(), Name = "Vera Viewer", Email = "vera@example.org" };
        harness.Viewers.Viewers.Add(viewer);

        await harness.Service.SendAsync(LoginInstructionUserType.Viewer, viewer.ViewerId);

        var marked = Assert.Single(harness.Repository.Marked);
        Assert.Equal(LoginInstructionUserType.Viewer, marked.UserType);
        Assert.Equal(viewer.ViewerId, marked.Id);
        Assert.Equal(Now.UtcDateTime, marked.SentDate);
        Assert.Equal(new AuditEntry(harness.ActingUser.ActingUserId, "SendLoginInstructions", "Viewer", viewer.ViewerId.ToString(), "Template: viewer-login-instructions"), marked.Audit);
        Assert.Equal(CancellationToken.None, marked.Token);
    }

    [Fact]
    public async Task SendAsync_NotifyFails_ReturnsSendFailedAndDoesNotRecordItAsSent()
    {
        var harness = new Harness { Notify = { Failure = new InvalidOperationException("Notify is down") } };
        var viewer = new ViewerEntity { ViewerId = Guid.NewGuid(), Name = "Vera Viewer", Email = "vera@example.org" };
        harness.Viewers.Viewers.Add(viewer);

        var result = await harness.Service.SendAsync(LoginInstructionUserType.Viewer, viewer.ViewerId);

        Assert.Equal(LoginInstructionsOutcome.SendFailed, result.Outcome);
        Assert.False(result.Success);
        Assert.Empty(harness.Repository.Marked);
    }

    [Fact]
    public async Task SendAsync_Cancelled_PropagatesAndDoesNotRecordItAsSent()
    {
        var harness = new Harness { Notify = { Failure = new OperationCanceledException() } };
        var viewer = new ViewerEntity { ViewerId = Guid.NewGuid(), Name = "Vera Viewer", Email = "vera@example.org" };
        harness.Viewers.Viewers.Add(viewer);

        await Assert.ThrowsAsync<OperationCanceledException>(() => harness.Service.SendAsync(LoginInstructionUserType.Viewer, viewer.ViewerId));

        Assert.Empty(harness.Repository.Marked);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendAsync_NoEmailAddress_SendsNothingAndRecordsNothing(string email)
    {
        var harness = new Harness();
        var viewer = new ViewerEntity { ViewerId = Guid.NewGuid(), Name = "Vera Viewer", Email = email };
        harness.Viewers.Viewers.Add(viewer);

        var result = await harness.Service.SendAsync(LoginInstructionUserType.Viewer, viewer.ViewerId);

        Assert.Equal(LoginInstructionsOutcome.NoEmailAddress, result.Outcome);
        Assert.Empty(harness.Notify.Sent);
        Assert.Empty(harness.Repository.Marked);
    }

    [Theory]
    [InlineData(LoginInstructionUserType.Participant)]
    [InlineData(LoginInstructionUserType.Viewer)]
    [InlineData(LoginInstructionUserType.ExternalTestConsultant)]
    [InlineData(LoginInstructionUserType.LabUser)]
    public async Task SendAsync_UnknownRecord_ReturnsNotFound(LoginInstructionUserType userType)
    {
        var harness = new Harness();

        var result = await harness.Service.SendAsync(userType, Guid.NewGuid());

        Assert.Equal(LoginInstructionsOutcome.NotFound, result.Outcome);
        Assert.Empty(harness.Notify.Sent);
        Assert.Empty(harness.Repository.Marked);
    }

    [Fact]
    public async Task SendAsync_UnknownUserType_Throws()
    {
        var harness = new Harness();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => harness.Service.SendAsync((LoginInstructionUserType)99, Guid.NewGuid()));
    }

    private static LabUserLink NewLink(string email = "lara@example.org") => new()
    {
        LabUserParticipantId = Guid.NewGuid(),
        LabUserId = Guid.NewGuid(),
        ParticipantId = Guid.NewGuid(),
        Name = "Lara Lab",
        Email = email,
        IsActive = true,
        LabCode = "0142",
        LabName = "Bristol Veterinary Diagnostics"
    };

    // The id is the lab link's own id, because the email is about access to one lab.
    [Fact]
    public async Task SendAsync_LabUser_SendsTheLabUserTemplateNamingThePersonAndTheLab()
    {
        var harness = new Harness();
        var link = NewLink();
        harness.LabUsers.Links.Add(link);

        var result = await harness.Service.SendAsync(LoginInstructionUserType.LabUser, link.LabUserParticipantId);

        Assert.True(result.Success);
        var sent = Assert.Single(harness.Notify.Sent);
        Assert.Equal("72662826-7561-4506-ab41-b36a9035f1ea", sent.TemplateId);
        Assert.Equal("lara@example.org", sent.EmailAddress);
        Assert.Equal(link.LabUserParticipantId.ToString(), sent.Reference);
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["LabUserName"] = "Lara Lab",
                ["LabCode"] = "0142",
                ["LabName"] = "Bristol Veterinary Diagnostics",
                ["ExternalAppLink"] = ExternalAppUrl
            },
            sent.Personalisation);
        var marked = Assert.Single(harness.Repository.Marked);
        Assert.Equal((LoginInstructionUserType.LabUser, link.LabUserParticipantId), (marked.UserType, marked.Id));
        Assert.Equal("Template: lab-user-login-instructions", marked.Audit.Details);
    }

    [Theory]
    [InlineData(LoginInstructionsOutcome.Sent, true, null)]
    [InlineData(LoginInstructionsOutcome.NoEmailAddress, false, "There is no email address saved to send the login instructions to.")]
    [InlineData(LoginInstructionsOutcome.NotActive, false, "Login instructions can only be sent to an active user.")]
    [InlineData(LoginInstructionsOutcome.SendFailed, false, "The login instructions email could not be sent. Please try again.")]
    public void ToResponse_DescribesEachOutcome(LoginInstructionsOutcome outcome, bool success, string? message)
    {
        var response = outcome.ToResponse();

        Assert.Equal(success, response.Success);
        Assert.Equal(message, response.Message);
    }
}
