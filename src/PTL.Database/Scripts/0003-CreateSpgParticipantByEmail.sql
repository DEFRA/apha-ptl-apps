-- New procedure for CIDM external-user resolution: lookup by email, used only as a fallback the
-- first time a user signs in through CIDM and has no fldSsoIdExt recorded yet (mirrors
-- D2R2/CDC's spgUserByEmailAddress).
CREATE OR ALTER PROCEDURE [dbo].[spgParticipantByEmail]
	@Email varchar(150)
AS
BEGIN
	SET NOCOUNT ON;

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
		fldEmail = @Email
END
