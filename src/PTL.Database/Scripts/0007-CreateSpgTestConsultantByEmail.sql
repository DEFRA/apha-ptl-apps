-- New procedure for CIDM external-user resolution: lookup by email, used only as a fallback the
-- first time a user signs in through CIDM and has no fldSsoIdExt recorded yet.
CREATE OR ALTER PROCEDURE [dbo].[spgTestConsultantByEmail]
	@Email varchar(50)
AS
BEGIN
	SET NOCOUNT ON;

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
		fldEmail = @Email
END
