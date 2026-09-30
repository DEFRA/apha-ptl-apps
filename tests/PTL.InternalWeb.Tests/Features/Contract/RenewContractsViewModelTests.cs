using PTL.Contracts.Contract;
using PTL.InternalWeb.Features.Contract;

namespace PTL.InternalWeb.Tests.Features.Contract;

// Covers the display projection legacy MergeContractsViewModel.GetDisplayModel() performs:
// duplicate identifiers collapse into one row, ordered by numeric lab code then old scheme.
public class RenewContractsViewModelTests
{
    [Fact]
    public void ItemRows_GroupsByIdentifierAndOrdersByNumericLabCodeThenOldScheme()
    {
        var model = new RenewContractsViewModel
        {
            Items =
            [
                Item(labCode: "10", oldScheme: "S1", identifier: "10S1"),
                Item(labCode: "2", oldScheme: "S2", identifier: "2S2"),
                Item(labCode: "2", oldScheme: "S1", identifier: "2S1"),
                Item(labCode: "10", oldScheme: "S1", identifier: "10S1")
            ]
        };

        var rows = model.ItemRows;

        Assert.Equal(3, rows.Count);
        Assert.Equal(["2S1", "2S2", "10S1"], rows.Select(r => r.Identifier));
        Assert.Equal(2, rows[2].Items.Count);
        Assert.Equal("10", rows[2].First.LabCode);
    }

    [Fact]
    public void ItemRows_NonNumericLabCode_SortsLast()
    {
        var model = new RenewContractsViewModel
        {
            Items = [Item(labCode: "LAB", oldScheme: "S1", identifier: "LABS1"), Item(labCode: "7", oldScheme: "S1", identifier: "7S1")]
        };

        Assert.Equal(["7S1", "LABS1"], model.ItemRows.Select(r => r.Identifier));
    }

    [Fact]
    public void ItemRows_IsComputedOnceAndCached()
    {
        var model = new RenewContractsViewModel { Items = [Item("1", "S1", "1S1")] };

        Assert.Same(model.ItemRows, model.ItemRows);
    }

    [Fact]
    public void IsRowVisible_OnlyWhenOneOfTheRowsContractsIsSelected()
    {
        var contractId = Guid.NewGuid();
        var model = new RenewContractsViewModel
        {
            Items = [Item("1", "S1", "1S1", contractId)],
            SelectedContractIds = []
        };

        var row = Assert.Single(model.ItemRows);
        Assert.False(model.IsRowVisible(row));
        Assert.True(model.HasNoItems);

        model.SelectedContractIds = [contractId];

        Assert.True(model.IsRowVisible(row));
        Assert.False(model.HasNoItems);
    }

    private static RenewableContractItemDto Item(string labCode, string oldScheme, string identifier, Guid? contractId = null) => new(
        contractId ?? Guid.NewGuid(),
        "A",
        Guid.NewGuid(),
        labCode,
        "Lab One",
        oldScheme,
        "Old Scheme",
        "S9",
        "New Scheme",
        IsRenewable: true,
        identifier);
}
