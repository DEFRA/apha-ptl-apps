-- A participant (lab) created with the email of a person who has already signed in through CIDM inherits that
-- person's CIDM contact id, so the new lab shows up for them without a second sign-in link. Email is only the
-- bootstrap for a new row - it is never consulted again once the row carries the contact id.
--
-- The contact id is copied only when the caller supplied none and the email is already linked to exactly
-- one contact id: if the same email is linked to several different contact ids it is unclear whose lab this
-- is, so the row is left unlinked and the first-sign-in match handles it. Everything else is unchanged from
-- 0002's spiParticipant.

-- A procedure keeps the QUOTED_IDENTIFIER setting it was created with, and inserting into a table with a
-- filtered index (fldSsoIdExt) fails when it is OFF - which is sqlcmd's default unless run with -I.
SET QUOTED_IDENTIFIER ON
SET ANSI_NULLS ON

GO

CREATE OR ALTER PROCEDURE [dbo].[spiParticipant]
	@ParticipantId uniqueidentifier,
	@SsoId uniqueidentifier,
	@CustomerId uniqueidentifier,
	@LabCode varchar(8),
	@LabName varchar(50),
	@LabTypeId uniqueidentifier,
	@ContactName varchar(50),
	@Organisation varchar(50),
	@Address1 varchar(100),
	@Address2 varchar(100),
	@Address3 varchar(100),
	@Address4 varchar(100),
	@Address5 varchar(100),
	@CountryId uniqueidentifier,
	@Telephone varchar(20),
	@Fax varchar(20),
	@Email varchar(150),
	@Email2 varchar(150),
	@Comments varchar(2000),
	@IsActive bit,
	@InactiveDate datetime2,
	@InactiveError BIT,
	@InactiveErrorDate datetime2,
	@SsoIdExt uniqueidentifier = NULL
AS
BEGIN
	SET NOCOUNT ON;

	IF @SsoIdExt IS NULL AND @Email IS NOT NULL AND @Email <> ''
		AND (SELECT COUNT(DISTINCT fldSsoIdExt) FROM tblParticipant WHERE fldEmail = @Email AND fldSsoIdExt IS NOT NULL) = 1
	BEGIN
		SELECT TOP (1) @SsoIdExt = fldSsoIdExt
		FROM tblParticipant
		WHERE fldEmail = @Email AND fldSsoIdExt IS NOT NULL
	END

	INSERT INTO tblParticipant
	(
		fldParticipantId,
		fldSsoId,
		fldSsoIdExt,
		fldCustomerId,
		fldLabCode,
		fldLabName,
		fldLabTypeId,
		fldContactName,
		fldOrganisation,
		fldAddress1,
		fldAddress2,
		fldAddress3,
		fldAddress4,
		fldAddress5,
		fldCountryId,
		fldTelephone,
		fldFax,
		fldEmail,
		fldEmail2,
		fldComments,
		fldIsActive,
		fldInactiveDate,
		fldInactiveError,
		fldInactiveErrorDate
	)
	VALUES
	(
		@ParticipantId,
		@SsoId,
		@SsoIdExt,
		@CustomerId,
		@LabCode,
		@LabName,
		@LabTypeId,
		@ContactName,
		@Organisation,
		@Address1,
		@Address2,
		@Address3,
		@Address4,
		@Address5,
		@CountryId,
		@Telephone,
		@Fax,
		@Email,
		@Email2,
		@Comments,
		@IsActive,
		@InactiveDate,
		@InactiveError,
		@InactiveErrorDate
	)
END
