using PTL.Api.Tests.Participant;
using PTL.Contracts.LabUsers;
using PTL.Core.Audit;
using PTL.Core.LabUsers;
using PTL.Core.LoginInstructions;
using CoreParticipant = PTL.Core.Participant.Participant;

namespace PTL.Api.Tests.LabUsers;

public class LabUserServiceTests
{
    private sealed class RecordingLoginInstructionsService : ILoginInstructionsService
    {
        public LoginInstructionsOutcome Outcome { get; set; } = LoginInstructionsOutcome.Sent;

        public List<(LoginInstructionUserType UserType, Guid Id)> Calls { get; } = [];

        public Task<LoginInstructionsResult> SendAsync(LoginInstructionUserType userType, Guid id, CancellationToken cancellationToken = default)
        {
            Calls.Add((userType, id));
            return Task.FromResult(new LoginInstructionsResult(Outcome));
        }
    }

    private readonly FakeLabUserRepository _labUsers = new();
    private readonly FakeParticipantRepository _participants = new();
    private readonly RecordingLoginInstructionsService _loginInstructions = new();
    private CoreParticipant _lab = new();

    private LabUserService CreateService() => new(_labUsers, _participants, _loginInstructions);

    private async Task<CoreParticipant> AddLabAsync(bool isActive = true)
    {
        _lab = await _participants.CreateAsync(new CoreParticipant { LabCode = "0142", LabName = "Bristol Veterinary Diagnostics", IsActive = isActive });
        return _lab;
    }

    private LabUserLink AddLink(Guid participantId, string name = "Emma Wright", string email = "emma@example.org", bool isActive = true, int otherLabs = 0, bool signedIn = false, bool sent = false)
    {
        var link = new LabUserLink
        {
            LabUserParticipantId = Guid.NewGuid(),
            LabUserId = Guid.NewGuid(),
            ParticipantId = participantId,
            Name = name,
            Email = email,
            IsActive = isActive,
            OtherLabCount = otherLabs,
            HasSignedIn = signedIn,
            LoginInstructionsSent = sent,
            LabCode = "0142",
            LabName = "Bristol Veterinary Diagnostics"
        };
        _labUsers.Links.Add(link);
        return link;
    }

    // ---- listing ----

    [Fact]
    public async Task GetListAsync_ReturnsTheLabAndItsPeopleByName_MarkingWhoseDetailsAreShared()
    {
        var lab = await AddLabAsync();
        AddLink(lab.ParticipantId, "Tom Baker", "tom@example.org", isActive: false, sent: true);
        AddLink(lab.ParticipantId, "Emma Wright", "emma@example.org", otherLabs: 2, signedIn: true);
        AddLink(Guid.NewGuid(), "Someone Elsewhere", "else@example.org");

        var list = await CreateService().GetListAsync(lab.ParticipantId);

        Assert.NotNull(list);
        Assert.Equal("0142", list.LabCode);
        Assert.Equal("Bristol Veterinary Diagnostics", list.LabName);
        Assert.Equal(["Emma Wright", "Tom Baker"], list.Users.Select(user => user.Name));
        var emma = list.Users[0];
        Assert.False(emma.CanChangeDetails);
        Assert.True(emma.HasSignedIn);
        Assert.True(emma.IsActive);
        var tom = list.Users[1];
        Assert.True(tom.CanChangeDetails);
        Assert.False(tom.IsActive);
        Assert.True(tom.LoginInstructionsSent);
    }

    [Fact]
    public async Task GetListAsync_LabWithNobody_ReturnsAnEmptyListForAKnownLab()
    {
        var lab = await AddLabAsync();

        var list = await CreateService().GetListAsync(lab.ParticipantId);

        Assert.NotNull(list);
        Assert.Empty(list.Users);
    }

    [Fact]
    public async Task GetListAsync_UnknownLab_ReturnsNull()
    {
        Assert.Null(await CreateService().GetListAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetAsync_PersonOfThisLab_ReturnsThemWithTheLab()
    {
        var lab = await AddLabAsync();
        var link = AddLink(lab.ParticipantId);

        var details = await CreateService().GetAsync(lab.ParticipantId, link.LabUserId);

        Assert.NotNull(details);
        Assert.Equal("0142", details.LabCode);
        Assert.Equal("emma@example.org", details.User.Email);
        Assert.True(details.User.CanChangeDetails);
    }

    [Fact]
    public async Task GetAsync_PersonOfAnotherLab_IsNotFound()
    {
        var lab = await AddLabAsync();
        var elsewhere = AddLink(Guid.NewGuid());

        Assert.Null(await CreateService().GetAsync(lab.ParticipantId, elsewhere.LabUserId));
    }

    // ---- adding ----

    [Fact]
    public async Task AddAsync_NewPerson_AddsThemTrimmedAndAuditsWithTheirId()
    {
        var lab = await AddLabAsync();
        var labUserId = Guid.NewGuid();
        _labUsers.AddResult = new LabUserAddResult(LabUserAddOutcome.Created, labUserId);

        var result = await CreateService().AddAsync(lab.ParticipantId, new LabUserSaveRequest("  Ramesh Mehara ", " ramesh@lab.com  ", true));

        Assert.NotNull(result);
        Assert.Equal(LabUserSaveOutcome.Added, result.Outcome);
        Assert.True(result.Success);
        Assert.Equal((lab.ParticipantId, "Ramesh Mehara", "ramesh@lab.com", true), Assert.Single(_labUsers.AddCalls));
        var audit = Assert.Single(_labUsers.AddAudits);
        Assert.Equal(new AuditEntry(lab.ParticipantId, "AddLabUser", "LabUser", labUserId.ToString(), "Lab: 0142; Name: Ramesh Mehara; Active: True"), audit);
    }

    [Fact]
    public async Task AddAsync_InactiveFromTheStart_PassesActiveFalse()
    {
        var lab = await AddLabAsync();

        await CreateService().AddAsync(lab.ParticipantId, new LabUserSaveRequest("Ramesh", "ramesh@lab.com", false));

        Assert.False(Assert.Single(_labUsers.AddCalls).IsActive);
    }

    [Fact]
    public async Task AddAsync_ExistingAccount_ReportsTheLinkWasAddedToTheExistingPerson()
    {
        var lab = await AddLabAsync();
        _labUsers.AddResult = new LabUserAddResult(LabUserAddOutcome.AddedExistingAccount, Guid.NewGuid());

        var result = await CreateService().AddAsync(lab.ParticipantId, new LabUserSaveRequest("Ramesh", "ramesh@lab.com", true));

        Assert.Equal(LabUserSaveOutcome.AddedExistingAccount, result!.Outcome);
    }

    [Fact]
    public async Task AddAsync_AlreadyLinkedToThisLab_FailsOnTheEmailField()
    {
        var lab = await AddLabAsync();
        _labUsers.AddResult = new LabUserAddResult(LabUserAddOutcome.AlreadyLinked, Guid.NewGuid());

        var result = await CreateService().AddAsync(lab.ParticipantId, new LabUserSaveRequest("Ramesh", "ramesh@lab.com", true));

        Assert.False(result!.Success);
        Assert.Contains("already a lab user for this lab", result.FieldErrors["Email"][0]);
    }

    [Fact]
    public async Task AddAsync_EmailOfAnotherKindOfUser_FailsOnTheEmailField()
    {
        var lab = await AddLabAsync();
        _labUsers.AddResult = new LabUserAddResult(LabUserAddOutcome.EmailUsedByOtherUserType, null);

        var result = await CreateService().AddAsync(lab.ParticipantId, new LabUserSaveRequest("Ramesh", "viewer@lab.com", true));

        Assert.False(result!.Success);
        Assert.Contains("different type of PT-LIMS user", result.FieldErrors["Email"][0]);
        Assert.Empty(_labUsers.AddAudits);
    }

    [Theory]
    [InlineData("", "ramesh@lab.com", "Name")]
    [InlineData("   ", "ramesh@lab.com", "Name")]
    [InlineData("Ramesh", "", "Email")]
    [InlineData("Ramesh", "not-an-email", "Email")]
    [InlineData("Ramesh", "two@@lab.com", "Email")]
    [InlineData("Ramesh", "spaces in@lab.com", "Email")]
    public async Task AddAsync_InvalidDetails_FailsWithoutTouchingTheDatabase(string name, string email, string field)
    {
        var lab = await AddLabAsync();

        var result = await CreateService().AddAsync(lab.ParticipantId, new LabUserSaveRequest(name, email, true));

        Assert.False(result!.Success);
        Assert.Contains(field, result.FieldErrors.Keys);
        Assert.Empty(_labUsers.AddCalls);
    }

    [Fact]
    public async Task AddAsync_InactiveLab_IsRefused()
    {
        var lab = await AddLabAsync(isActive: false);

        var result = await CreateService().AddAsync(lab.ParticipantId, new LabUserSaveRequest("Ramesh", "ramesh@lab.com", true));

        Assert.False(result!.Success);
        Assert.Equal("This lab is not available.", result.FieldErrors[string.Empty][0]);
        Assert.Empty(_labUsers.AddCalls);
    }

    [Fact]
    public async Task AddAsync_UnknownLab_ReturnsNull()
    {
        Assert.Null(await CreateService().AddAsync(Guid.NewGuid(), new LabUserSaveRequest("Ramesh", "ramesh@lab.com", true)));
    }

    // ---- editing ----

    [Fact]
    public async Task UpdateAsync_OnlyTheAccessChanged_DoesNotChangeTheDetails()
    {
        var lab = await AddLabAsync();
        var link = AddLink(lab.ParticipantId, otherLabs: 1);

        var result = await CreateService().UpdateAsync(lab.ParticipantId, link.LabUserId, new LabUserSaveRequest("Emma Wright", "EMMA@example.org", false));

        Assert.Equal(LabUserSaveOutcome.Updated, result!.Outcome);
        var call = Assert.Single(_labUsers.UpdateCalls);
        Assert.False(call.ChangeDetails);
        Assert.False(call.IsActive);
        Assert.Equal(new AuditEntry(lab.ParticipantId, "UpdateLabUser", "LabUser", link.LabUserId.ToString(), "Lab: 0142; Name: Emma Wright; Active: False; DetailsChanged: False"), call.Audit);
    }

    [Theory]
    [InlineData("Emma W", "emma@example.org")]
    [InlineData("Emma Wright", "emma.new@example.org")]
    public async Task UpdateAsync_NameOrEmailChanged_ChangesTheDetails(string name, string email)
    {
        var lab = await AddLabAsync();
        var link = AddLink(lab.ParticipantId);

        await CreateService().UpdateAsync(lab.ParticipantId, link.LabUserId, new LabUserSaveRequest(name, email, true));

        var call = Assert.Single(_labUsers.UpdateCalls);
        Assert.True(call.ChangeDetails);
        Assert.Equal((name, email), (call.Name, call.Email));
    }

    [Fact]
    public async Task UpdateAsync_EmailChanged_ReportsItSoThePersonCanBeToldToSignInAgain()
    {
        var lab = await AddLabAsync();
        var link = AddLink(lab.ParticipantId, signedIn: true);
        _labUsers.UpdateOutcome = LabUserUpdateOutcome.UpdatedEmailChanged;

        var result = await CreateService().UpdateAsync(lab.ParticipantId, link.LabUserId, new LabUserSaveRequest("Emma Wright", "new@example.org", true));

        Assert.Equal(LabUserSaveOutcome.UpdatedEmailChanged, result!.Outcome);
    }

    [Theory]
    [InlineData("Emma W", "emma@example.org")]
    [InlineData("Emma Wright", "new@example.org")]
    public async Task UpdateAsync_DetailsOfAPersonSharedWithOtherLabs_AreRefusedWithoutTouchingTheDatabase(string name, string email)
    {
        var lab = await AddLabAsync();
        var link = AddLink(lab.ParticipantId, otherLabs: 1);

        var result = await CreateService().UpdateAsync(lab.ParticipantId, link.LabUserId, new LabUserSaveRequest(name, email, true));

        Assert.False(result!.Success);
        Assert.Contains("also works with other labs", result.FieldErrors[string.Empty][0]);
        Assert.Empty(_labUsers.UpdateCalls);
    }

    [Theory]
    [InlineData(LabUserUpdateOutcome.EmailUsedByOtherLabUser, "Email", "Another lab user already uses this email address.")]
    [InlineData(LabUserUpdateOutcome.EmailUsedByOtherUserType, "Email", "This email address already belongs to a different type of PT-LIMS user, so it cannot be used for a lab user.")]
    [InlineData(LabUserUpdateOutcome.SharedWithOtherLabs, "", "This person also works with other labs, so their name and email address cannot be changed here. You can still make them active or inactive for this lab.")]
    public async Task UpdateAsync_RefusedByTheDatabase_FailsWithTheMatchingMessage(LabUserUpdateOutcome outcome, string field, string message)
    {
        var lab = await AddLabAsync();
        var link = AddLink(lab.ParticipantId);
        _labUsers.UpdateOutcome = outcome;

        var result = await CreateService().UpdateAsync(lab.ParticipantId, link.LabUserId, new LabUserSaveRequest("Emma Wright", "new@example.org", true));

        Assert.False(result!.Success);
        Assert.Equal(message, result.FieldErrors[field][0]);
    }

    [Fact]
    public async Task UpdateAsync_PersonRemovedMeanwhile_ReturnsNull()
    {
        var lab = await AddLabAsync();
        var link = AddLink(lab.ParticipantId);
        _labUsers.UpdateOutcome = LabUserUpdateOutcome.NotFound;

        Assert.Null(await CreateService().UpdateAsync(lab.ParticipantId, link.LabUserId, new LabUserSaveRequest("Emma Wright", "emma@example.org", true)));
    }

    [Fact]
    public async Task UpdateAsync_PersonOfAnotherLab_ReturnsNullWithoutUpdating()
    {
        var lab = await AddLabAsync();
        var elsewhere = AddLink(Guid.NewGuid());

        Assert.Null(await CreateService().UpdateAsync(lab.ParticipantId, elsewhere.LabUserId, new LabUserSaveRequest("x", "x@example.org", true)));
        Assert.Empty(_labUsers.UpdateCalls);
    }

    [Fact]
    public async Task UpdateAsync_InvalidDetails_FailsWithoutTouchingTheDatabase()
    {
        var lab = await AddLabAsync();
        var link = AddLink(lab.ParticipantId);

        var result = await CreateService().UpdateAsync(lab.ParticipantId, link.LabUserId, new LabUserSaveRequest("", "bad", true));

        Assert.False(result!.Success);
        Assert.Contains("Name", result.FieldErrors.Keys);
        Assert.Contains("Email", result.FieldErrors.Keys);
        Assert.Empty(_labUsers.UpdateCalls);
    }

    // ---- login instructions ----

    [Fact]
    public async Task SendLoginInstructionsAsync_ActivePersonOfThisLab_SendsByTheLinkId()
    {
        var lab = await AddLabAsync();
        var link = AddLink(lab.ParticipantId);

        var outcome = await CreateService().SendLoginInstructionsAsync(lab.ParticipantId, link.LabUserId);

        Assert.Equal(LoginInstructionsOutcome.Sent, outcome);
        Assert.Equal((LoginInstructionUserType.LabUser, link.LabUserParticipantId), Assert.Single(_loginInstructions.Calls));
    }

    [Fact]
    public async Task SendLoginInstructionsAsync_SendFails_PassesTheOutcomeOn()
    {
        var lab = await AddLabAsync();
        var link = AddLink(lab.ParticipantId);
        _loginInstructions.Outcome = LoginInstructionsOutcome.SendFailed;

        Assert.Equal(LoginInstructionsOutcome.SendFailed, await CreateService().SendLoginInstructionsAsync(lab.ParticipantId, link.LabUserId));
    }

    [Fact]
    public async Task SendLoginInstructionsAsync_InactivePerson_SendsNothing()
    {
        var lab = await AddLabAsync();
        var link = AddLink(lab.ParticipantId, isActive: false);

        var outcome = await CreateService().SendLoginInstructionsAsync(lab.ParticipantId, link.LabUserId);

        Assert.Equal(LoginInstructionsOutcome.NotActive, outcome);
        Assert.Empty(_loginInstructions.Calls);
    }

    [Fact]
    public async Task SendLoginInstructionsAsync_PersonOfAnotherLab_IsNotFoundAndSendsNothing()
    {
        var lab = await AddLabAsync();
        var elsewhere = AddLink(Guid.NewGuid());

        Assert.Null(await CreateService().SendLoginInstructionsAsync(lab.ParticipantId, elsewhere.LabUserId));
        Assert.Empty(_loginInstructions.Calls);
    }
}
