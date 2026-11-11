-- Schema changes for CIDM external-user linking across the four user tables (Participant, Viewer,
-- External Test Consultant, Lab User). Safe to re-run: every change is guarded, so it creates the Lab User
-- tables where they do not exist yet and only alters them where they do.

-- Filtered indexes need these on; sqlcmd defaults QUOTED_IDENTIFIER to OFF unless run with -I.
SET QUOTED_IDENTIFIER ON
SET ANSI_NULLS ON

GO

-- 0. Lab users, and the mapping of each lab user to the labs (participant records) they can act for.
--    The columns added by sections 1 and 3 below are included here so a new database gets the final shape.
IF OBJECT_ID(N'[dbo].[tblLabUser]', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[tblLabUser](
		[fldLabUserId] [uniqueidentifier] NOT NULL CONSTRAINT [DF_tblLabUser_fldLabUserId] DEFAULT (newid()),
		[fldName] [nvarchar](255) NOT NULL,
		[fldEmail] [nvarchar](255) NOT NULL,
		[fldSsoId] [nvarchar](255) NOT NULL,
		[fldSsoIdExt] [uniqueidentifier] NULL,
		CONSTRAINT [PK_tblLabUser] PRIMARY KEY CLUSTERED ([fldLabUserId] ASC)
	)
END

GO

IF OBJECT_ID(N'[dbo].[tlnkLabUserParticipant]', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[tlnkLabUserParticipant](
		[fldLabUserParticipantId] [uniqueidentifier] NOT NULL CONSTRAINT [DF_tlnkLabUserParticipant_fldLabUserParticipantId] DEFAULT (newid()),
		[fldLabUserId] [uniqueidentifier] NOT NULL,
		[fldParticipantId] [uniqueidentifier] NOT NULL,
		[fldIsActive] [bit] NOT NULL CONSTRAINT [DF_tlnkLabUserParticipant_fldIsActive] DEFAULT (1),
		CONSTRAINT [PK_tlnkLabUserParticipant] PRIMARY KEY CLUSTERED ([fldLabUserParticipantId] ASC),
		CONSTRAINT [FK_tlnkLabUserParticipant_tblLabUser] FOREIGN KEY ([fldLabUserId]) REFERENCES [dbo].[tblLabUser] ([fldLabUserId]),
		CONSTRAINT [FK_tlnkLabUserParticipant_tblParticipant] FOREIGN KEY ([fldParticipantId]) REFERENCES [dbo].[tblParticipant] ([fldParticipantId])
	)
END

GO

-- 1. Lab users get the same CIDM identity column as the other external-user tables. fldSsoId is the
--    legacy SSO identifier and must not be repurposed.
IF COL_LENGTH('dbo.tblLabUser', 'fldSsoIdExt') IS NULL
BEGIN
	ALTER TABLE [dbo].[tblLabUser]
	ADD [fldSsoIdExt] UNIQUEIDENTIFIER NULL
END

GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_tblLabUser_fldSsoIdExt' AND object_id = OBJECT_ID('dbo.tblLabUser'))
BEGIN
	CREATE UNIQUE NONCLUSTERED INDEX [IX_tblLabUser_fldSsoIdExt]
	ON [dbo].[tblLabUser] ([fldSsoIdExt])
	WHERE [fldSsoIdExt] IS NOT NULL
END

GO

-- 2. A person can be the primary contact of several participant (lab) rows, so one CIDM contact id must
--    be able to appear on more than one tblParticipant row. The index stays (filtered, for the lookup by
--    contact id) but is no longer unique. Viewer, Test Consultant and Lab User remain one row per person.
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_tblParticipant_fldSsoIdExt' AND object_id = OBJECT_ID('dbo.tblParticipant') AND is_unique = 1)
BEGIN
	DROP INDEX [IX_tblParticipant_fldSsoIdExt] ON [dbo].[tblParticipant]
END

GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_tblParticipant_fldSsoIdExt' AND object_id = OBJECT_ID('dbo.tblParticipant'))
BEGIN
	CREATE NONCLUSTERED INDEX [IX_tblParticipant_fldSsoIdExt]
	ON [dbo].[tblParticipant] ([fldSsoIdExt])
	WHERE [fldSsoIdExt] IS NOT NULL
END

GO

-- 3. The lab user -> participant (lab) mapping gets an active flag so access to a single lab can be
--    switched off without deleting the link. Existing links stay active.
IF COL_LENGTH('dbo.tlnkLabUserParticipant', 'fldIsActive') IS NULL
BEGIN
	ALTER TABLE [dbo].[tlnkLabUserParticipant]
	ADD [fldIsActive] BIT NOT NULL
	CONSTRAINT [DF_tlnkLabUserParticipant_fldIsActive] DEFAULT (1)
END

GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_tlnkLabUserParticipant_fldLabUserId' AND object_id = OBJECT_ID('dbo.tlnkLabUserParticipant'))
BEGIN
	CREATE NONCLUSTERED INDEX [IX_tlnkLabUserParticipant_fldLabUserId]
	ON [dbo].[tlnkLabUserParticipant] ([fldLabUserId])
	INCLUDE ([fldParticipantId], [fldIsActive])
END

GO

-- 4. Sign-in matches an unlinked user by email, so each table needs an index on it. The Lab User table is
--    small and its email column is nvarchar (matching the varchar lookup would force a scan anyway), so
--    it is deliberately not indexed.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_tblParticipant_fldEmail' AND object_id = OBJECT_ID(N'[dbo].[tblParticipant]'))
BEGIN
	CREATE NONCLUSTERED INDEX [IX_tblParticipant_fldEmail]
	ON [dbo].[tblParticipant] ([fldEmail])
END

GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_tblViewer_fldEmail' AND object_id = OBJECT_ID('dbo.tblViewer'))
BEGIN
	CREATE NONCLUSTERED INDEX [IX_tblViewer_fldEmail]
	ON [dbo].[tblViewer] ([fldEmail])
END

GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_tblExternalTestConsultant_fldEmail' AND object_id = OBJECT_ID('dbo.tblExternalTestConsultant'))
BEGIN
	CREATE NONCLUSTERED INDEX [IX_tblExternalTestConsultant_fldEmail]
	ON [dbo].[tblExternalTestConsultant] ([fldEmail])
END
