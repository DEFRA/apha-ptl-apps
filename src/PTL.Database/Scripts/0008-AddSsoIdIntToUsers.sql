-- Adds the new Entra ID internal-identity column to tblUsers (legacy's internal/Windows-authenticated
-- user table) - a brand-new column, since tblUsers has no existing SSO/external-identifier column at
-- all (confirmed via a full repo grep of the legacy proficiency-testing-2026-08-17 codebase).
ALTER TABLE [dbo].[tblUsers]
ADD [fldSsoIdInt] UNIQUEIDENTIFIER NULL

GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_tblUsers_fldSsoIdInt]
ON [dbo].[tblUsers] ([fldSsoIdInt])
WHERE [fldSsoIdInt] IS NOT NULL
