-- Lab user management by a participant (primary contact): list the people who can sign in to a lab, add one, make
-- one active or inactive, change their name or email, and record that their login instructions were sent.
--
-- A lab user is one person (tblLabUser, identified by email) who can be linked to several labs; the link
-- (tlnkLabUserParticipant) carries the per-lab state: Active, and whether the login instructions for that lab
-- have been sent. Making a person inactive for a lab only changes the link - nothing is ever deleted.
--
-- Safe to re-run: each step checks whether it has already been applied.

-- A procedure keeps the QUOTED_IDENTIFIER setting it was created with, and updating a table that has a
-- filtered index (fldSsoIdExt) fails when it is OFF - which is sqlcmd's default unless run with -I.
SET QUOTED_IDENTIFIER ON
SET ANSI_NULLS ON

GO

------------------------------------------------------------------------------------------------------------------------
-- 1. Login instructions sent, per lab link. Links that already exist are SENT (1) - those people were told about their
--    access before this feature - and the default for new links is NOT SENT (0), as for the other user tables (0011).
------------------------------------------------------------------------------------------------------------------------
IF COL_LENGTH('dbo.tlnkLabUserParticipant', 'fldLoginInstructionsSent') IS NULL
BEGIN
	ALTER TABLE [dbo].[tlnkLabUserParticipant] ADD
		[fldLoginInstructionsSent] BIT NOT NULL CONSTRAINT [DF_tlnkLabUserParticipant_fldLoginInstructionsSent] DEFAULT (1),
		[fldLoginInstructionsSentDate] DATETIME2 NULL
END

GO

IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_tlnkLabUserParticipant_fldLoginInstructionsSent' AND definition = '((1))')
BEGIN
	ALTER TABLE [dbo].[tlnkLabUserParticipant] DROP CONSTRAINT [DF_tlnkLabUserParticipant_fldLoginInstructionsSent]
	ALTER TABLE [dbo].[tlnkLabUserParticipant] ADD CONSTRAINT [DF_tlnkLabUserParticipant_fldLoginInstructionsSent] DEFAULT (0) FOR [fldLoginInstructionsSent]
END

GO

------------------------------------------------------------------------------------------------------------------------
-- 2. One link per person per lab, and one lab user per email address (the unit a participant adds and CIDM links).
--    The email index is only created when the existing data has no duplicates, so the script cannot fail on old data;
--    the procedures below enforce uniqueness either way.
------------------------------------------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_tlnkLabUserParticipant_LabUser_Participant' AND object_id = OBJECT_ID('dbo.tlnkLabUserParticipant'))
	AND NOT EXISTS (SELECT 1 FROM tlnkLabUserParticipant GROUP BY fldLabUserId, fldParticipantId HAVING COUNT(*) > 1)
BEGIN
	CREATE UNIQUE NONCLUSTERED INDEX [UX_tlnkLabUserParticipant_LabUser_Participant]
	ON [dbo].[tlnkLabUserParticipant] ([fldLabUserId], [fldParticipantId])
END

GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_tlnkLabUserParticipant_fldParticipantId' AND object_id = OBJECT_ID('dbo.tlnkLabUserParticipant'))
BEGIN
	CREATE NONCLUSTERED INDEX [IX_tlnkLabUserParticipant_fldParticipantId]
	ON [dbo].[tlnkLabUserParticipant] ([fldParticipantId])
	INCLUDE ([fldLabUserId], [fldIsActive], [fldLoginInstructionsSent])
END

GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_tblLabUser_fldEmail' AND object_id = OBJECT_ID('dbo.tblLabUser'))
	AND NOT EXISTS (SELECT 1 FROM tblLabUser GROUP BY fldEmail HAVING COUNT(*) > 1)
BEGIN
	CREATE UNIQUE NONCLUSTERED INDEX [UX_tblLabUser_fldEmail]
	ON [dbo].[tblLabUser] ([fldEmail])
END

GO

------------------------------------------------------------------------------------------------------------------------
-- 3. Read the lab users of a lab. Filter by lab user (the edit page) or by link (login instructions); the lab is always
--    given by the callers, so one participant can never reach another lab's users.
--    OtherLabCount: how many OTHER labs the person is linked to - when above zero the person is shared, so their name and
--    email cannot be changed from this lab. HasSignedIn: the person has already linked a CIDM account.
------------------------------------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[spgLabUsersByParticipant]
	@ParticipantId uniqueidentifier,
	@LabUserId uniqueidentifier = NULL,
	@LabUserParticipantId uniqueidentifier = NULL
AS
BEGIN
	SET NOCOUNT ON;

	SELECT
		m.fldLabUserParticipantId AS LabUserParticipantId,
		u.fldLabUserId AS LabUserId,
		m.fldParticipantId AS ParticipantId,
		u.fldName AS Name,
		u.fldEmail AS Email,
		m.fldIsActive AS IsActive,
		m.fldLoginInstructionsSent AS LoginInstructionsSent,
		(
			SELECT COUNT(*)
			FROM tlnkLabUserParticipant o
			WHERE o.fldLabUserId = u.fldLabUserId AND o.fldParticipantId <> m.fldParticipantId
		) AS OtherLabCount,
		CAST(CASE WHEN u.fldSsoIdExt IS NULL THEN 0 ELSE 1 END AS bit) AS HasSignedIn,
		p.fldLabCode AS LabCode,
		p.fldLabName AS LabName
	FROM
		tlnkLabUserParticipant m
		INNER JOIN tblLabUser u ON u.fldLabUserId = m.fldLabUserId
		INNER JOIN tblParticipant p ON p.fldParticipantId = m.fldParticipantId
	WHERE
		m.fldParticipantId = @ParticipantId
		AND (@LabUserId IS NULL OR m.fldLabUserId = @LabUserId)
		AND (@LabUserParticipantId IS NULL OR m.fldLabUserParticipantId = @LabUserParticipantId)
	ORDER BY
		u.fldName,
		u.fldEmail
END

GO

-- The same row for a link id alone, for the login instructions email (which is built from the link, not from a lab).
CREATE OR ALTER PROCEDURE [dbo].[spgLabUserLinkById]
	@LabUserParticipantId uniqueidentifier
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @ParticipantId uniqueidentifier
	SELECT @ParticipantId = fldParticipantId FROM tlnkLabUserParticipant WHERE fldLabUserParticipantId = @LabUserParticipantId

	IF @ParticipantId IS NOT NULL
		EXEC dbo.spgLabUsersByParticipant @ParticipantId = @ParticipantId, @LabUserParticipantId = @LabUserParticipantId
END

GO

------------------------------------------------------------------------------------------------------------------------
-- 4. Add a lab user to a lab. If the email already belongs to a lab user (another lab's), only the link is added.
--    Outcome: 1 new person + link, 2 existing person + link, 3 already linked to this lab (nothing changed),
--             4 the email belongs to a Participant, Viewer or Test Consultant (a person is one kind of user only).
------------------------------------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[spiLabUserForParticipant]
	@ParticipantId uniqueidentifier,
	@Name nvarchar(255),
	@Email nvarchar(255),
	@IsActive bit
AS
BEGIN
	SET NOCOUNT ON;
	SET XACT_ABORT ON;

	DECLARE @LabUserId uniqueidentifier, @Outcome int

	-- vwExternalUser.Email is varchar, so compare as varchar to keep the lookups on the indexed columns.
	-- No transaction yet, so the refusal needs no rollback (the caller may already hold one of its own).
	IF EXISTS (SELECT 1 FROM vwExternalUser WHERE UserType <> 4 AND Email = CAST(@Email AS varchar(255)))
	BEGIN
		SELECT 4 AS Outcome, CAST(NULL AS uniqueidentifier) AS LabUserId
		RETURN
	END

	BEGIN TRANSACTION

	SELECT @LabUserId = fldLabUserId FROM tblLabUser WITH (UPDLOCK, HOLDLOCK) WHERE fldEmail = @Email

	IF @LabUserId IS NULL
	BEGIN
		SET @LabUserId = NEWID()
		-- fldSsoId is the legacy SSO identifier; new people never have one.
		INSERT INTO tblLabUser (fldLabUserId, fldName, fldEmail, fldSsoId) VALUES (@LabUserId, @Name, @Email, N'')
		SET @Outcome = 1
	END
	ELSE
		SET @Outcome = 2

	IF EXISTS (SELECT 1 FROM tlnkLabUserParticipant WHERE fldLabUserId = @LabUserId AND fldParticipantId = @ParticipantId)
		SET @Outcome = 3
	ELSE
		INSERT INTO tlnkLabUserParticipant (fldLabUserId, fldParticipantId, fldIsActive) VALUES (@LabUserId, @ParticipantId, @IsActive)

	COMMIT TRANSACTION

	SELECT @Outcome AS Outcome, @LabUserId AS LabUserId
END

GO

------------------------------------------------------------------------------------------------------------------------
-- 5. Change a person's access to one lab (@IsActive) and, when @ChangeDetails = 1, their name and email.
--    Only a person linked to no other lab can have name/email changed from here. A changed email clears the linked CIDM
--    account and the instructions-sent flag: the new address belongs to whoever the participant means to give access, so
--    the previous CIDM account must not keep it, and the new person needs the login instructions.
--    Outcome: 0 not a lab user of this lab, 1 updated, 2 email already used by another lab user,
--             3 shared with other labs (name/email not changed, nothing saved), 4 email belongs to another kind of user,
--             5 updated and the email changed.
------------------------------------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[spuLabUserForParticipant]
	@ParticipantId uniqueidentifier,
	@LabUserId uniqueidentifier,
	@Name nvarchar(255),
	@Email nvarchar(255),
	@IsActive bit,
	@ChangeDetails bit
AS
BEGIN
	SET NOCOUNT ON;
	SET XACT_ABORT ON;

	DECLARE @LinkId uniqueidentifier, @CurrentEmail nvarchar(255), @Outcome int = 1, @EmailChanged bit = 0

	SELECT @LinkId = fldLabUserParticipantId
	FROM tlnkLabUserParticipant
	WHERE fldLabUserId = @LabUserId AND fldParticipantId = @ParticipantId

	IF @LinkId IS NULL
	BEGIN
		SELECT 0 AS Outcome
		RETURN
	END

	-- Every check comes before any change, so a refusal leaves nothing to roll back (the caller may already hold a
	-- transaction of its own, which a ROLLBACK here would also end).
	IF @ChangeDetails = 1
	BEGIN
		IF EXISTS (SELECT 1 FROM tlnkLabUserParticipant WHERE fldLabUserId = @LabUserId AND fldParticipantId <> @ParticipantId)
		BEGIN
			SELECT 3 AS Outcome
			RETURN
		END

		SELECT @CurrentEmail = fldEmail FROM tblLabUser WHERE fldLabUserId = @LabUserId

		IF @Email <> @CurrentEmail
		BEGIN
			IF EXISTS (SELECT 1 FROM tblLabUser WHERE fldEmail = @Email AND fldLabUserId <> @LabUserId)
			BEGIN
				SELECT 2 AS Outcome
				RETURN
			END

			IF EXISTS (SELECT 1 FROM vwExternalUser WHERE UserType <> 4 AND Email = CAST(@Email AS varchar(255)))
			BEGIN
				SELECT 4 AS Outcome
				RETURN
			END

			SET @EmailChanged = 1
			SET @Outcome = 5
		END
	END

	BEGIN TRANSACTION

	IF @ChangeDetails = 1
	BEGIN
		IF @EmailChanged = 1
		BEGIN
			UPDATE tblLabUser SET fldName = @Name, fldEmail = @Email, fldSsoIdExt = NULL WHERE fldLabUserId = @LabUserId
			UPDATE tlnkLabUserParticipant SET fldLoginInstructionsSent = 0, fldLoginInstructionsSentDate = NULL WHERE fldLabUserParticipantId = @LinkId
		END
		ELSE
			UPDATE tblLabUser SET fldName = @Name WHERE fldLabUserId = @LabUserId
	END

	UPDATE tlnkLabUserParticipant SET fldIsActive = @IsActive WHERE fldLabUserParticipantId = @LinkId

	COMMIT TRANSACTION

	SELECT @Outcome AS Outcome
END

GO

------------------------------------------------------------------------------------------------------------------------
-- 6. Record that the login instructions email for a lab link has been sent.
------------------------------------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[spuLabUserLoginInstructions]
	@LabUserParticipantId uniqueidentifier,
	@LoginInstructionsSent bit,
	@LoginInstructionsSentDate datetime2 = NULL
AS
BEGIN
	SET NOCOUNT ON;

	UPDATE tlnkLabUserParticipant
	SET
		fldLoginInstructionsSent = @LoginInstructionsSent,
		fldLoginInstructionsSentDate = @LoginInstructionsSentDate
	WHERE
		fldLabUserParticipantId = @LabUserParticipantId
END

GO
GRANT EXECUTE ON [dbo].[spgLabUsersByParticipant] TO [ProficiencyTestingInternalUser]
GO
GRANT EXECUTE ON [dbo].[spgLabUserLinkById] TO [ProficiencyTestingInternalUser]
GO
GRANT EXECUTE ON [dbo].[spiLabUserForParticipant] TO [ProficiencyTestingInternalUser]
GO
GRANT EXECUTE ON [dbo].[spuLabUserForParticipant] TO [ProficiencyTestingInternalUser]
GO
GRANT EXECUTE ON [dbo].[spuLabUserLoginInstructions] TO [ProficiencyTestingInternalUser]
GO
