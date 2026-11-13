namespace PTL.InternalWeb.Features.SystemAdministration;

// Submit-button commands (the "command" query value) understood by the pending-list pages (Manage Viewers,
// Manage Countries, the two Test Consultant pages), mirroring the legacy GridView + page-level button workflow:
//   edit       - GridView_RowEditing: puts one row into edit mode.
//   update     - GridView_RowUpdating: applies the edited row to the pending list only.
//   cancelEdit - GridView_RowCancelingEdit: discards the row's edit, keeping other pending changes.
//   add        - ButtonAdd_Click: appends a pending row.
//   remove     - Remove: viewers/countries show a confirmation page then mark the row removed; the test
//                consultant pages mark it removed directly. Nothing is deleted until the page is saved.
//   toggle     - the Status link of the test consultant pages: flips Active/Inactive in the pending list.
//   sendInstructions - the Send Instructions link of the Viewer and External Test Consultant pages: emails the saved address now.
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

    public const string Toggle = "toggle";

    public const string SendInstructions = "sendInstructions";

    public const string Apply = "apply";

    public const string Save = "save";

    public const string Cancel = "cancel";

    public const string Submit = "submit";

    public static bool Is(string? command, string expected) =>
        string.Equals(command, expected, StringComparison.OrdinalIgnoreCase);
}

// The banner at the top of a pending-list page (all four carry a Message the page shows when MessageIsError is set).
public interface IPendingListMessage
{
    string? Message { get; set; }

    bool MessageIsError { get; set; }
}

public static class PendingList
{
    // An outcome of a save or remove (as opposed to a problem with a field) goes in the banner at the top of the
    // page, not the validation summary; several outcomes are joined.
    public static void AddOutcomeError(IPendingListMessage model, string message)
    {
        ArgumentNullException.ThrowIfNull(model);

        model.Message = model.MessageIsError && !string.IsNullOrEmpty(model.Message) ? $"{model.Message} {message}" : message;
        model.MessageIsError = true;
    }

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
