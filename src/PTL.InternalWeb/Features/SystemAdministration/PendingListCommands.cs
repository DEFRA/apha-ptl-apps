namespace PTL.InternalWeb.Features.SystemAdministration;

// Submit-button commands (the "command" query value) understood by the Manage Viewers and Manage
// Countries POSTs, mirroring the legacy GridView + page-level button workflow:
//   edit       - GridView_RowEditing: puts one row into edit mode.
//   update     - GridView_RowUpdating: applies the edited row to the pending list only.
//   cancelEdit - GridView_RowCancelingEdit: discards the row's edit, keeping other pending changes.
//   add        - ButtonAdd_Click: appends a pending row.
//   remove     - Remove: confirmation page, then deletes the record.
//   apply/save - ButtonApply/ButtonSave: write the pending rows; save then leaves the page.
//   cancel     - ButtonCancel: leaves the page, discarding all pending changes.
//   submit     - the Enter key in a text box (the hidden default button).
public static class PendingListCommands
{
    public const string Edit = "edit";

    public const string Update = "update";

    public const string CancelEdit = "cancelEdit";

    public const string Add = "add";

    public const string Remove = "remove";

    public const string Apply = "apply";

    public const string Save = "save";

    public const string Cancel = "cancel";

    public const string Submit = "submit";

    public static bool Is(string? command, string expected) =>
        string.Equals(command, expected, StringComparison.OrdinalIgnoreCase);
}

public static class PendingList
{
    public static bool IsRow<T>(IReadOnlyList<T> rows, int? index) =>
        index is >= 0 && index < rows.Count;

    // Keeps the edit-mode row pointing at the same row after the row at removedIndex is removed.
    public static int? EditIndexAfterRemoval(int? editIndex, int removedIndex)
    {
        if (editIndex is not { } index || index == removedIndex)
        {
            return null;
        }

        return index > removedIndex ? index - 1 : index;
    }
}
