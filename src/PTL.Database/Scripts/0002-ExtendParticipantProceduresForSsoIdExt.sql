-- Extends the existing legacy Participant procedures (already reused as-is by PTL.Data's
-- ParticipantRepository) to also recognise the new CIDM fldSsoIdExt identity, instead of
-- maintaining a parallel set of CIDM-only procedures. @SsoIdExt is appended as the LAST
-- parameter with no change to the original required parameters, so the existing
-- named-parameter EXEC calls (@ParticipantId, @SsoId, ...) keep working unchanged. All other
-- logic (including spuParticipant's OldLabCode history tracking) is preserved verbatim.
CREATE OR ALTER PROCEDURE [dbo].[spgParticipantBySsoId]
	@SsoId uniqueidentifier = NULL,
	@SsoIdExt uniqueidentifier = NULL
AS
BEGIN
	SET NOCOUNT ON;

	IF @SsoIdExt IS NOT NULL
	BEGIN
		SELECT
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
			fldIsActive
		FROM
			tblParticipant
		WHERE
			fldSsoIdExt = @SsoIdExt
	END
	ELSE
	BEGIN
		SELECT
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
			fldIsActive
		FROM
			tblParticipant
		WHERE
			fldSsoId = @SsoId
	END
END

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

GO

CREATE OR ALTER PROCEDURE [dbo].[spuParticipant]
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

	--If the Lab Code has changed, store the old one
	DECLARE @OldLabCode varchar(8)
	SELECT @OldLabCode = fldLabCode
	FROM tblParticipant
	WHERE fldParticipantId = @ParticipantId
	IF (@LabCode <> @OldLabCode)
	INSERT INTO
		tblOldLabCode
	(
		fldOldLabCodeId,
		fldParticipantId,
		fldLabCode,
		fldDate
	)
	VALUES
	(
		newid(),
		@ParticipantId,
		@OldLabCode,
		getdate()
	)

	UPDATE
		tblParticipant
	SET
		fldCustomerId = @CustomerId,
		fldSsoId = @SsoId,
		fldSsoIdExt = @SsoIdExt,
		fldLabCode = @LabCode,
		fldLabName = @LabName,
		fldLabTypeId = @LabTypeId,
		fldContactName = @ContactName,
		fldOrganisation = @Organisation,
		fldAddress1 = @Address1,
		fldAddress2 = @Address2,
		fldAddress3 = @Address3,
		fldAddress4 = @Address4,
		fldAddress5 = @Address5,
		fldCountryId = @CountryId,
		fldTelephone = @Telephone,
		fldFax = @Fax,
		fldEmail = @Email,
		fldEmail2 = @Email2,
		fldComments = @Comments,
		fldIsActive = @IsActive,
		fldInactiveDate = @InactiveDate,
		fldInactiveError = @InactiveError,
		fldInactiveErrorDate = @InactiveErrorDate,
		fldDataCleanedDate = NULL
	WHERE
		fldParticipantId = @ParticipantId
END
