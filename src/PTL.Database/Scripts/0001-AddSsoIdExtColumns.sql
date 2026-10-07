-- Adds the new CIDM external-identity column to all three external-user tables, alongside (not
-- replacing) the existing legacy fldSsoId column - same rationale as D2R2/CDC's SsoUserId vs
-- SsoUserIdExt split: fldSsoId is the old VLA SSO system's identifier and must not be repurposed.
ALTER TABLE [dbo].[tblParticipant]
ADD [fldSsoIdExt] UNIQUEIDENTIFIER NULL

GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_tblParticipant_fldSsoIdExt]
ON [dbo].[tblParticipant] ([fldSsoIdExt])
WHERE [fldSsoIdExt] IS NOT NULL

GO

ALTER TABLE [dbo].[tblViewer]
ADD [fldSsoIdExt] UNIQUEIDENTIFIER NULL

GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_tblViewer_fldSsoIdExt]
ON [dbo].[tblViewer] ([fldSsoIdExt])
WHERE [fldSsoIdExt] IS NOT NULL

GO

ALTER TABLE [dbo].[tblExternalTestConsultant]
ADD [fldSsoIdExt] UNIQUEIDENTIFIER NULL

GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_tblExternalTestConsultant_fldSsoIdExt]
ON [dbo].[tblExternalTestConsultant] ([fldSsoIdExt])
WHERE [fldSsoIdExt] IS NOT NULL
