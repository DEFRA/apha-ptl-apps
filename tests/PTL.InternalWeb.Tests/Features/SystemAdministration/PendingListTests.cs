using PTL.InternalWeb.Features.SystemAdministration;

namespace PTL.InternalWeb.Tests.Features.SystemAdministration;

public class PendingListTests
{
    private static readonly string[] ThreeRows = ["a", "b", "c"];

    [Theory]
    [InlineData(0, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(-1, false)]
    [InlineData(null, false)]
    public void IsRow_IsTrueOnlyForAnIndexInsideTheList(int? index, bool expected) =>
        Assert.Equal(expected, PendingList.IsRow(ThreeRows, index));

    [Fact]
    public void IsRow_IsFalseForAnEmptyList() =>
        Assert.False(PendingList.IsRow(Array.Empty<string>(), 0));

    [Theory]
    [InlineData(null, 0, null)]
    [InlineData(1, 1, null)]
    [InlineData(3, 1, 2)]
    [InlineData(0, 1, 0)]
    [InlineData(2, 2, null)]
    public void EditIndexAfterRemoval_FollowsTheEditedRowOrClearsItWhenThatRowWasRemoved(int? editIndex, int removedIndex, int? expected) =>
        Assert.Equal(expected, PendingList.EditIndexAfterRemoval(editIndex, removedIndex));
}

public class PendingListCommandsTests
{
    [Theory]
    [InlineData("edit", "edit", true)]
    [InlineData("EDIT", "edit", true)]
    [InlineData("cancelEdit", "canceledit", true)]
    [InlineData("save", "apply", false)]
    [InlineData("", "save", false)]
    [InlineData(null, "save", false)]
    public void Is_ComparesIgnoringCaseAndTreatsNullAsNoMatch(string? command, string expected, bool match) =>
        Assert.Equal(match, PendingListCommands.Is(command, expected));

    [Fact]
    public void Commands_HaveTheValuesTheViewsPostInTheQueryString() =>
        Assert.Equal(
            ["edit", "update", "cancelEdit", "add", "remove", "apply", "save", "cancel", "submit"],
            [
                PendingListCommands.Edit, PendingListCommands.Update, PendingListCommands.CancelEdit,
                PendingListCommands.Add, PendingListCommands.Remove, PendingListCommands.Apply,
                PendingListCommands.Save, PendingListCommands.Cancel, PendingListCommands.Submit
            ]);
}
