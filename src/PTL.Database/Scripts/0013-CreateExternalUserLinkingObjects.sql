-- A procedure keeps the QUOTED_IDENTIFIER setting it was created with, and updating a table that has a
-- filtered index (fldSsoIdExt) fails when it is OFF - which is sqlcmd's default unless run with -I.
SET QUOTED_IDENTIFIER ON
SET ANSI_NULLS ON

GO

-- Single source for "which external user is this?" across the four user tables. UserType doubles as the
-- match priority (lowest wins): 1 Participant, 2 Viewer, 3 Test Consultant, 4 Lab User. Must match
-- PTL.Contracts.ExternalUser.ExternalUserType.
-- A Participant or Lab User yields one row per lab (a Lab User with no mapping at all yields one row with
-- no lab); Viewer and Test Consultant yield one row. IsActive is the state of that row's access.
CREATE OR ALTER VIEW [dbo].[vwExternalUser]
AS
	SELECT
		CAST(1 AS int) AS UserType,
		p.fldParticipantId AS UserId,
		CAST(p.fldEmail AS varchar(255)) AS Email,
		p.fldSsoIdExt AS SsoIdExt,
		CAST(p.fldIsActive AS bit) AS IsActive,
		p.fldParticipantId AS LabParticipantId,
		CAST(p.fldLabCode AS varchar(50)) AS LabCode,
		CAST(p.fldLabName AS varchar(100)) AS LabName
	FROM
		tblParticipant p

	UNION ALL

	SELECT
		2,
		v.fldViewerId,
		CAST(v.fldEmail AS varchar(255)),
		v.fldSsoIdExt,
		CAST(1 AS bit),
		CAST(NULL AS uniqueidentifier),
		CAST(NULL AS varchar(50)),
		CAST(NULL AS varchar(100))
	FROM
		tblViewer v

	UNION ALL

	SELECT
		3,
		t.fldExternalTestConsultantId,
		CAST(t.fldEmail AS varchar(255)),
		t.fldSsoIdExt,
		CAST(CASE WHEN t.fldIsInactive = 1 THEN 0 ELSE 1 END AS bit),
		CAST(NULL AS uniqueidentifier),
		CAST(NULL AS varchar(50)),
		CAST(NULL AS varchar(100))
	FROM
		tblExternalTestConsultant t

	UNION ALL

	SELECT
		4,
		u.fldLabUserId,
		CAST(u.fldEmail AS varchar(255)),
		u.fldSsoIdExt,
		CAST(CASE WHEN m.fldIsActive = 1 AND p.fldIsActive = 1 THEN 1 ELSE 0 END AS bit),
		p.fldParticipantId,
		CAST(p.fldLabCode AS varchar(50)),
		CAST(p.fldLabName AS varchar(100))
	FROM
		tblLabUser u
		LEFT JOIN tlnkLabUserParticipant m ON m.fldLabUserId = u.fldLabUserId
		LEFT JOIN tblParticipant p ON p.fldParticipantId = m.fldParticipantId

GO

-- Returning user: everything linked to a CIDM contact id, highest-priority user type first.
CREATE OR ALTER PROCEDURE [dbo].[spgExternalUserBySsoIdExt]
	@SsoIdExt uniqueidentifier
AS
BEGIN
	SET NOCOUNT ON;

	SELECT
		UserType,
		UserId,
		Email,
		SsoIdExt,
		IsActive,
		LabParticipantId,
		LabCode,
		LabName
	FROM
		vwExternalUser
	WHERE
		SsoIdExt = @SsoIdExt
	ORDER BY
		UserType,
		LabCode
END

GO

-- First sign-in: every user matching the email, linked to a CIDM contact id or not, so the caller can
-- tell a free record from one already claimed by a different identity.
CREATE OR ALTER PROCEDURE [dbo].[spgExternalUserByEmail]
	@Email varchar(255)
AS
BEGIN
	SET NOCOUNT ON;

	SELECT
		UserType,
		UserId,
		Email,
		SsoIdExt,
		IsActive,
		LabParticipantId,
		LabCode,
		LabName
	FROM
		vwExternalUser
	WHERE
		Email = @Email
	ORDER BY
		UserType,
		LabCode
END

GO

-- Links a CIDM contact id to the not-yet-linked records of one user type that carry the email, then
-- returns what is now linked to that contact id (same result set as spgExternalUserBySsoIdExt).
-- fldSsoIdExt IS NULL is part of the UPDATE itself, so a record already claimed by any identity - or
-- claimed by a concurrent sign-in between the caller's check and this statement - is never overwritten.
-- A Participant's email can be on several lab rows, so all of them are linked together.
CREATE OR ALTER PROCEDURE [dbo].[spuExternalUserSsoIdExt]
	@UserType int,
	@Email varchar(255),
	@SsoIdExt uniqueidentifier
AS
BEGIN
	SET NOCOUNT ON;

	IF @UserType = 1
		UPDATE tblParticipant SET fldSsoIdExt = @SsoIdExt WHERE fldEmail = @Email AND fldSsoIdExt IS NULL;
	ELSE IF @UserType = 2
		UPDATE tblViewer SET fldSsoIdExt = @SsoIdExt WHERE fldEmail = @Email AND fldSsoIdExt IS NULL;
	ELSE IF @UserType = 3
		UPDATE tblExternalTestConsultant SET fldSsoIdExt = @SsoIdExt WHERE fldEmail = @Email AND fldSsoIdExt IS NULL;
	ELSE IF @UserType = 4
		UPDATE tblLabUser SET fldSsoIdExt = @SsoIdExt WHERE fldEmail = @Email AND fldSsoIdExt IS NULL;
	ELSE
		THROW 50000, 'Unknown external user type.', 1;

	EXEC dbo.spgExternalUserBySsoIdExt @SsoIdExt;
END

GO
GRANT EXECUTE ON [dbo].[spgExternalUserBySsoIdExt] TO [ProficiencyTestingInternalUser]
GO
GRANT EXECUTE ON [dbo].[spgExternalUserByEmail] TO [ProficiencyTestingInternalUser]
GO
GRANT EXECUTE ON [dbo].[spuExternalUserSsoIdExt] TO [ProficiencyTestingInternalUser]
GO
