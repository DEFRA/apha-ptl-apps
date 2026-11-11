using Microsoft.Extensions.Logging;
using PTL.Api.Tests.Customer;
using PTL.Api.Tests.Distribution;
using PTL.Api.Tests.Participant;
using PTL.Contracts.ExternalUser;
using PTL.Core.ExternalUser;
using CoreCustomer = PTL.Core.Customer.Customer;
using CoreParticipant = PTL.Core.Participant.Participant;

namespace PTL.Api.Tests.ExternalUser;

public class ExternalUserServiceTests
{
    private const string Email = "user@example.com";
    private const string DisplayName = "Jane Doe";

    private readonly FakeExternalUserRepository _users = new();
    private readonly FakeParticipantRepository _participants = new();
    private readonly FakeCustomerRepository _customers = new();
    private readonly MessageCapturingLogger<ExternalUserService> _logger = new();

    private ExternalUserService CreateService() => new(_users, _participants, _customers, _logger);

    private Task<ExternalUserResolutionResult> ResolveAsync(Guid ssoIdExt, string email = Email) =>
        CreateService().ResolveAsync(ssoIdExt, email, DisplayName);

    private async Task<Guid> AddLabAsync(Guid customerId, string labCode, bool isActive = true)
    {
        var participant = await _participants.CreateAsync(new CoreParticipant { CustomerId = customerId, LabCode = labCode, IsActive = isActive });
        return participant.ParticipantId;
    }

    private void AddParticipantRow(Guid ssoIdExt, Guid labId, string labCode, bool isActive = true) =>
        _users.Add(ExternalUserType.Participant, ssoIdExt: ssoIdExt, isActive: isActive, userId: labId, labParticipantId: labId, labCode: labCode, labName: $"{labCode} Lab");

    // ---- returning users (found by contact id) ----

    [Fact]
    public async Task ResolveAsync_ReturningViewer_ResolvesByContactIdWithoutTouchingTheEmail()
    {
        var ssoIdExt = Guid.NewGuid();
        var viewer = _users.Add(ExternalUserType.Viewer, ssoIdExt: ssoIdExt);

        var result = await ResolveAsync(ssoIdExt);

        Assert.True(result.IsResolved);
        Assert.Equal(ExternalUserType.Viewer, result.UserType);
        Assert.Equal([ExternalRoleNames.Viewer], result.Roles);
        Assert.Equal(viewer.UserId, result.ViewerId);
        Assert.Null(result.TestConsultantId);
        Assert.Null(result.LabUserId);
        Assert.Null(result.ParticipantId);
        Assert.Empty(result.Labs!);
        Assert.Equal(DisplayName, result.DisplayName);
        Assert.Equal(0, _users.FindByEmailCalls);
        Assert.Equal(0, _users.LinkCalls);
    }

    [Fact]
    public async Task ResolveAsync_ReturningTestConsultant_GrantsTestConsultantAndImpliedViewerRole()
    {
        var ssoIdExt = Guid.NewGuid();
        var consultant = _users.Add(ExternalUserType.TestConsultant, ssoIdExt: ssoIdExt);

        var result = await ResolveAsync(ssoIdExt);

        Assert.Equal(ExternalUserType.TestConsultant, result.UserType);
        Assert.Equal([ExternalRoleNames.TestConsultant, ExternalRoleNames.Viewer], result.Roles);
        Assert.Equal(consultant.UserId, result.TestConsultantId);
        Assert.Null(result.ViewerId);
    }

    [Fact]
    public async Task ResolveAsync_ReturningParticipantWithOneLab_GrantsParticipantAndImpliedViewerRoleAndActsForTheLab()
    {
        var ssoIdExt = Guid.NewGuid();
        var labId = await AddLabAsync(Guid.NewGuid(), "LAB001");
        AddParticipantRow(ssoIdExt, labId, "LAB001");

        var result = await ResolveAsync(ssoIdExt);

        Assert.Equal(ExternalUserType.Participant, result.UserType);
        Assert.Equal([ExternalRoleNames.Participant, ExternalRoleNames.Viewer], result.Roles);
        Assert.Equal(labId, result.ParticipantId);
        Assert.Equal("LAB001", result.LabCode);
        Assert.Null(result.ViewerId);
        Assert.Null(result.LabUserId);
        var lab = Assert.Single(result.Labs!);
        Assert.Equal(new ExternalLab(labId, "LAB001", "LAB001 Lab"), lab);
    }

    [Fact]
    public async Task ResolveAsync_ReturningParticipantWithSeveralLabs_ListsThemAndSelectsNone()
    {
        var ssoIdExt = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var first = await AddLabAsync(customerId, "LAB001");
        var second = await AddLabAsync(customerId, "LAB002");
        AddParticipantRow(ssoIdExt, first, "LAB001");
        AddParticipantRow(ssoIdExt, second, "LAB002");

        var result = await ResolveAsync(ssoIdExt);

        Assert.True(result.IsResolved);
        Assert.Equal([first, second], result.Labs!.Select(lab => lab.ParticipantId));
        Assert.Null(result.ParticipantId);
        Assert.Null(result.LabCode);
        Assert.False(result.CanOrderOnline);
    }

    [Fact]
    public async Task ResolveAsync_ReturningParticipantWithOneActiveAndOneInactiveLab_ActsForTheActiveLabOnly()
    {
        var ssoIdExt = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var active = await AddLabAsync(customerId, "LAB001");
        var inactive = await AddLabAsync(customerId, "LAB002", isActive: false);
        AddParticipantRow(ssoIdExt, active, "LAB001");
        AddParticipantRow(ssoIdExt, inactive, "LAB002", isActive: false);

        var result = await ResolveAsync(ssoIdExt);

        Assert.Equal(active, result.ParticipantId);
        Assert.Equal(active, Assert.Single(result.Labs!).ParticipantId);
    }

    [Fact]
    public async Task ResolveAsync_ReturningParticipantWhoseEveryLabIsInactive_IsDeniedAsInactive()
    {
        var ssoIdExt = Guid.NewGuid();
        AddParticipantRow(ssoIdExt, Guid.NewGuid(), "LAB001", isActive: false);

        var result = await ResolveAsync(ssoIdExt);

        Assert.Equal(ExternalUserResolutionStatus.Inactive, result.Status);
        Assert.False(result.IsResolved);
        Assert.Null(result.Roles);
        Assert.Equal(DisplayName, result.DisplayName);
    }

    [Fact]
    public async Task ResolveAsync_ReturningInactiveTestConsultant_IsDeniedAsInactive()
    {
        var ssoIdExt = Guid.NewGuid();
        _users.Add(ExternalUserType.TestConsultant, ssoIdExt: ssoIdExt, isActive: false);

        var result = await ResolveAsync(ssoIdExt);

        Assert.Equal(ExternalUserResolutionStatus.Inactive, result.Status);
    }

    [Fact]
    public async Task ResolveAsync_ContactLinkedInTwoTables_TheFirstTableDecidesAndTheOtherIsIgnored()
    {
        var ssoIdExt = Guid.NewGuid();
        _users.Add(ExternalUserType.Viewer, ssoIdExt: ssoIdExt);
        var labId = await AddLabAsync(Guid.NewGuid(), "LAB001");
        AddParticipantRow(ssoIdExt, labId, "LAB001");

        var result = await ResolveAsync(ssoIdExt);

        Assert.Equal(ExternalUserType.Participant, result.UserType);
        Assert.Null(result.ViewerId);
    }

    // ---- lab users ----

    [Fact]
    public async Task ResolveAsync_LabUserWithOneActiveLab_IsLabUserWithParticipantAccessToThatLab()
    {
        var ssoIdExt = Guid.NewGuid();
        var labId = await AddLabAsync(Guid.NewGuid(), "LAB001");
        var labUser = _users.Add(ExternalUserType.LabUser, ssoIdExt: ssoIdExt, labParticipantId: labId, labCode: "LAB001", labName: "North Lab");

        var result = await ResolveAsync(ssoIdExt);

        Assert.Equal(ExternalUserType.LabUser, result.UserType);
        Assert.Equal([ExternalRoleNames.LabUser, ExternalRoleNames.Participant, ExternalRoleNames.Viewer], result.Roles);
        Assert.Equal(labUser.UserId, result.LabUserId);
        Assert.Equal(labId, result.ParticipantId);
        Assert.Equal("LAB001", result.LabCode);
        Assert.Null(result.ViewerId);
        Assert.Null(result.TestConsultantId);
    }

    [Fact]
    public async Task ResolveAsync_LabUserWithSeveralActiveLabs_ListsThemAndSelectsNone()
    {
        var ssoIdExt = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        _users.Add(ExternalUserType.LabUser, ssoIdExt: ssoIdExt, userId: userId, labParticipantId: first, labCode: "LAB001", labName: "A");
        _users.Add(ExternalUserType.LabUser, ssoIdExt: ssoIdExt, userId: userId, labParticipantId: second, labCode: "LAB002", labName: "B");

        var result = await ResolveAsync(ssoIdExt);

        Assert.True(result.IsResolved);
        Assert.Equal(userId, result.LabUserId);
        Assert.Equal(2, result.Labs!.Count);
        Assert.Null(result.ParticipantId);
        Assert.Null(result.LabCode);
        Assert.False(result.CanOrderOnline);
    }

    [Fact]
    public async Task ResolveAsync_LabUserWithOneActiveAndOneInactiveLab_ActsForTheActiveLab()
    {
        var ssoIdExt = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var active = await AddLabAsync(Guid.NewGuid(), "LAB001");
        _users.Add(ExternalUserType.LabUser, ssoIdExt: ssoIdExt, userId: userId, labParticipantId: active, labCode: "LAB001", labName: "A");
        _users.Add(ExternalUserType.LabUser, ssoIdExt: ssoIdExt, userId: userId, isActive: false, labParticipantId: Guid.NewGuid(), labCode: "LAB002", labName: "B");

        var result = await ResolveAsync(ssoIdExt);

        Assert.Equal(active, result.ParticipantId);
        Assert.Single(result.Labs!);
    }

    [Fact]
    public async Task ResolveAsync_LabUserWithNoActiveLab_IsDeniedAsNoActiveLab()
    {
        var ssoIdExt = Guid.NewGuid();
        _users.Add(ExternalUserType.LabUser, ssoIdExt: ssoIdExt, isActive: false, labParticipantId: Guid.NewGuid(), labCode: "LAB001", labName: "A");

        var result = await ResolveAsync(ssoIdExt);

        Assert.Equal(ExternalUserResolutionStatus.NoActiveLab, result.Status);
    }

    [Fact]
    public async Task ResolveAsync_LabUserWithNoLabLinksAtAll_IsDeniedAsNoActiveLab()
    {
        var ssoIdExt = Guid.NewGuid();
        _users.Add(ExternalUserType.LabUser, ssoIdExt: ssoIdExt, isActive: false);

        var result = await ResolveAsync(ssoIdExt);

        Assert.Equal(ExternalUserResolutionStatus.NoActiveLab, result.Status);
    }

    [Fact]
    public async Task ResolveAsync_SameLabReturnedTwice_ListsItOnce()
    {
        var ssoIdExt = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var labId = await AddLabAsync(Guid.NewGuid(), "LAB001");
        _users.Add(ExternalUserType.LabUser, ssoIdExt: ssoIdExt, userId: userId, labParticipantId: labId, labCode: "LAB001", labName: "A");
        _users.Add(ExternalUserType.LabUser, ssoIdExt: ssoIdExt, userId: userId, labParticipantId: labId, labCode: "LAB001", labName: "A");

        var result = await ResolveAsync(ssoIdExt);

        Assert.Single(result.Labs!);
        Assert.Equal(labId, result.ParticipantId);
    }

    [Fact]
    public async Task ResolveAsync_LabWithoutCodeOrName_FallsBackToEmptyStrings()
    {
        var ssoIdExt = Guid.NewGuid();
        var labId = Guid.NewGuid();
        _users.Add(ExternalUserType.LabUser, ssoIdExt: ssoIdExt, labParticipantId: labId);

        var result = await ResolveAsync(ssoIdExt);

        Assert.Equal(new ExternalLab(labId, string.Empty, string.Empty), Assert.Single(result.Labs!));
        Assert.Equal(string.Empty, result.LabCode);
    }

    // ---- online ordering (only evaluated for a single lab) ----

    [Fact]
    public async Task ResolveAsync_SingleLabWhoseCustomerCanOrderOnline_CanOrderOnline()
    {
        var ssoIdExt = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        await _customers.CreateAsync(new CoreCustomer { CustomerId = customerId, CanOrderOnline = true });
        var labId = await AddLabAsync(customerId, "LAB001");
        AddParticipantRow(ssoIdExt, labId, "LAB001");

        var result = await ResolveAsync(ssoIdExt);

        Assert.True(result.CanOrderOnline);
    }

    [Fact]
    public async Task ResolveAsync_SingleLabWhoseCustomerCannotOrderOnline_CannotOrderOnline()
    {
        var ssoIdExt = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        await _customers.CreateAsync(new CoreCustomer { CustomerId = customerId, CanOrderOnline = false });
        var labId = await AddLabAsync(customerId, "LAB001");
        AddParticipantRow(ssoIdExt, labId, "LAB001");

        var result = await ResolveAsync(ssoIdExt);

        Assert.False(result.CanOrderOnline);
    }

    [Fact]
    public async Task ResolveAsync_SingleLabWithAnotherActiveParticipantOnTheCustomer_CannotOrderOnline()
    {
        var ssoIdExt = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        await _customers.CreateAsync(new CoreCustomer { CustomerId = customerId, CanOrderOnline = true });
        var labId = await AddLabAsync(customerId, "LAB001");
        await AddLabAsync(customerId, "LAB002");
        AddParticipantRow(ssoIdExt, labId, "LAB001");

        var result = await ResolveAsync(ssoIdExt);

        Assert.False(result.CanOrderOnline);
    }

    [Fact]
    public async Task ResolveAsync_SingleLabWithAnotherParticipantOnTheCustomerInactive_CanOrderOnline()
    {
        var ssoIdExt = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        await _customers.CreateAsync(new CoreCustomer { CustomerId = customerId, CanOrderOnline = true });
        var labId = await AddLabAsync(customerId, "LAB001");
        await AddLabAsync(customerId, "LAB002", isActive: false);
        AddParticipantRow(ssoIdExt, labId, "LAB001");

        var result = await ResolveAsync(ssoIdExt);

        Assert.True(result.CanOrderOnline);
    }

    [Fact]
    public async Task ResolveAsync_SingleLabWhoseCustomerIsMissing_CannotOrderOnline()
    {
        var ssoIdExt = Guid.NewGuid();
        var labId = await AddLabAsync(Guid.NewGuid(), "LAB001");
        AddParticipantRow(ssoIdExt, labId, "LAB001");

        var result = await ResolveAsync(ssoIdExt);

        Assert.True(result.IsResolved);
        Assert.False(result.CanOrderOnline);
    }

    [Fact]
    public async Task ResolveAsync_SingleLabWhoseParticipantRecordIsMissing_StillResolvesButCannotOrderOnline()
    {
        var ssoIdExt = Guid.NewGuid();
        AddParticipantRow(ssoIdExt, Guid.NewGuid(), "LAB001");

        var result = await ResolveAsync(ssoIdExt);

        Assert.True(result.IsResolved);
        Assert.False(result.CanOrderOnline);
    }

    // ---- first sign-in (linked by email) ----

    [Theory]
    [InlineData(ExternalUserType.Participant)]
    [InlineData(ExternalUserType.Viewer)]
    [InlineData(ExternalUserType.TestConsultant)]
    [InlineData(ExternalUserType.LabUser)]
    public async Task ResolveAsync_FirstSignInMatchingOneUserByEmail_LinksTheContactIdAndResolvesThatType(ExternalUserType userType)
    {
        var ssoIdExt = Guid.NewGuid();
        _users.Add(userType);

        var result = await ResolveAsync(ssoIdExt);

        Assert.True(result.IsResolved);
        Assert.Equal(userType, result.UserType);
        Assert.Equal(1, _users.LinkCalls);
        Assert.Equal(ssoIdExt, _users.Rows.Single().SsoIdExt);
    }

    [Fact]
    public async Task ResolveAsync_FirstSignInThenSecondSignIn_SecondUsesOnlyTheContactId()
    {
        var ssoIdExt = Guid.NewGuid();
        _users.Add(ExternalUserType.Viewer);
        await ResolveAsync(ssoIdExt);
        var emailCallsAfterFirst = _users.FindByEmailCalls;

        var second = await ResolveAsync(ssoIdExt);

        Assert.True(second.IsResolved);
        Assert.Equal(emailCallsAfterFirst, _users.FindByEmailCalls);
        Assert.Equal(1, _users.LinkCalls);
    }

    [Fact]
    public async Task ResolveAsync_FirstSignInParticipantWithSeveralLabs_LinksEveryLabRow()
    {
        var ssoIdExt = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var first = await AddLabAsync(customerId, "LAB001");
        var second = await AddLabAsync(customerId, "LAB002");
        _users.Add(ExternalUserType.Participant, userId: first, labParticipantId: first, labCode: "LAB001", labName: "A");
        _users.Add(ExternalUserType.Participant, userId: second, labParticipantId: second, labCode: "LAB002", labName: "B");

        var result = await ResolveAsync(ssoIdExt);

        Assert.True(result.IsResolved);
        Assert.Equal(2, result.Labs!.Count);
        Assert.Null(result.ParticipantId);
        Assert.All(_users.Rows, row => Assert.Equal(ssoIdExt, row.SsoIdExt));
        Assert.Equal(1, _users.LinkCalls);
    }

    [Fact]
    public async Task ResolveAsync_FirstSignInEmailInSeveralTables_TheFirstTableWinsAndTheOthersStayUnlinked()
    {
        var ssoIdExt = Guid.NewGuid();
        var labId = await AddLabAsync(Guid.NewGuid(), "LAB001");
        _users.Add(ExternalUserType.LabUser, labParticipantId: labId, labCode: "LAB001", labName: "A");
        _users.Add(ExternalUserType.Viewer);
        _users.Add(ExternalUserType.Participant, userId: labId, labParticipantId: labId, labCode: "LAB001", labName: "A");

        var result = await ResolveAsync(ssoIdExt);

        Assert.Equal(ExternalUserType.Participant, result.UserType);
        Assert.Equal(ssoIdExt, _users.Rows.Single(row => row.UserType == ExternalUserType.Participant).SsoIdExt);
        Assert.Null(_users.Rows.Single(row => row.UserType == ExternalUserType.Viewer).SsoIdExt);
        Assert.Null(_users.Rows.Single(row => row.UserType == ExternalUserType.LabUser).SsoIdExt);
    }

    [Fact]
    public async Task ResolveAsync_FirstSignInEmailDiffersOnlyByCaseAndPadding_StillMatches()
    {
        var ssoIdExt = Guid.NewGuid();
        _users.Add(ExternalUserType.Viewer, email: "Jane.Doe@Example.com");

        var result = await ResolveAsync(ssoIdExt, "  jane.doe@example.com ");

        Assert.True(result.IsResolved);
    }

    [Fact]
    public async Task ResolveAsync_FirstSignInLabUserWithSeveralLabs_IsNotAmbiguousBecauseItIsOnePerson()
    {
        var ssoIdExt = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _users.Add(ExternalUserType.LabUser, userId: userId, labParticipantId: Guid.NewGuid(), labCode: "LAB001", labName: "A");
        _users.Add(ExternalUserType.LabUser, userId: userId, labParticipantId: Guid.NewGuid(), labCode: "LAB002", labName: "B");

        var result = await ResolveAsync(ssoIdExt);

        Assert.True(result.IsResolved);
        Assert.Equal(2, result.Labs!.Count);
    }

    [Fact]
    public async Task ResolveAsync_FirstSignInInactiveMatch_LinksButIsDeniedAsInactive()
    {
        var ssoIdExt = Guid.NewGuid();
        _users.Add(ExternalUserType.TestConsultant, isActive: false);

        var result = await ResolveAsync(ssoIdExt);

        Assert.Equal(ExternalUserResolutionStatus.Inactive, result.Status);
        Assert.Equal(ssoIdExt, _users.Rows.Single().SsoIdExt);
    }

    [Fact]
    public async Task ResolveAsync_FirstSignInLabUserWithNoActiveLab_IsDeniedAsNoActiveLab()
    {
        _users.Add(ExternalUserType.LabUser, isActive: false, labParticipantId: Guid.NewGuid(), labCode: "LAB001", labName: "A");

        var result = await ResolveAsync(Guid.NewGuid());

        Assert.Equal(ExternalUserResolutionStatus.NoActiveLab, result.Status);
    }

    // ---- denials ----

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ResolveAsync_NoEmailAndNoLinkedRecord_IsNotFoundWithoutAnyEmailLookup(string email)
    {
        _users.Add(ExternalUserType.Viewer, email: string.Empty);

        var result = await ResolveAsync(Guid.NewGuid(), email);

        Assert.Equal(ExternalUserResolutionStatus.NotFound, result.Status);
        Assert.Equal(0, _users.FindByEmailCalls);
        Assert.Equal(0, _users.LinkCalls);
    }

    [Fact]
    public async Task ResolveAsync_NothingMatchesTheContactIdOrTheEmail_IsNotFoundAndLinksNothing()
    {
        _users.Add(ExternalUserType.Viewer, email: "someone.else@example.com");

        var result = await ResolveAsync(Guid.NewGuid());

        Assert.Equal(ExternalUserResolutionStatus.NotFound, result.Status);
        Assert.Equal(DisplayName, result.DisplayName);
        Assert.Equal(1, _users.FindByEmailCalls);
        Assert.Equal(0, _users.LinkCalls);
        Assert.Null(_users.Rows.Single().SsoIdExt);
    }

    [Fact]
    public async Task ResolveAsync_EmailBelongsToARecordLinkedToAnotherIdentity_IsDeniedAndNeverRelinked()
    {
        var originalOwner = Guid.NewGuid();
        _users.Add(ExternalUserType.Viewer, ssoIdExt: originalOwner);

        var result = await ResolveAsync(Guid.NewGuid());

        Assert.Equal(ExternalUserResolutionStatus.LinkedToAnotherIdentity, result.Status);
        Assert.Equal(0, _users.LinkCalls);
        Assert.Equal(originalOwner, _users.Rows.Single().SsoIdExt);
        Assert.Contains(_logger.Entries, entry => entry.EventId.Id == 122);
    }

    [Fact]
    public async Task ResolveAsync_ParticipantEmailWithOneLabAlreadyLinkedElsewhere_IsDeniedAndLinksNone()
    {
        var customerId = Guid.NewGuid();
        var first = await AddLabAsync(customerId, "LAB001");
        var second = await AddLabAsync(customerId, "LAB002");
        _users.Add(ExternalUserType.Participant, userId: first, labParticipantId: first, labCode: "LAB001", labName: "A");
        _users.Add(ExternalUserType.Participant, ssoIdExt: Guid.NewGuid(), userId: second, labParticipantId: second, labCode: "LAB002", labName: "B");

        var result = await ResolveAsync(Guid.NewGuid());

        Assert.Equal(ExternalUserResolutionStatus.LinkedToAnotherIdentity, result.Status);
        Assert.Equal(0, _users.LinkCalls);
    }

    [Theory]
    [InlineData(ExternalUserType.Viewer)]
    [InlineData(ExternalUserType.TestConsultant)]
    [InlineData(ExternalUserType.LabUser)]
    public async Task ResolveAsync_EmailMatchesSeveralPeopleOfOneType_IsAmbiguousAndLinksNothing(ExternalUserType userType)
    {
        _users.Add(userType, labParticipantId: Guid.NewGuid(), labCode: "LAB001", labName: "A");
        _users.Add(userType, labParticipantId: Guid.NewGuid(), labCode: "LAB002", labName: "B");

        var result = await ResolveAsync(Guid.NewGuid());

        Assert.Equal(ExternalUserResolutionStatus.Ambiguous, result.Status);
        Assert.Equal(0, _users.LinkCalls);
        Assert.All(_users.Rows, row => Assert.Null(row.SsoIdExt));
        Assert.Contains(_logger.Entries, entry => entry.EventId.Id == 123);
    }

    [Fact]
    public async Task ResolveAsync_RecordIsClaimedBetweenTheCheckAndTheLink_IsNotFound()
    {
        _users.Add(ExternalUserType.Viewer);
        _users.LinkFindsNothing = true;

        var result = await ResolveAsync(Guid.NewGuid());

        Assert.Equal(ExternalUserResolutionStatus.NotFound, result.Status);
        Assert.Equal(1, _users.LinkCalls);
    }

    // ---- the labs a signed-in user can choose from ----

    [Fact]
    public async Task GetLabsAsync_ParticipantWithSeveralLabs_ListsTheActiveOnesOnly()
    {
        var ssoIdExt = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var active = await AddLabAsync(customerId, "LAB001");
        var inactive = await AddLabAsync(customerId, "LAB002", isActive: false);
        var another = await AddLabAsync(customerId, "LAB003");
        AddParticipantRow(ssoIdExt, active, "LAB001");
        AddParticipantRow(ssoIdExt, inactive, "LAB002", isActive: false);
        AddParticipantRow(ssoIdExt, another, "LAB003");

        var labs = await CreateService().GetLabsAsync(ssoIdExt);

        Assert.Equal([active, another], labs.Select(lab => lab.ParticipantId));
        Assert.Equal(["LAB001", "LAB003"], labs.Select(lab => lab.LabCode));
    }

    [Fact]
    public async Task GetLabsAsync_LabUser_ListsTheirActiveLabs()
    {
        var ssoIdExt = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        _users.Add(ExternalUserType.LabUser, ssoIdExt: ssoIdExt, userId: userId, labParticipantId: first, labCode: "LAB001", labName: "A");
        _users.Add(ExternalUserType.LabUser, ssoIdExt: ssoIdExt, userId: userId, labParticipantId: second, labCode: "LAB002", labName: "B");
        _users.Add(ExternalUserType.LabUser, ssoIdExt: ssoIdExt, userId: userId, isActive: false, labParticipantId: Guid.NewGuid(), labCode: "LAB003", labName: "C");

        var labs = await CreateService().GetLabsAsync(ssoIdExt);

        Assert.Equal([first, second], labs.Select(lab => lab.ParticipantId));
    }

    [Fact]
    public async Task GetLabsAsync_ViewerUnknownContactOrNoLabs_IsEmpty()
    {
        var viewerContact = Guid.NewGuid();
        _users.Add(ExternalUserType.Viewer, ssoIdExt: viewerContact);

        Assert.Empty(await CreateService().GetLabsAsync(viewerContact));
        Assert.Empty(await CreateService().GetLabsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetLabsAsync_ContactLinkedInTwoTables_OffersOnlyTheLabsOfTheFirstTableThatMatched()
    {
        var ssoIdExt = Guid.NewGuid();
        var participantLab = await AddLabAsync(Guid.NewGuid(), "LAB001");
        AddParticipantRow(ssoIdExt, participantLab, "LAB001");
        _users.Add(ExternalUserType.LabUser, ssoIdExt: ssoIdExt, labParticipantId: Guid.NewGuid(), labCode: "LAB009", labName: "Other");

        var labs = await CreateService().GetLabsAsync(ssoIdExt);

        Assert.Equal(participantLab, Assert.Single(labs).ParticipantId);
    }

    [Fact]
    public async Task GetLabAsync_OneOfTheUsersActiveLabs_ReturnsItWithOnlineOrdering()
    {
        var ssoIdExt = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        await _customers.CreateAsync(new CoreCustomer { CustomerId = customerId, CanOrderOnline = true });
        var labId = await AddLabAsync(customerId, "LAB001");
        AddParticipantRow(ssoIdExt, labId, "LAB001");
        AddParticipantRow(ssoIdExt, Guid.NewGuid(), "LAB002");

        var lab = await CreateService().GetLabAsync(ssoIdExt, labId);

        Assert.Equal(new ExternalLabContext(labId, "LAB001", "LAB001 Lab", CanOrderOnline: true), lab);
    }

    [Fact]
    public async Task GetLabAsync_ACustomerWithTwoActiveLabs_CannotOrderOnline()
    {
        var ssoIdExt = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        await _customers.CreateAsync(new CoreCustomer { CustomerId = customerId, CanOrderOnline = true });
        var labId = await AddLabAsync(customerId, "LAB001");
        await AddLabAsync(customerId, "LAB002");
        AddParticipantRow(ssoIdExt, labId, "LAB001");

        Assert.False((await CreateService().GetLabAsync(ssoIdExt, labId))!.CanOrderOnline);
    }

    [Fact]
    public async Task GetLabAsync_LabOfSomeoneElseAnInactiveLabOrAnUnknownOne_IsNull()
    {
        var ssoIdExt = Guid.NewGuid();
        var mine = await AddLabAsync(Guid.NewGuid(), "LAB001");
        var inactive = await AddLabAsync(Guid.NewGuid(), "LAB002", isActive: false);
        var someoneElses = await AddLabAsync(Guid.NewGuid(), "LAB003");
        AddParticipantRow(ssoIdExt, mine, "LAB001");
        AddParticipantRow(ssoIdExt, inactive, "LAB002", isActive: false);
        AddParticipantRow(Guid.NewGuid(), someoneElses, "LAB003");
        var service = CreateService();

        Assert.Null(await service.GetLabAsync(ssoIdExt, someoneElses));
        Assert.Null(await service.GetLabAsync(ssoIdExt, inactive));
        Assert.Null(await service.GetLabAsync(ssoIdExt, Guid.NewGuid()));
        Assert.Null(await service.GetLabAsync(Guid.NewGuid(), mine));
    }

    // ---- email drift after linking ----

    [Fact]
    public async Task ResolveAsync_ReturningUserWhoseEmailChangedInCidm_StillSignsInByContactIdAndWarnsWithoutLoggingEmails()
    {
        var ssoIdExt = Guid.NewGuid();
        var viewer = _users.Add(ExternalUserType.Viewer, email: "old.address@example.com", ssoIdExt: ssoIdExt);

        var result = await ResolveAsync(ssoIdExt, "new.address@example.com");

        Assert.True(result.IsResolved);
        Assert.Equal(viewer.UserId, result.ViewerId);
        var warning = Assert.Single(_logger.At(LogLevel.Warning));
        Assert.Equal(124, warning.EventId.Id);
        Assert.DoesNotContain("address@example.com", warning.Message);
        Assert.Equal(0, _users.LinkCalls);
        Assert.Equal("old.address@example.com", _users.Rows.Single().Email);
    }

    [Fact]
    public async Task ResolveAsync_ReturningUserWhoseEmailStillMatches_DoesNotWarn()
    {
        var ssoIdExt = Guid.NewGuid();
        _users.Add(ExternalUserType.Viewer, email: "Jane.Doe@example.com", ssoIdExt: ssoIdExt);

        await ResolveAsync(ssoIdExt, "jane.doe@EXAMPLE.com");

        Assert.Empty(_logger.At(LogLevel.Warning));
    }

    [Fact]
    public async Task ResolveAsync_ReturningParticipantWithOnlyOneLabEmailChanged_DoesNotWarnWhileAnotherLabStillMatches()
    {
        var ssoIdExt = Guid.NewGuid();
        _users.Add(ExternalUserType.Participant, email: "old@example.com", ssoIdExt: ssoIdExt, labParticipantId: Guid.NewGuid(), labCode: "LAB001", labName: "A");
        _users.Add(ExternalUserType.Participant, email: Email, ssoIdExt: ssoIdExt, labParticipantId: Guid.NewGuid(), labCode: "LAB002", labName: "B");

        await ResolveAsync(ssoIdExt);

        Assert.Empty(_logger.At(LogLevel.Warning));
    }

    [Fact]
    public async Task ResolveAsync_ReturningUserWithNoEmailInTheToken_DoesNotWarn()
    {
        var ssoIdExt = Guid.NewGuid();
        _users.Add(ExternalUserType.Viewer, ssoIdExt: ssoIdExt);

        await ResolveAsync(ssoIdExt, string.Empty);

        Assert.Empty(_logger.At(LogLevel.Warning));
    }

    [Fact]
    public async Task ResolveAsync_SuccessfulFirstLink_LogsTheLinkWithoutEmails()
    {
        _users.Add(ExternalUserType.Viewer);

        await ResolveAsync(Guid.NewGuid());

        var entry = Assert.Single(_logger.Entries, e => e.EventId.Id == 121);
        Assert.DoesNotContain(Email, entry.Message);
    }
}
