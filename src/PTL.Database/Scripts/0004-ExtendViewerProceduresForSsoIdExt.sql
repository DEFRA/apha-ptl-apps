-- Extends the existing legacy Viewer procedures to also recognise the new CIDM fldSsoIdExt
-- identity, and to read/write fldEmail (previously selected nowhere for Viewer - needed so
-- CIDM resolution can fall back to matching by email, and so the mapped entity's Email property
-- is actually populated). @SsoIdExt is appended as the LAST parameter so the existing
-- named-parameter EXEC calls keep working unchanged.
CREATE OR ALTER PROCEDURE [dbo].[spgViewerBySsoId]
	@SsoId uniqueidentifier = NULL,
	@SsoIdExt uniqueidentifier = NULL
AS
BEGIN
	SET NOCOUNT ON;

	IF @SsoIdExt IS NOT NULL
	BEGIN
		SELECT
			fldViewerId,
			fldName,
			fldEmail,
			fldSsoId,
			fldSsoIdExt
		FROM
			tblViewer
		WHERE
			fldSsoIdExt = @SsoIdExt
	END
	ELSE
	BEGIN
		SELECT
			fldViewerId,
			fldName,
			fldEmail,
			fldSsoId,
			fldSsoIdExt
		FROM
			tblViewer
		WHERE
			fldSsoId = @SsoId
	END
END

GO

CREATE OR ALTER PROCEDURE [dbo].[spiViewer]
	@ViewerId uniqueidentifier,
	@Name varchar(50),
	@Email varchar(50),
	@SsoId uniqueidentifier,
	@SsoIdExt uniqueidentifier = NULL
AS
BEGIN
	SET NOCOUNT ON;

	INSERT INTO tblViewer
	(
		fldViewerId,
		fldName,
		fldEmail,
		fldSsoId,
		fldSsoIdExt
	)
	VALUES
	(
		@ViewerId,
		@Name,
		@Email,
		@SsoId,
		@SsoIdExt
	)
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
		fldEmail = @Email,
		fldSsoId = @SsoId,
		fldSsoIdExt = @SsoIdExt
	WHERE
		fldViewerId = @ViewerId
END
