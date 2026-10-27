-- Enforces one tblUsers row per username. The Create User screen checks for an existing account
-- before inserting, but two admins creating the same user at once could both pass that check;
-- this index makes the second insert fail (UserRepository turns that into the normal
-- "account already exists" message).
--
-- The script stops with an error if duplicate usernames already exist, so they can be resolved by
-- hand (decide which row to keep - roles and audit entries point at fldUserId) before re-running.
-- The comparison follows the column's collation, which is case-insensitive for the PT-LIMS database,
-- matching the case-insensitive duplicate check in UserService.
IF EXISTS (SELECT 1 FROM [dbo].[tblUsers] GROUP BY [fldUsername] HAVING COUNT(*) > 1)
BEGIN
    SELECT [fldUsername], COUNT(*) AS [Occurrences]
    FROM [dbo].[tblUsers]
    GROUP BY [fldUsername]
    HAVING COUNT(*) > 1;

    THROW 50000, 'Duplicate fldUsername values exist in tblUsers - resolve them (see the result set above) before creating IX_tblUsers_fldUsername.', 1;
END

GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_tblUsers_fldUsername]
ON [dbo].[tblUsers] ([fldUsername])
