-- Reuses the legacy app's existing sign-in lookup procedure (CustomIdentity.vb's DataPortal_Fetch
-- calls this directly) for Entra ID internal-user resolution too, instead of maintaining a parallel
-- lookup procedure - same pattern as 0002/0004/0006 for the external-user tables.
--
-- CRITICAL: the legacy VB reader (CustomIdentity.vb) reads the first result set strictly by
-- ORDINAL - dr.GetGuid(0) then dr.GetBoolean(1) - so @UserId and fldIsInactive MUST stay in those
-- exact first two positions; every new column is appended after them, never inserted before.
--
-- Parameter priority when both are supplied: @SsoIdInt first, then @Username - matching the
-- precedent set for the external-user procedures. @SsoIdInt defaults to NULL, so the legacy app's
-- existing single-parameter call (@Username only) is unaffected.
--
-- The original proc wrapped the first SELECT in `IF (@UserId IS NOT NULL) BEGIN ... END`, so when
-- no match was found the client only ever received ONE result set (the empty roles list), not two.
-- That's fine for the legacy VB reader (its "not found" branch never calls dr.NextResult() at all),
-- but makes the result unreliable to consume generically via Dapper's QueryMultipleAsync (the
-- number of result sets would vary). The IF wrapper is removed here - the first SELECT now always
-- runs and simply returns zero rows when @UserId is NULL, so callers always get exactly two result
-- sets. dr.Read() on an empty result set still returns False, so the legacy VB behaviour (and its
-- ordinal reads) is completely unaffected by this change.
CREATE OR ALTER PROCEDURE [dbo].[sppAuthenticate]
	@Username varchar(50) = NULL,
	@SsoIdInt uniqueidentifier = NULL
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @UserId uniqueidentifier

	IF @SsoIdInt IS NOT NULL BEGIN

		SELECT
			@UserId = fldUserId
		FROM
			tblUsers
		WHERE
			fldSsoIdInt = @SsoIdInt;

	END ELSE BEGIN

		SELECT
			@UserId = fldUserId
		FROM
			tblUsers
		WHERE
			fldUsername = @Username;

	END

	SELECT @UserId AS fldUserId,
	fldIsInactive,
	fldUsername,
	fldFriendlyName,
	fldFirstName,
	fldLastName,
	fldEmail,
	fldDepartment,
	fldInactiveDate,
	fldSsoIdInt

	FROM tblUsers 

	WHERE fldUserId = @UserId

	SELECT
		r.fldRole 
	FROM
		tlnkUserRoles ur INNER JOIN tblRoles r
	ON
		ur.fldRoleId = r.fldRoleId
		INNER JOIN tblUsers ut ON ut.fldUserId = ur.fldUserId
	WHERE
		ur.fldUserId = @UserId
		AND NOT
		(ut.fldIsInactive = 1 AND r.fldRole = 'Test Consultant')
	ORDER BY
		r.fldOrder
END

GO
GRANT EXECUTE ON [dbo].[sppAuthenticate] TO [ProficiencyTestingInternalUser]
GO
