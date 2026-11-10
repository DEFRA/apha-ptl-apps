-- Login instruction emails for Participants, Viewers and External Test Consultants (Manage Participants / Manage
-- Viewers / Manage External Test Consultants "Login Instructions" column).
--
-- fldLoginInstructionsSent  bit NOT NULL      True once the login instructions email has been sent to the saved address.
-- fldLoginInstructionsSentDate datetime2 NULL  When it was sent (null for users that existed before this script).
--
-- Every user that already exists is set to SENT (1), so they show "Sent" straight after deployment. The column default is
-- then changed to NOT SENT (0), so every user created after deployment starts as "Send Instructions" until the email is sent.
-- Changing a saved email address clears the flag (spuParticipant / spuViewer / spuExtTestConsultant below), which brings
-- "Send Instructions" back for the new address.
--
-- Safe to re-run: each step checks whether it has already been applied.

------------------------------------------------------------------------------------------------------------------------
-- 1. Columns: existing rows become Sent (1); the default for new rows is Not sent (0)
------------------------------------------------------------------------------------------------------------------------
IF COL_LENGTH('dbo.tblParticipant', 'fldLoginInstructionsSent') IS NULL
BEGIN
        ALTER TABLE [dbo].[tblParticipant] ADD
                [fldLoginInstructionsSent] BIT NOT NULL CONSTRAINT [DF_tblParticipant_fldLoginInstructionsSent] DEFAULT (1),
                [fldLoginInstructionsSentDate] DATETIME2 NULL
END

GO

IF COL_LENGTH('dbo.tblViewer', 'fldLoginInstructionsSent') IS NULL
BEGIN
        ALTER TABLE [dbo].[tblViewer] ADD
                [fldLoginInstructionsSent] BIT NOT NULL CONSTRAINT [DF_tblViewer_fldLoginInstructionsSent] DEFAULT (1),
                [fldLoginInstructionsSentDate] DATETIME2 NULL
END

GO

IF COL_LENGTH('dbo.tblExternalTestConsultant', 'fldLoginInstructionsSent') IS NULL
BEGIN
        ALTER TABLE [dbo].[tblExternalTestConsultant] ADD
                [fldLoginInstructionsSent] BIT NOT NULL CONSTRAINT [DF_tblExternalTestConsultant_fldLoginInstructionsSent] DEFAULT (1),
                [fldLoginInstructionsSentDate] DATETIME2 NULL
END

GO

-- Existing rows now hold 1. Swap the default to 0 for rows inserted from here on (the insert procedures do not name the column).
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_tblParticipant_fldLoginInstructionsSent')
BEGIN
        ALTER TABLE [dbo].[tblParticipant] DROP CONSTRAINT [DF_tblParticipant_fldLoginInstructionsSent]
        ALTER TABLE [dbo].[tblParticipant] ADD CONSTRAINT [DF_tblParticipant_fldLoginInstructionsSent] DEFAULT (0) FOR [fldLoginInstructionsSent]
END

IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_tblViewer_fldLoginInstructionsSent')
BEGIN
        ALTER TABLE [dbo].[tblViewer] DROP CONSTRAINT [DF_tblViewer_fldLoginInstructionsSent]
        ALTER TABLE [dbo].[tblViewer] ADD CONSTRAINT [DF_tblViewer_fldLoginInstructionsSent] DEFAULT (0) FOR [fldLoginInstructionsSent]
END

IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_tblExternalTestConsultant_fldLoginInstructionsSent')
BEGIN
        ALTER TABLE [dbo].[tblExternalTestConsultant] DROP CONSTRAINT [DF_tblExternalTestConsultant_fldLoginInstructionsSent]
        ALTER TABLE [dbo].[tblExternalTestConsultant] ADD CONSTRAINT [DF_tblExternalTestConsultant_fldLoginInstructionsSent] DEFAULT (0) FOR [fldLoginInstructionsSent]
END

GO

------------------------------------------------------------------------------------------------------------------------
-- 2. Procedures that list the users: also return fldLoginInstructionsSent
------------------------------------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[spgaViewers]
AS
BEGIN
        SET NOCOUNT ON;

        --Viewer
        SELECT
                tblViewer.fldViewerId,
                tblViewer.fldName,
                tblViewer.fldEmail,
                tblViewer.fldSsoId,
                tblViewer.fldLoginInstructionsSent
        FROM
                tblViewer
        ORDER BY fldName

        --ViewerSchemes
        SELECT
                tlnkViewerScheme.fldViewerSchemeId,
                tlnkViewerScheme.fldViewerId,
                tlnkViewerScheme.fldSchemeId,
                tblScheme.fldName,
                tblScheme.fldIdentifier,
                tblScheme.fldYearId
        FROM
                tlnkViewerScheme
        INNER JOIN tblScheme ON tlnkViewerScheme.fldSchemeId = tblScheme.fldSchemeId

        --ViewerParticipants
        SELECT
                tlnkViewerParticipant.fldViewerParticipantId,
                tlnkViewerParticipant.fldViewerId,
                tlnkViewerParticipant.fldParticipantId,
                tblParticipant.fldLabCode,
                tblParticipant.fldLabName
        FROM
                tlnkViewerParticipant
        INNER JOIN tblParticipant ON tlnkViewerParticipant.fldParticipantId = tblParticipant.fldParticipantId
END

GO

CREATE OR ALTER PROCEDURE [dbo].[spgaExtTestConsultants]
AS
BEGIN
        SET NOCOUNT ON;

        SELECT
                fldExternalTestConsultantId,
                fldName,
                fldDepartment,
                fldEmail,
                fldSsoId,
                (dbo.fnCountExtTCAllocationById(fldExternalTestConsultantId)) as fldAllocationCount,
                (dbo.fnGetExtTCAllocationStringById(fldExternalTestConsultantId)) as fldAllocationString,
                fldIsInactive,
                fldInactiveDate,
                fldLoginInstructionsSent
        FROM
                tblExternalTestConsultant
        ORDER BY fldName
END

GO

CREATE OR ALTER PROCEDURE [dbo].[spgParticipantInfoByCustomerId]
        @CustomerId uniqueidentifier,
        @ActiveOnly bit
AS
BEGIN
        SET NOCOUNT ON;

        SELECT
                fldParticipantId,
                fldSsoId,
                fldCustomerId,
                fldLabCode,
                fldLabName,
                fldContactName,
                fldIsActive,
                fldInactiveDate,
                fldLoginInstructionsSent
        INTO #temp1
        FROM
                tblParticipant
        WHERE
                fldCustomerId = @CustomerId and
                isnumeric(fldLabCode) = 1
                AND (not @ActiveOnly = 1 or fldIsActive = 1)
        ORDER BY
                CAST(fldLabCode as int)

        SELECT
                fldParticipantId,
                fldSsoId,
                fldCustomerId,
                fldLabCode,
                fldLabName,
                fldContactName,
                fldIsActive,
                fldInactiveDate,
                fldLoginInstructionsSent
        INTO #temp2
        FROM
                tblParticipant
        WHERE
                fldCustomerId = @CustomerId and
                isnumeric(fldLabCode) = 0
                AND (not @ActiveOnly = 1 or fldIsActive = 1)
        ORDER BY
                substring(fldLabCode, 0, 4),
                CAST(substring(fldLabCode, 5, 8) as int)

        SELECT * FROM #temp1 UNION ALL SELECT * FROM #temp2
END

GO

------------------------------------------------------------------------------------------------------------------------
-- 3. Update procedures: a changed email address clears the sent flag.
--    In an UPDATE the right-hand side reads the row as it was before the update, so fldEmail below is the previously saved
--    address. The comparison uses the column's collation, so a change of letter case alone does not clear the flag.
--    Everything else is unchanged from scripts 0002 / 0004 / 0006.
------------------------------------------------------------------------------------------------------------------------
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
                fldSsoIdExt = @SsoIdExt
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
                fldSsoIdExt = @SsoIdExt,
                fldIsInactive = @IsInactive,
                fldInactiveDate = @InactiveDate
        WHERE
                fldExternalTestConsultantId = @ExternalTestConsultantId
END

GO

------------------------------------------------------------------------------------------------------------------------
-- 4. New procedures: record that the login instructions email has been sent
------------------------------------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[spuParticipantLoginInstructions]
        @ParticipantId uniqueidentifier,
        @LoginInstructionsSent bit,
        @LoginInstructionsSentDate datetime2 = NULL
AS
BEGIN
        SET NOCOUNT ON;

        UPDATE tblParticipant
        SET
                fldLoginInstructionsSent = @LoginInstructionsSent,
                fldLoginInstructionsSentDate = @LoginInstructionsSentDate
        WHERE
                fldParticipantId = @ParticipantId
END

GO
GRANT EXECUTE ON [dbo].[spuParticipantLoginInstructions] TO [ProficiencyTestingInternalUser]
GO

CREATE OR ALTER PROCEDURE [dbo].[spuViewerLoginInstructions]
        @ViewerId uniqueidentifier,
        @LoginInstructionsSent bit,
        @LoginInstructionsSentDate datetime2 = NULL
AS
BEGIN
        SET NOCOUNT ON;

        UPDATE tblViewer
        SET
                fldLoginInstructionsSent = @LoginInstructionsSent,
                fldLoginInstructionsSentDate = @LoginInstructionsSentDate
        WHERE
                fldViewerId = @ViewerId
END

GO
GRANT EXECUTE ON [dbo].[spuViewerLoginInstructions] TO [ProficiencyTestingInternalUser]
GO

CREATE OR ALTER PROCEDURE [dbo].[spuExtTestConsultantLoginInstructions]
        @ExternalTestConsultantId uniqueidentifier,
        @LoginInstructionsSent bit,
        @LoginInstructionsSentDate datetime2 = NULL
AS
BEGIN
        SET NOCOUNT ON;

        UPDATE tblExternalTestConsultant
        SET
                fldLoginInstructionsSent = @LoginInstructionsSent,
                fldLoginInstructionsSentDate = @LoginInstructionsSentDate
        WHERE
                fldExternalTestConsultantId = @ExternalTestConsultantId
END

GO
GRANT EXECUTE ON [dbo].[spuExtTestConsultantLoginInstructions] TO [ProficiencyTestingInternalUser]
GO
