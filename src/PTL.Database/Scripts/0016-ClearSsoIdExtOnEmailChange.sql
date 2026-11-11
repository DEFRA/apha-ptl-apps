-- A changed email address ends the CIDM link of a Participant, Viewer or External Test Consultant.
--
-- Scripts 0002 / 0004 / 0006 / 0011 let the caller decide fldSsoIdExt, and the internal application writes back the value it
-- read, so an admin who corrected an email address left the old CIDM account linked: the person could still sign in with it.
-- Now, when the saved email address changes, fldSsoIdExt is cleared in the same statement that clears the "login instructions
-- sent" flag (0011). The admin sends the instructions to the new address, and the next sign-in links the new CIDM account by
-- email (see spgExternalUserByEmail / spuExternalUserSsoIdExt, 0013). Lab users get the same behaviour from
-- spuLabUserForParticipant (0015).
--
-- In an UPDATE the right-hand side reads the row as it was before the update, so fldEmail below is the previously saved
-- address. The comparison uses the column's collation, so a change of letter case alone changes nothing. Every other column is
-- written exactly as in 0011. fldSsoId (the legacy identifier) is never touched here.
--
-- Safe to re-run.

-- A procedure keeps the QUOTED_IDENTIFIER setting it was created with, and updating a table that has a
-- filtered index (fldSsoIdExt) fails when it is OFF - which is sqlcmd's default unless run with -I.
SET QUOTED_IDENTIFIER ON
SET ANSI_NULLS ON

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
                fldSsoIdExt = CASE WHEN ISNULL(fldEmail, '') <> ISNULL(@Email, '') THEN NULL ELSE @SsoIdExt END,
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
                fldLoginInstructionsSent = CASE WHEN ISNULL(fldEmail, '') <> ISNULL(@Email, '') THEN 0 ELSE fldLoginInstructionsSent END,
                fldLoginInstructionsSentDate = CASE WHEN ISNULL(fldEmail, '') <> ISNULL(@Email, '') THEN NULL ELSE fldLoginInstructionsSentDate END,
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

GO

CREATE OR ALTER PROCEDURE [dbo].[spuViewer]
        @ViewerId uniqueidentifier,
        @Name varchar(50),
        @Email varchar(50),
        @SsoId uniqueidentifier,
        @SsoIdExt uniqueidentifier = NULL
AS
BEGIN
        SET NOCOUNT ON;

        UPDATE
                tblViewer
        SET
                fldName = @Name,
                fldLoginInstructionsSent = CASE WHEN ISNULL(fldEmail, '') <> ISNULL(@Email, '') THEN 0 ELSE fldLoginInstructionsSent END,
                fldLoginInstructionsSentDate = CASE WHEN ISNULL(fldEmail, '') <> ISNULL(@Email, '') THEN NULL ELSE fldLoginInstructionsSentDate END,
                fldEmail = @Email,
                fldSsoId = @SsoId,
                fldSsoIdExt = CASE WHEN ISNULL(fldEmail, '') <> ISNULL(@Email, '') THEN NULL ELSE @SsoIdExt END
        WHERE
                fldViewerId = @ViewerId
END

GO

CREATE OR ALTER PROCEDURE [dbo].[spuExtTestConsultant]
        @ExternalTestConsultantId uniqueidentifier,
        @Name varchar(50),
        @Department varchar(50),
        @Email varchar(50),
        @SsoId uniqueidentifier,
        @IsInactive BIT,
        @InactiveDate DATETIME = NULL,
        @SsoIdExt uniqueidentifier = NULL
AS
BEGIN
        SET NOCOUNT ON;

        UPDATE
                tblExternalTestConsultant
        SET
                fldName = @Name,
                fldDepartment = @Department,
                fldLoginInstructionsSent = CASE WHEN ISNULL(fldEmail, '') <> ISNULL(@Email, '') THEN 0 ELSE fldLoginInstructionsSent END,
                fldLoginInstructionsSentDate = CASE WHEN ISNULL(fldEmail, '') <> ISNULL(@Email, '') THEN NULL ELSE fldLoginInstructionsSentDate END,
                fldEmail = @Email,
                fldSsoId = @SsoId,
                fldSsoIdExt = CASE WHEN ISNULL(fldEmail, '') <> ISNULL(@Email, '') THEN NULL ELSE @SsoIdExt END,
                fldIsInactive = @IsInactive,
                fldInactiveDate = @InactiveDate
        WHERE
                fldExternalTestConsultantId = @ExternalTestConsultantId
END

GO
