-- New generic audit trail for System Administration changes - no legacy precedent exists for
-- this (legacy only ever audited Invoice generation and Sample/Distribution tabulation changes,
-- via tblAuditInvoiceGeneration/spiAuditSample). Confirmed gap per the HLD's "Role assignments,
-- privileged actions and access events are logged and available for audit purposes" requirement.
CREATE TABLE [dbo].[tblSystemAdministrationAudit] (
	[fldAuditId] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
	[fldActingUserId] UNIQUEIDENTIFIER NULL,
	[fldAction] VARCHAR(100) NOT NULL,
	[fldEntityType] VARCHAR(100) NOT NULL,
	[fldEntityId] VARCHAR(100) NULL,
	[fldDetails] VARCHAR(1000) NULL,
	[fldAuditDate] DATETIME2 NOT NULL CONSTRAINT [DF_tblSystemAdministrationAudit_fldAuditDate] DEFAULT (SYSUTCDATETIME())
)

GO

CREATE PROCEDURE [dbo].[spiSystemAdministrationAudit]
	@AuditId uniqueidentifier,
	@ActingUserId uniqueidentifier = NULL,
	@Action varchar(100),
	@EntityType varchar(100),
	@EntityId varchar(100) = NULL,
	@Details varchar(1000) = NULL
AS
BEGIN
	SET NOCOUNT ON;

	INSERT INTO tblSystemAdministrationAudit
		(fldAuditId, fldActingUserId, fldAction, fldEntityType, fldEntityId, fldDetails)
	VALUES
		(@AuditId, @ActingUserId, @Action, @EntityType, @EntityId, @Details)
END

GO
GRANT EXECUTE ON [dbo].[spiSystemAdministrationAudit] TO [ProficiencyTestingInternalUser]
GO

CREATE PROCEDURE [dbo].[spgaSystemAdministrationAudit]
AS
BEGIN
	SET NOCOUNT ON;

	SELECT
		fldAuditId AS AuditId,
		fldActingUserId AS ActingUserId,
		fldAction AS Action,
		fldEntityType AS EntityType,
		fldEntityId AS EntityId,
		fldDetails AS Details,
		fldAuditDate AS AuditDate
	FROM
		tblSystemAdministrationAudit
	ORDER BY
		fldAuditDate DESC
END

GO
GRANT EXECUTE ON [dbo].[spgaSystemAdministrationAudit] TO [ProficiencyTestingInternalUser]
GO
