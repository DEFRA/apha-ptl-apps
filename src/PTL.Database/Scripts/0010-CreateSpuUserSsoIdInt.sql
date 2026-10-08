-- Backfills fldSsoIdInt on a tblUsers row previously matched by username - mirrors
-- spuUserSsoIdExt/spuUserSsoUserIdInt's role for the external-user tables.
CREATE OR ALTER PROCEDURE [dbo].[spuUserSsoIdInt]
	@UserId uniqueidentifier,
	@SsoIdInt uniqueidentifier
AS
BEGIN
	SET NOCOUNT ON;

	UPDATE
		tblUsers
	SET
		fldSsoIdInt = @SsoIdInt
	WHERE
		fldUserId = @UserId
END

GO
GRANT EXECUTE ON [dbo].[spuUserSsoIdInt] TO [ProficiencyTestingInternalUser]
GO
