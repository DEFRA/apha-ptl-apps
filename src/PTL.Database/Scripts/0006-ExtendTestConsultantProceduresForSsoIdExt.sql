-- Extends the existing legacy TestConsultant procedures to also recognise the new CIDM
-- fldSsoIdExt identity, and to read fldEmail/fldDepartment (previously selected nowhere by
-- spgTestConsultantBySsoId - needed so CIDM resolution can fall back to matching by email, and
-- so the mapped entity has a populated Email/Department). @SsoIdExt is appended as the LAST
-- parameter so the existing named-parameter EXEC calls keep working unchanged.
CREATE OR ALTER PROCEDURE [dbo].[spgTestConsultantBySsoId]
	@SsoId uniqueidentifier = NULL,
	@SsoIdExt uniqueidentifier = NULL
AS
BEGIN
	SET NOCOUNT ON;

	IF @SsoIdExt IS NOT NULL
	BEGIN
		SELECT
			fldExternalTestConsultantId,
			fldName,
			fldDepartment,
			fldEmail,
			fldSsoId,
			fldSsoIdExt,
			fldIsInactive
		FROM
			tblExternalTestConsultant
		WHERE
			fldSsoIdExt = @SsoIdExt
	END
	ELSE
	BEGIN
		SELECT
			fldExternalTestConsultantId,
			fldName,
			fldDepartment,
			fldEmail,
			fldSsoId,
			fldSsoIdExt,
			fldIsInactive
		FROM
			tblExternalTestConsultant
		WHERE
			fldSsoId = @SsoId
	END
END

GO

CREATE OR ALTER PROCEDURE [dbo].[spiExtTestConsultant]
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

	INSERT INTO tblExternalTestConsultant
	(
		fldExternalTestConsultantId,
		fldName,
		fldDepartment,
		fldEmail,
		fldSsoId,
		fldSsoIdExt,
		fldIsInactive,
		fldInactiveDate
	)
	VALUES
	(
		@ExternalTestConsultantId,
		@Name,
		@Department,
		@Email,
		@SsoId,
		@SsoIdExt,
		@IsInactive,
		@InactiveDate
	)
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
		fldEmail = @Email,
		fldSsoId = @SsoId,
		fldSsoIdExt = @SsoIdExt,
		fldIsInactive = @IsInactive,
		fldInactiveDate = @InactiveDate
	WHERE
		fldExternalTestConsultantId = @ExternalTestConsultantId
END
